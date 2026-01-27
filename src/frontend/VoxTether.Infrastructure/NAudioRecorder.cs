using Microsoft.Extensions.Logging;
using NAudio.Wave;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Audio recorder implementation using NAudio.
/// </summary>
public class NAudioRecorder : IAudioRecorder
{
    private readonly ILogger<NAudioRecorder> _logger;
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentOutputPath;
    private bool _disposed;

    /// <summary>
    /// Sample rate for recording (16kHz for Whisper compatibility).
    /// </summary>
    private const int SampleRate = 16000;

    /// <summary>
    /// Mono channel.
    /// </summary>
    private const int Channels = 1;

    /// <summary>
    /// Bits per sample.
    /// </summary>
    private const int BitsPerSample = 16;

    public NAudioRecorder(ILogger<NAudioRecorder> logger)
    {
        _logger = logger;
    }

    public bool IsRecording => _waveIn != null;

    public int SelectedDeviceId { get; set; } = -1;

    public event EventHandler? RecordingStarted;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<int>? AudioLevelChanged;

    public void StartRecording(string outputWavPath)
    {
        if (IsRecording)
        {
            _logger.LogWarning("Already recording");
            return;
        }

        try
        {
            _currentOutputPath = outputWavPath;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputWavPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var deviceNumber = SelectedDeviceId >= 0 ? SelectedDeviceId : 0;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                DeviceNumber = deviceNumber,
                BufferMilliseconds = 50
            };

            _writer = new WaveFileWriter(outputWavPath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _logger.LogInformation("Recording started to {Path}", outputWavPath);
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            CleanupRecording();
            throw;
        }
    }

    public string StopRecording()
    {
        if (!IsRecording || _currentOutputPath == null)
        {
            _logger.LogWarning("Not recording");
            return string.Empty;
        }

        var outputPath = _currentOutputPath;

        try
        {
            _waveIn?.StopRecording();
            _logger.LogInformation("Recording stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording");
        }
        finally
        {
            CleanupRecording();
        }

        return outputPath;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

        // Calculate audio level for visualization
        var level = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
        AudioLevelChanged?.Invoke(this, level);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        var path = _currentOutputPath ?? string.Empty;
        CleanupRecording();

        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error");
        }

        RecordingStopped?.Invoke(this, path);
    }

    private void CleanupRecording()
    {
        if (_writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _currentOutputPath = null;
    }

    private static int CalculateAudioLevel(byte[] buffer, int bytesRecorded)
    {
        // Calculate RMS level from 16-bit samples
        long sum = 0;
        var sampleCount = bytesRecorded / 2;

        for (int i = 0; i < bytesRecorded; i += 2)
        {
            if (i + 1 < bytesRecorded)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                sum += sample * sample;
            }
        }

        if (sampleCount == 0) return 0;

        var rms = Math.Sqrt(sum / (double)sampleCount);
        var level = (int)(rms / 32768.0 * 100);
        return Math.Min(100, level);
    }

    public bool HasRecordingDevice()
    {
        return WaveInEvent.DeviceCount > 0;
    }

    public string? GetDefaultDeviceName()
    {
        if (WaveInEvent.DeviceCount == 0)
            return null;

        var capabilities = WaveInEvent.GetCapabilities(0);
        return capabilities.ProductName;
    }

    public List<(int DeviceId, string DeviceName)> GetAvailableDevices()
    {
        var devices = new List<(int, string)>();

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var capabilities = WaveInEvent.GetCapabilities(i);
            devices.Add((i, capabilities.ProductName));
        }

        return devices;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupRecording();
        GC.SuppressFinalize(this);
    }
}
