using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Core.Services;

/// <summary>
/// Service for checking updates from GitHub releases.
/// </summary>
public class GitHubUpdateService : IUpdateService
{
    private readonly ILogger<GitHubUpdateService> _logger;
    
    // Use static HttpClient to prevent socket exhaustion
    // See: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
    private static readonly HttpClient SharedHttpClient;
    
    private const string GitHubApiUrl = "https://api.github.com/repos/KennethHeine/VoxTether/releases/latest";
    
    // Asset file patterns for matching release downloads
    private const string InstallerExtension = ".exe";
    private const string InstallerPattern = "Setup";
    private const string PortableExtension = ".zip";
    private const string PortablePattern = "portable";
    
    private const int BufferSize = 8192;
    private const double BytesToMb = 1024.0 * 1024.0;

    /// <summary>
    /// Event raised when download status changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    static GitHubUpdateService()
    {
        SharedHttpClient = new HttpClient();
        SharedHttpClient.DefaultRequestHeaders.Add("User-Agent", "VoxTether");
        SharedHttpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        // Set a reasonable timeout for update checks
        SharedHttpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public GitHubUpdateService(ILogger<GitHubUpdateService> logger)
    {
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates. Current version: {CurrentVersion}", currentVersion);

            var response = await SharedHttpClient.GetAsync(GitHubApiUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var latestVersion = tagName.TrimStart('v');
            var htmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

            string? installerUrl = null;
            string? portableUrl = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? string.Empty;
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();

                    if (IsInstallerAsset(name))
                    {
                        installerUrl = downloadUrl;
                    }
                    else if (IsPortableAsset(name))
                    {
                        portableUrl = downloadUrl;
                    }
                }
            }

            var isNewer = IsNewerVersion(currentVersion, latestVersion);
            
            _logger.LogInformation(
                "Update check complete. Latest: {LatestVersion}, Current: {CurrentVersion}, UpdateAvailable: {IsNewer}",
                latestVersion, currentVersion, isNewer);

            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseUrl = htmlUrl,
                InstallerUrl = installerUrl,
                PortableUrl = portableUrl,
                ReleaseNotes = body,
                IsNewerVersion = isNewer
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    public void OpenReleasePage(UpdateInfo updateInfo)
    {
        if (string.IsNullOrEmpty(updateInfo.ReleaseUrl))
        {
            _logger.LogWarning("Cannot open release page: URL is empty");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = updateInfo.ReleaseUrl,
                UseShellExecute = true
            });
            _logger.LogInformation("Opened release page: {Url}", updateInfo.ReleaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open release page");
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(
        UpdateInfo updateInfo,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(updateInfo.InstallerUrl))
        {
            _logger.LogWarning("Cannot download update: Installer URL is empty");
            StatusChanged?.Invoke("No installer available for this update.");
            return false;
        }

        try
        {
            StatusChanged?.Invoke($"Downloading VoxTether v{updateInfo.Version}...");
            _logger.LogInformation("Downloading update from: {Url}", updateInfo.InstallerUrl);

            // Download to temp folder
            var tempPath = SettingsService.TempPath;
            var installerFileName = $"VoxTether-Setup-{updateInfo.Version}.exe";
            var installerPath = Path.Combine(tempPath, installerFileName);

            // Create a new HttpClient with longer timeout for downloads
            using var downloadClient = new HttpClient();
            downloadClient.Timeout = TimeSpan.FromMinutes(10);

            using var response = await downloadClient.GetAsync(
                updateInfo.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalMb = totalBytes > 0 ? totalBytes / BytesToMb : 0;

            StatusChanged?.Invoke($"Downloading VoxTether v{updateInfo.Version} ({totalMb:F1} MB)...");

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

            var buffer = new byte[BufferSize];
            var totalBytesRead = 0L;
            var lastReportedProgress = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalBytesRead * 100) / totalBytes);
                    if (progressPercent != lastReportedProgress)
                    {
                        lastReportedProgress = progressPercent;
                        progress?.Report(progressPercent);
                        StatusChanged?.Invoke($"Downloading VoxTether v{updateInfo.Version}: {progressPercent}%");
                    }
                }
            }

            // Close the file stream before launching
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            StatusChanged?.Invoke("Download complete. Launching installer...");
            _logger.LogInformation("Update downloaded to: {Path}", installerPath);

            // Launch the installer with silent/automatic upgrade flag
            // The /SILENT flag performs a silent install with progress display
            // The /CLOSEAPPLICATIONS flag will close VoxTether if running
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS",
                UseShellExecute = true
            });

            _logger.LogInformation("Installer launched, application will be updated");
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke("Download cancelled.");
            _logger.LogInformation("Update download was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Download failed: {ex.Message}");
            _logger.LogError(ex, "Failed to download and install update");
            return false;
        }
    }

    /// <summary>
    /// Determines if an asset name matches the installer pattern.
    /// </summary>
    private static bool IsInstallerAsset(string name)
    {
        return name.EndsWith(InstallerExtension, StringComparison.OrdinalIgnoreCase) && 
               name.Contains(InstallerPattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if an asset name matches the portable package pattern.
    /// </summary>
    private static bool IsPortableAsset(string name)
    {
        return name.EndsWith(PortableExtension, StringComparison.OrdinalIgnoreCase) && 
               name.Contains(PortablePattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compares two version strings to determine if the latest is newer than current.
    /// </summary>
    private static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        // Normalize versions - remove any leading 'v' and metadata
        currentVersion = NormalizeVersion(currentVersion);
        latestVersion = NormalizeVersion(latestVersion);

        if (Version.TryParse(currentVersion, out var current) &&
            Version.TryParse(latestVersion, out var latest))
        {
            return latest > current;
        }

        // Fallback to string comparison if version parsing fails
        return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string NormalizeVersion(string version)
    {
        // Remove leading 'v'
        version = version.TrimStart('v', 'V');
        
        // Remove build metadata (anything after '+')
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        // Remove prerelease suffix for comparison (anything after '-')
        // Note: This means prerelease versions like '2.0.0-beta' are compared as '2.0.0'.
        // This is intentional for VoxTether since we typically release stable versions only.
        // If prerelease support is needed, this should be enhanced with proper semver comparison.
        var dashIndex = version.IndexOf('-');
        if (dashIndex >= 0)
        {
            version = version[..dashIndex];
        }

        return version;
    }
}
