using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Services;

/// <summary>
/// Main application controller that orchestrates all components.
/// </summary>
public class VoxTetherController : IDisposable
{
    private readonly ILogger<VoxTetherController> _logger;
    private readonly SettingsService _settingsService;
    private readonly IAudioRecorder _audioRecorder;
    private readonly IHotkeyService _hotkeyService;
    private readonly ITextInjector _textInjector;
    private readonly IBackendClient _backendClient;
    private readonly BackendProcessManager _backendProcess;
    
    private bool _isRecording;
    private string? _currentRecordingPath;
    private bool _disposed;

    public VoxTetherController(
        ILogger<VoxTetherController> logger,
        SettingsService settingsService,
        IAudioRecorder audioRecorder,
        IHotkeyService hotkeyService,
        ITextInjector textInjector,
        IBackendClient backendClient,
        BackendProcessManager backendProcess)
    {
        _logger = logger;
        _settingsService = settingsService;
        _audioRecorder = audioRecorder;
        _hotkeyService = hotkeyService;
        _textInjector = textInjector;
        _backendClient = backendClient;
        _backendProcess = backendProcess;
    }

    /// <summary>
    /// Gets a value indicating whether recording is in progress.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Event raised when recording state changes.
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;

    /// <summary>
    /// Event raised when status message changes.
    /// </summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Starts the controller and all services.
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting VoxTether controller");

        // Start the backend process
        try
        {
            await _backendProcess.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start backend");
            StatusChanged?.Invoke(this, "Backend error");
        }

        // Register hotkey
        var settings = _settingsService.Settings;
        
        if (!_hotkeyService.RegisterPushToTalk(
            settings.Hotkey,
            OnHotkeyPressed,
            OnHotkeyReleased))
        {
            _logger.LogError("Failed to register hotkey: {Hotkey}", settings.Hotkey);
        }
        else
        {
            _logger.LogInformation("Registered hotkey: {Hotkey}", settings.Hotkey);
        }

        // Configure text injector
        _textInjector.Mode = settings.OutputMode switch
        {
            "Clipboard" => TextInjectionMode.Clipboard,
            "ClipboardAndPaste" => TextInjectionMode.ClipboardAndPaste,
            "SimulateTyping" => TextInjectionMode.SimulateTyping,
            _ => TextInjectionMode.ClipboardAndPaste
        };
        _textInjector.ClipboardDelayMs = settings.ClipboardDelayMs;

        // Configure audio recorder
        _audioRecorder.SelectedDeviceId = settings.AudioDeviceId;

        StatusChanged?.Invoke(this, "Ready");
    }

    /// <summary>
    /// Updates the hotkey registration.
    /// </summary>
    public void UpdateHotkey(string hotkey)
    {
        _hotkeyService.UnregisterAll();
        
        if (!_hotkeyService.RegisterPushToTalk(hotkey, OnHotkeyPressed, OnHotkeyReleased))
        {
            _logger.LogError("Failed to register new hotkey: {Hotkey}", hotkey);
        }
        else
        {
            _logger.LogInformation("Updated hotkey to: {Hotkey}", hotkey);
        }
    }

    private void OnHotkeyPressed()
    {
        if (_isRecording) return;

        _logger.LogDebug("Hotkey pressed - starting recording");
        _isRecording = true;
        RecordingStateChanged?.Invoke(this, true);
        StatusChanged?.Invoke(this, "Recording...");

        try
        {
            // Create temp file for recording
            _currentRecordingPath = Path.Combine(
                Path.GetTempPath(),
                $"voxtether_{Guid.NewGuid()}.wav"
            );

            _audioRecorder.StartRecording(_currentRecordingPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _isRecording = false;
            RecordingStateChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Recording failed");
        }
    }

    private async void OnHotkeyReleased()
    {
        if (!_isRecording) return;

        _logger.LogDebug("Hotkey released - stopping recording");
        _isRecording = false;
        RecordingStateChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Transcribing...");

        try
        {
            _audioRecorder.StopRecording();

            if (!string.IsNullOrEmpty(_currentRecordingPath) && File.Exists(_currentRecordingPath))
            {
                // Transcribe
                var result = await _backendClient.TranscribeAsync(
                    _currentRecordingPath,
                    _settingsService.Settings.Language
                );

                if (result.Success && !string.IsNullOrEmpty(result.Text))
                {
                    _logger.LogInformation("Transcribed: {Text}", result.Text);
                    
                    // Inject text
                    _textInjector.InjectText(result.Text);
                    StatusChanged?.Invoke(this, "Ready");
                }
                else if (result.Success)
                {
                    _logger.LogDebug("No speech detected");
                    StatusChanged?.Invoke(this, "No speech detected");
                }
                else
                {
                    _logger.LogWarning("Transcription failed: {Error}", result.Error);
                    StatusChanged?.Invoke(this, "Transcription failed");
                }

                // Clean up temp file
                try 
                { 
                    File.Delete(_currentRecordingPath); 
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Could not delete temp file: {Path}", _currentRecordingPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
            StatusChanged?.Invoke(this, "Error");
        }

        _currentRecordingPath = null;
    }

    /// <summary>
    /// Stops the controller and all services.
    /// </summary>
    public void Stop()
    {
        _logger.LogInformation("Stopping VoxTether controller");

        // Stop recording if active
        if (_isRecording)
        {
            try { _audioRecorder.StopRecording(); } catch { }
            _isRecording = false;
        }

        // Unregister hotkey
        _hotkeyService.UnregisterAll();

        // Stop backend
        _backendProcess.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
