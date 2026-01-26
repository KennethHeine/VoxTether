namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for audio recording functionality.
/// </summary>
public interface IAudioRecorder : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether recording is in progress.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Event raised when recording starts.
    /// </summary>
    event EventHandler? RecordingStarted;

    /// <summary>
    /// Event raised when recording stops.
    /// </summary>
    event EventHandler<string>? RecordingStopped;

    /// <summary>
    /// Starts recording audio to the specified WAV file path.
    /// </summary>
    /// <param name="outputWavPath">The path where the WAV file will be saved.</param>
    void StartRecording(string outputWavPath);

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    /// <returns>The path to the recorded WAV file.</returns>
    string StopRecording();

    /// <summary>
    /// Checks if a recording device is available.
    /// </summary>
    /// <returns>True if at least one recording device is available.</returns>
    bool HasRecordingDevice();

    /// <summary>
    /// Gets the name of the default recording device.
    /// </summary>
    /// <returns>The device name or null if not available.</returns>
    string? GetDefaultDeviceName();
}
