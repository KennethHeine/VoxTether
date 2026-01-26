using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Transcription;

namespace VoxTether.Core.Tests;

public class BackendDownloadTests
{
    private static ILogger<BackendDownloadService> CreateTestLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        return loggerFactory.CreateLogger<BackendDownloadService>();
    }

    private static ILogger<BackendSelectionService> CreateBackendSelectionLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        return loggerFactory.CreateLogger<BackendSelectionService>();
    }

    [Fact]
    public async Task GetManifestAsync_ReturnsValidManifest()
    {
        // Arrange
        var logger = CreateTestLogger();
        var backendSelection = new BackendSelectionService(CreateBackendSelectionLogger());
        using var service = new BackendDownloadService(logger, backendSelection);

        // Act
        var manifest = await service.GetManifestAsync();

        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.Backends);
        Assert.Contains(manifest.Backends, b => b.Id == "cuda");
        Assert.Contains(manifest.Backends, b => b.Id == "vulkan");
        Assert.Contains(manifest.Backends, b => b.Id == "openvino");
    }

    [Fact]
    public void GetAvailableDiskSpace_ReturnsPositiveValue()
    {
        // Arrange
        var logger = CreateTestLogger();
        var backendSelection = new BackendSelectionService(CreateBackendSelectionLogger());
        using var service = new BackendDownloadService(logger, backendSelection);

        // Act
        var space = service.GetAvailableDiskSpace();

        // Assert
        Assert.True(space > 0);
    }

    [Fact]
    public void IsBackendInstalled_ForNonExistentBackend_ReturnsFalse()
    {
        // Arrange
        var logger = CreateTestLogger();
        var backendSelection = new BackendSelectionService(CreateBackendSelectionLogger());
        using var service = new BackendDownloadService(logger, backendSelection);

        // Act
        var isInstalled = service.IsBackendInstalled("nonexistent-backend");

        // Assert
        Assert.False(isInstalled);
    }

    [Fact]
    public void GetRecommendedBackends_ReturnsListOfBackends()
    {
        // Arrange
        var logger = CreateTestLogger();
        var backendSelection = new BackendSelectionService(CreateBackendSelectionLogger());
        using var service = new BackendDownloadService(logger, backendSelection);

        // Act
        var recommended = service.GetRecommendedBackends();

        // Assert
        Assert.NotNull(recommended);
        // In test environment, may or may not have GPU hardware
    }

    [Fact]
    public async Task RemoveBackendAsync_ForNonExistentBackend_ReturnsFalse()
    {
        // Arrange
        var logger = CreateTestLogger();
        var backendSelection = new BackendSelectionService(CreateBackendSelectionLogger());
        using var service = new BackendDownloadService(logger, backendSelection);

        // Act
        var result = await service.RemoveBackendAsync("nonexistent-backend");

        // Assert
        Assert.False(result);
    }
}

public class BackendManifestTests
{
    [Fact]
    public void BackendPackageInfo_GetBackendMode_ReturnsCorrectMode()
    {
        // Arrange & Act
        var cudaPackage = new BackendPackageInfo { Id = "cuda" };
        var vulkanPackage = new BackendPackageInfo { Id = "vulkan" };
        var openvinoPackage = new BackendPackageInfo { Id = "openvino" };
        var unknownPackage = new BackendPackageInfo { Id = "unknown" };

        // Assert
        Assert.Equal(TranscriptionBackendMode.Cuda, cudaPackage.GetBackendMode());
        Assert.Equal(TranscriptionBackendMode.Vulkan, vulkanPackage.GetBackendMode());
        Assert.Equal(TranscriptionBackendMode.OpenVino, openvinoPackage.GetBackendMode());
        Assert.Equal(TranscriptionBackendMode.CpuOnly, unknownPackage.GetBackendMode());
    }

    [Fact]
    public void BackendManifest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var manifest = new BackendManifest();

        // Assert
        Assert.Equal("1.0", manifest.Version);
        Assert.NotNull(manifest.Backends);
        Assert.Empty(manifest.Backends);
    }
}

public class BackendDownloadProgressTests
{
    [Fact]
    public void PercentComplete_CalculatesCorrectly()
    {
        // Arrange
        var progress = new BackendDownloadProgress
        {
            BytesDownloaded = 50,
            TotalBytes = 100
        };

        // Act
        var percent = progress.PercentComplete;

        // Assert
        Assert.Equal(50, percent);
    }

    [Fact]
    public void PercentComplete_WithZeroTotal_ReturnsZero()
    {
        // Arrange
        var progress = new BackendDownloadProgress
        {
            BytesDownloaded = 50,
            TotalBytes = 0
        };

        // Act
        var percent = progress.PercentComplete;

        // Assert
        Assert.Equal(0, percent);
    }

    [Fact]
    public void BackendDownloadStatus_AllValuesAreDefined()
    {
        // Assert - Verify all enum values are accessible
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Queued));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Downloading));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Validating));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Extracting));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Completed));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Failed));
        Assert.True(Enum.IsDefined(typeof(BackendDownloadStatus), BackendDownloadStatus.Cancelled));
    }
}
