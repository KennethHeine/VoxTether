using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Services;

/// <summary>
/// Service for checking updates from GitHub releases.
/// </summary>
public class GitHubUpdateService : IUpdateService
{
    private readonly ILogger<GitHubUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/KennethHeine/VoxTether/releases/latest";

    public GitHubUpdateService(ILogger<GitHubUpdateService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VoxTether");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates. Current version: {CurrentVersion}", currentVersion);

            var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);
            
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

                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup"))
                    {
                        installerUrl = downloadUrl;
                    }
                    else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && name.Contains("portable"))
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
            version = version.Substring(0, plusIndex);
        }

        // Remove prerelease suffix for comparison (anything after '-')
        // But we should handle this more carefully in a real implementation
        var dashIndex = version.IndexOf('-');
        if (dashIndex >= 0)
        {
            version = version.Substring(0, dashIndex);
        }

        return version;
    }
}
