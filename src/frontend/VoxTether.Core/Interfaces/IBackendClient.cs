namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for communicating with the Python backend.
/// </summary>
public interface IBackendClient
{
    /// <summary>
    /// Checks if the backend is healthy and responding.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes an audio file.
    /// </summary>
    /// <param name="wavPath">Path to the WAV file.</param>
    /// <param name="language">Language code or "auto".</param>
    /// <param name="translate">Whether to translate to English.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        string language = "auto",
        bool translate = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available models.
    /// </summary>
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a model.
    /// </summary>
    /// <param name="modelName">Name of the model to download.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadModelAsync(
        string modelName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a model for transcription.
    /// </summary>
    Task<bool> LoadModelAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets device information.
    /// </summary>
    Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcription operation.
/// </summary>
public record TranscriptionResult(
    string Text,
    bool Success,
    double Duration,
    string? Language = null,
    string? Error = null);

/// <summary>
/// Information about a model.
/// </summary>
public record ModelInfo(
    string Name,
    string DisplayName,
    int SizeMb,
    bool Downloaded,
    string? Path = null,
    string Description = "");

/// <summary>
/// Progress of a model download.
/// </summary>
public record DownloadProgress(
    string Status,
    double Progress,
    double DownloadedMb,
    double TotalMb,
    double SpeedMbps = 0,
    string? Error = null);

/// <summary>
/// Information about compute devices.
/// </summary>
public record DeviceInfo(
    bool CudaAvailable,
    string? CudaVersion,
    string? DeviceName);
