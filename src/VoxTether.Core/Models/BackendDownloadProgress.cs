namespace VoxTether.Core.Models;

/// <summary>
/// Progress information for backend download operations.
/// </summary>
public class BackendDownloadProgress
{
    /// <summary>
    /// The backend being downloaded.
    /// </summary>
    public string BackendId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the download.
    /// </summary>
    public BackendDownloadStatus Status { get; set; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    /// Total bytes to download.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Download progress as a percentage (0-100).
    /// </summary>
    public int PercentComplete => TotalBytes > 0 
        ? (int)((BytesDownloaded * 100) / TotalBytes) 
        : 0;

    /// <summary>
    /// Current operation description.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error message if download failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a backend download operation.
/// </summary>
public enum BackendDownloadStatus
{
    /// <summary>
    /// Download is queued but not started.
    /// </summary>
    Queued,

    /// <summary>
    /// Currently downloading the backend package.
    /// </summary>
    Downloading,

    /// <summary>
    /// Validating the downloaded file checksum.
    /// </summary>
    Validating,

    /// <summary>
    /// Extracting the downloaded package.
    /// </summary>
    Extracting,

    /// <summary>
    /// Download and installation completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Download or installation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Download was cancelled by the user.
    /// </summary>
    Cancelled
}
