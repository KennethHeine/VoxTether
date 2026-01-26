using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether;

/// <summary>
/// Main controller that orchestrates recording, transcription, and text injection.
/// </summary>
public class VoxTetherController
{
    private readonly ILogger<VoxTetherController> _logger;
    private readonly SettingsService _settingsService;
    private readonly IAudioRecorder _recorder;
    private readonly IHotkeyService _hotkeyService;
    private readonly ITranscriptionEngine _transcriptionEngine;
    private readonly ITextInjector _textInjector;
    private readonly ITextPostProcessor _postProcessor;

    private CancellationTokenSource? _transcriptionCts;
    private string _currentRecordingPath = string.Empty;
    private bool _isRecording;
    private bool _isTranscribing;

    /// <summary>
    /// Event raised when recording state changes.
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;

    /// <summary>
    /// Event raised when transcription is complete.
    /// </summary>
    public event EventHandler<string>? TranscriptionComplete;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Gets whether recording is in progress.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Gets whether transcription is in progress.
    /// </summary>
    public bool IsTranscribing => _isTranscribing;

    public VoxTetherController(
        ILogger<VoxTetherController> logger,
        SettingsService settingsService,
        IAudioRecorder recorder,
        IHotkeyService hotkeyService,
        ITranscriptionEngine transcriptionEngine,
        ITextInjector textInjector,
        ITextPostProcessor postProcessor)
    {
        _logger = logger;
        _settingsService = settingsService;
        _recorder = recorder;
        _hotkeyService = hotkeyService;
        _transcriptionEngine = transcriptionEngine;
        _textInjector = textInjector;
        _postProcessor = postProcessor;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.HotkeyReleased += OnHotkeyReleased;
    }

    /// <summary>
    /// Starts the controller and hotkey listening.
    /// </summary>
    public void Start()
    {
        // Configure hotkey from settings
        var hotkeyString = _settingsService.Settings.Hotkey;
        _hotkeyService.Hotkey = HotkeyCombination.Parse(hotkeyString);
        
        _hotkeyService.Start();
        _logger.LogInformation("VoxTether controller started, hotkey: {Hotkey}", _hotkeyService.Hotkey);
    }

    /// <summary>
    /// Stops the controller and releases resources.
    /// </summary>
    public void Stop()
    {
        _hotkeyService.Stop();
        
        if (_isRecording)
        {
            _recorder.StopRecording();
            _isRecording = false;
        }

        _transcriptionCts?.Cancel();
        _logger.LogInformation("VoxTether controller stopped");
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            _logger.LogDebug("Already recording, ignoring hotkey press");
            return;
        }

        StartRecording();
    }

    private void OnHotkeyReleased(object? sender, EventArgs e)
    {
        if (!_isRecording)
        {
            _logger.LogDebug("Not recording, ignoring hotkey release");
            return;
        }

        StopRecordingAndTranscribe();
    }

    private void StartRecording()
    {
        try
        {
            // Generate unique file path
            var tempPath = SettingsService.TempPath;
            var fileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.wav";
            _currentRecordingPath = Path.Combine(tempPath, fileName);

            _recorder.StartRecording(_currentRecordingPath);
            _isRecording = true;
            
            _logger.LogInformation("Recording started: {Path}", _currentRecordingPath);
            RecordingStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
        }
    }

    private void StopRecordingAndTranscribe()
    {
        try
        {
            var wavPath = _recorder.StopRecording();
            _isRecording = false;
            RecordingStateChanged?.Invoke(this, false);
            
            _logger.LogInformation("Recording stopped: {Path}", wavPath);

            // Cancel any previous transcription
            _transcriptionCts?.Cancel();
            _transcriptionCts = new CancellationTokenSource();

            // Start transcription in background
            _ = TranscribeAndInjectAsync(wavPath, _transcriptionCts.Token);
        }
        catch (Exception ex)
        {
            _isRecording = false;
            RecordingStateChanged?.Invoke(this, false);
            _logger.LogError(ex, "Failed to stop recording");
            ErrorOccurred?.Invoke(this, $"Failed to stop recording: {ex.Message}");
        }
    }

    private async Task TranscribeAndInjectAsync(string wavPath, CancellationToken cancellationToken)
    {
        try
        {
            _isTranscribing = true;
            
            var modelPath = _settingsService.GetEffectiveModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                _logger.LogError("No model file available");
                ErrorOccurred?.Invoke(this, "No model file available. Please add a model to the models folder.");
                return;
            }

            var options = new TranscriptionOptions
            {
                ModelPath = modelPath,
                Language = _settingsService.Settings.Language
            };

            _logger.LogInformation("Starting transcription with model: {Model}", modelPath);

            var result = await _transcriptionEngine.TranscribeAsync(wavPath, options, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Transcription failed: {Error}", result.Error);
                ErrorOccurred?.Invoke(this, $"Transcription failed: {result.Error}");
                return;
            }

            if (string.IsNullOrEmpty(result.Text))
            {
                _logger.LogWarning("Transcription returned empty text");
                return;
            }

            // Post-process the text
            var processedText = await _postProcessor.ProcessAsync(result.Text, cancellationToken);

            _logger.LogInformation("Transcription result: {Text}", processedText);
            TranscriptionComplete?.Invoke(this, processedText);

            // Inject the text
            var injected = await _textInjector.InjectAsync(processedText, cancellationToken);
            
            if (!injected)
            {
                _logger.LogWarning("Text injection failed or was skipped");
            }

            // Clean up temp file
            try
            {
                if (File.Exists(wavPath))
                {
                    File.Delete(wavPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete temp file: {Path}", wavPath);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            ErrorOccurred?.Invoke(this, $"Transcription error: {ex.Message}");
        }
        finally
        {
            _isTranscribing = false;
        }
    }

    /// <summary>
    /// Tests the microphone by recording for 2 seconds and transcribing.
    /// </summary>
    public async Task<string> TestMicrophoneAsync()
    {
        try
        {
            var tempPath = SettingsService.TempPath;
            var fileName = $"test_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var wavPath = Path.Combine(tempPath, fileName);

            _recorder.StartRecording(wavPath);
            await Task.Delay(2000);
            _recorder.StopRecording();

            var modelPath = _settingsService.GetEffectiveModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                return "No model file available";
            }

            var options = new TranscriptionOptions
            {
                ModelPath = modelPath,
                Language = _settingsService.Settings.Language
            };

            var result = await _transcriptionEngine.TranscribeAsync(wavPath, options);

            try { File.Delete(wavPath); } catch (IOException) { /* Ignore cleanup errors */ }

            if (result.Success)
            {
                return string.IsNullOrEmpty(result.Text) 
                    ? "[Silence detected]" 
                    : result.Text;
            }
            else
            {
                return $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"Test failed: {ex.Message}";
        }
    }
}
