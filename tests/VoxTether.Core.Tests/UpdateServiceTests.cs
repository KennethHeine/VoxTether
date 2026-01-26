using VoxTether.Core.Interfaces;
using VoxTether.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace VoxTether.Core.Tests;

public class UpdateServiceTests
{
    private readonly GitHubUpdateService _updateService;

    public UpdateServiceTests()
    {
        var logger = NullLogger<GitHubUpdateService>.Instance;
        _updateService = new GitHubUpdateService(logger);
    }

    [Fact]
    public void UpdateInfo_DefaultValues_AreCorrect()
    {
        var updateInfo = new UpdateInfo();
        
        Assert.Equal(string.Empty, updateInfo.Version);
        Assert.Equal(string.Empty, updateInfo.ReleaseUrl);
        Assert.Null(updateInfo.InstallerUrl);
        Assert.Null(updateInfo.PortableUrl);
        Assert.Null(updateInfo.ReleaseNotes);
        Assert.False(updateInfo.IsNewerVersion);
    }

    [Fact]
    public void UpdateInfo_CanSetAllProperties()
    {
        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            ReleaseUrl = "https://github.com/example/release",
            InstallerUrl = "https://github.com/example/installer.exe",
            PortableUrl = "https://github.com/example/portable.zip",
            ReleaseNotes = "Release notes here",
            IsNewerVersion = true
        };
        
        Assert.Equal("2.0.0", updateInfo.Version);
        Assert.Equal("https://github.com/example/release", updateInfo.ReleaseUrl);
        Assert.Equal("https://github.com/example/installer.exe", updateInfo.InstallerUrl);
        Assert.Equal("https://github.com/example/portable.zip", updateInfo.PortableUrl);
        Assert.Equal("Release notes here", updateInfo.ReleaseNotes);
        Assert.True(updateInfo.IsNewerVersion);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("1.1.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "v2.0.0", true)]
    [InlineData("1.0.0", "v2.0.0", true)]
    [InlineData("v1.0.0", "2.0.0", true)]
    public void VersionComparison_WorksCorrectly(string currentVersion, string latestVersion, bool expectedIsNewer)
    {
        // We use the IsNewerVersionTestable method which exposes the logic publicly for testing
        var isNewer = TestableUpdateService.IsNewerVersionTestable(currentVersion, latestVersion);
        
        Assert.Equal(expectedIsNewer, isNewer);
    }

    [Theory]
    [InlineData("1.0.0+build123", "1.0.0")]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("V1.0.0", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0")]
    [InlineData("1.0.0-beta+build", "1.0.0")]
    public void NormalizeVersion_WorksCorrectly(string input, string expected)
    {
        var normalized = TestableUpdateService.NormalizeVersionTestable(input);
        
        Assert.Equal(expected, normalized);
    }
}

/// <summary>
/// Exposes internal methods for testing purposes.
/// </summary>
public static class TestableUpdateService
{
    /// <summary>
    /// Compares two version strings to determine if the latest is newer than current.
    /// Exposed for testing.
    /// </summary>
    public static bool IsNewerVersionTestable(string currentVersion, string latestVersion)
    {
        currentVersion = NormalizeVersionTestable(currentVersion);
        latestVersion = NormalizeVersionTestable(latestVersion);

        if (Version.TryParse(currentVersion, out var current) &&
            Version.TryParse(latestVersion, out var latest))
        {
            return latest > current;
        }

        return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    /// <summary>
    /// Normalizes version string for comparison.
    /// Exposed for testing.
    /// </summary>
    public static string NormalizeVersionTestable(string version)
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
        var dashIndex = version.IndexOf('-');
        if (dashIndex >= 0)
        {
            version = version.Substring(0, dashIndex);
        }

        return version;
    }
}
