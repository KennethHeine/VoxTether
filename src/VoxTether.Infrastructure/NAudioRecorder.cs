using System.IO;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Audio recorder implementation using NAudio.
/// Records audio to 16kHz mono PCM WAV format.
/// </summary>
public class NAudioRecorder : IAudioRecorder
{
    private readonly ILogger<NAudioRecorder> _logger;
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string _currentOutputPath = string.Empty;
    private bool _disposed;

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public bool IsRecording => _waveIn != null && _writer != null;
    
    /// <summary>
    /// Gets or sets the selected device ID for recording.
    /// -1 means use the default device (device 0).
    /// </summary>
    public int SelectedDeviceId { get; set; } = -1;

    public event EventHandler? RecordingStarted;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<int>? AudioLevelChanged;

    public NAudioRecorder(ILogger<NAudioRecorder> logger)
    {
        _logger = logger;
    }

    public void StartRecording(string outputWavPath)
    {
        if (IsRecording)
        {
            _logger.LogWarning("Already recording, ignoring start request");
            return;
        }

        try
        {
            _currentOutputPath = outputWavPath;
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputWavPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use selected device or default (0)
            var deviceId = SelectedDeviceId >= 0 ? SelectedDeviceId : 0;
            
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceId,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 50
            };

            _writer = new WaveFileWriter(outputWavPath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _logger.LogInformation("Recording started: {Path} with device {DeviceId}", outputWavPath, deviceId);
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            Cleanup();
            throw;
        }
    }

    public string StopRecording()
    {
        if (!IsRecording)
        {
            _logger.LogWarning("Not recording, nothing to stop");
            return string.Empty;
        }

        var path = _currentOutputPath;
        
        try
        {
            _waveIn?.StopRecording();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording");
        }
        finally
        {
            Cleanup();
        }

        _logger.LogInformation("Recording stopped: {Path}", path);
        RecordingStopped?.Invoke(this, path);
        return path;
    }

    public bool HasRecordingDevice()
    {
        try
        {
            return WaveInEvent.DeviceCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for recording devices");
            return false;
        }
    }

    public string? GetDefaultDeviceName()
    {
        try
        {
            if (WaveInEvent.DeviceCount > 0)
            {
                var capabilities = WaveInEvent.GetCapabilities(0);
                return capabilities.ProductName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default device name");
        }
        return null;
    }

    public List<(int DeviceId, string DeviceName)> GetAvailableDevices()
    {
        var devices = new List<(int DeviceId, string DeviceName)>();
        try
        {
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices.Add((i, capabilities.ProductName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available devices");
        }
        return devices;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
            
            // Calculate audio level for visualization
            if (e.BytesRecorded > 0 && AudioLevelChanged != null)
            {
                var level = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
                AudioLevelChanged.Invoke(this, level);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing audio data");
        }
    }

    /// <summary>
    /// Calculates the audio level (0-100) from the buffer.
    /// </summary>
    private static int CalculateAudioLevel(byte[] buffer, int bytesRecorded)
    {
        // 16-bit audio samples (2 bytes per sample)
        var maxValue = 0;
        for (var i = 0; i + 1 < bytesRecorded; i += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(buffer, i));
            if (sample > maxValue)
            {
                maxValue = sample;
            }
        }
        
        // Convert to percentage (0-100)
        return (int)(maxValue * 100.0 / short.MaxValue);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error");
        }
    }

    private void Cleanup()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (IsRecording)
        {
            StopRecording();
        }
        
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
