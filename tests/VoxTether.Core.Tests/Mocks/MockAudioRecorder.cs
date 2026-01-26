using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

/// <summary>
/// Mock implementation of IAudioRecorder for testing.
/// Returns pre-recorded audio files instead of actually recording.
/// </summary>
public class MockAudioRecorder : IAudioRecorder
{
    private readonly string _testAudioPath;
    private bool _isRecording;

    public bool IsRecording => _isRecording;
    public int SelectedDeviceId { get; set; } = -1;

    public event EventHandler? RecordingStarted;
    public event EventHandler<string>? RecordingStopped;
#pragma warning disable CS0067 // Event is never used - required by interface
    public event EventHandler<int>? AudioLevelChanged;
#pragma warning restore CS0067

    public MockAudioRecorder(string testAudioPath)
    {
        _testAudioPath = testAudioPath;
    }

    public void StartRecording(string outputWavPath)
    {
        _isRecording = true;
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    public string StopRecording()
    {
        _isRecording = false;
        // Return the test audio file path instead of recording
        RecordingStopped?.Invoke(this, _testAudioPath);
        return _testAudioPath;
    }

    public bool HasRecordingDevice() => true;
    public string? GetDefaultDeviceName() => "Mock Microphone";
    public List<(int DeviceId, string DeviceName)> GetAvailableDevices()
        => [(0, "Mock Microphone")];

    public void Dispose() { }
}
