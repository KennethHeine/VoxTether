namespace VoxTether.Core.Interfaces;

/// <summary>
/// Represents information about an available update.
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// The version string of the latest available release.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The URL to the release page.
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The URL to download the installer.
    /// </summary>
    public string? InstallerUrl { get; set; }

    /// <summary>
    /// The URL to download the portable version.
    /// </summary>
    public string? PortableUrl { get; set; }

    /// <summary>
    /// Release notes or description.
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Whether this is a newer version than the currently installed one.
    /// </summary>
    public bool IsNewerVersion { get; set; }
}

/// <summary>
/// Service for checking and downloading application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for available updates.
    /// </summary>
    /// <param name="currentVersion">The current installed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update information if available, or null if up to date or check failed.</returns>
    Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the release page in the default browser.
    /// </summary>
    /// <param name="updateInfo">The update information containing the release URL.</param>
    void OpenReleasePage(UpdateInfo updateInfo);

    /// <summary>
    /// Downloads and installs an update from the specified URL.
    /// </summary>
    /// <param name="updateInfo">The update information containing the installer URL.</param>
    /// <param name="progress">Optional progress reporter (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the download was successful and installer was launched, false otherwise.</returns>
    Task<bool> DownloadAndInstallUpdateAsync(
        UpdateInfo updateInfo,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when download status changes.
    /// </summary>
    event Action<string>? StatusChanged;
}
