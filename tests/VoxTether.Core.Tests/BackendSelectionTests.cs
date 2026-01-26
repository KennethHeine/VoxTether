using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Transcription;

namespace VoxTether.Core.Tests;

public class BackendSelectionTests
{
    /// <summary>
    /// Creates a mock logger for testing.
    /// </summary>
    private static ILogger<BackendSelectionService> CreateTestLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        return loggerFactory.CreateLogger<BackendSelectionService>();
    }

    [Fact]
    public void DetermineBackend_CpuOnly_AlwaysReturnsCpu()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act
        var result = service.DetermineBackend(TranscriptionBackendMode.CpuOnly);

        // Assert
        Assert.Equal(TranscriptionBackendMode.CpuOnly, result);
        Assert.Equal(TranscriptionBackendMode.CpuOnly, service.ActiveBackend);
        Assert.False(service.FellBackToCpu);
    }

    [Fact]
    public void DetermineBackend_Auto_ReturnsAvailableBackend()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act
        var result = service.DetermineBackend(TranscriptionBackendMode.Auto);

        // Assert - should select CPU as fallback when no accelerated backends are installed
        // (In test environment, no GPU executables will be present)
        Assert.True(result != TranscriptionBackendMode.Auto);
        Assert.Equal(result, service.ActiveBackend);
    }

    [Fact]
    public void DetermineBackend_UnavailableBackend_FallsBackToCpu()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act - Request CUDA which likely isn't available in test environment
        var result = service.DetermineBackend(TranscriptionBackendMode.Cuda);

        // Assert - Should fall back to CPU (unless CUDA is actually installed)
        if (!service.IsBackendAvailable(TranscriptionBackendMode.Cuda))
        {
            Assert.Equal(TranscriptionBackendMode.CpuOnly, result);
            Assert.True(service.FellBackToCpu);
            Assert.Equal(TranscriptionBackendMode.Cuda, service.RequestedBackend);
        }
        else
        {
            Assert.Equal(TranscriptionBackendMode.Cuda, result);
            Assert.False(service.FellBackToCpu);
        }
    }

    [Fact]
    public void DetermineBackend_CalledTwice_ReturnsInitialResult()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act
        var firstResult = service.DetermineBackend(TranscriptionBackendMode.CpuOnly);
        var secondResult = service.DetermineBackend(TranscriptionBackendMode.Auto);

        // Assert - Second call should return same result as first (initialized once)
        Assert.Equal(firstResult, secondResult);
        Assert.Equal(TranscriptionBackendMode.CpuOnly, service.ActiveBackend);
    }

    [Fact]
    public void GetAvailableBackends_ReturnsAllBackendTypes()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act
        var backends = service.GetAvailableBackends();

        // Assert - Should include CPU, CUDA, Vulkan, OpenVINO (not Auto)
        Assert.Equal(4, backends.Count);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.CpuOnly);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.Cuda);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.Vulkan);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.OpenVino);
    }

    [Fact]
    public void IsBackendAvailable_Auto_ReturnsTrue()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act & Assert
        Assert.True(service.IsBackendAvailable(TranscriptionBackendMode.Auto));
    }

    [Fact]
    public void GetGpuDiagnostics_ReturnsValidDiagnostics()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger);

        // Act
        var diagnostics = service.GetGpuDiagnostics();

        // Assert - Should return a valid object even if no GPUs detected
        Assert.NotNull(diagnostics);
        Assert.NotNull(diagnostics.DetectedGpus);
    }

    [Fact]
    public void GetDisplayName_ReturnsCorrectNames()
    {
        // Assert
        Assert.Equal("Auto", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.Auto));
        Assert.Equal("CPU Only", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.CpuOnly));
        Assert.Equal("NVIDIA CUDA", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.Cuda));
        Assert.Equal("Vulkan", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.Vulkan));
        Assert.Equal("Intel OpenVINO", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.OpenVino));
    }

    [Fact]
    public void BackendInfo_Properties_SetCorrectly()
    {
        // Arrange & Act
        var info = new BackendInfo
        {
            Backend = TranscriptionBackendMode.Cuda,
            IsAvailable = true,
            ExecutablePath = "/path/to/whisper_cuda.exe",
            UnavailableReason = null
        };

        // Assert
        Assert.Equal(TranscriptionBackendMode.Cuda, info.Backend);
        Assert.True(info.IsAvailable);
        Assert.Equal("/path/to/whisper_cuda.exe", info.ExecutablePath);
        Assert.Null(info.UnavailableReason);
    }

    [Fact]
    public void GpuDiagnostics_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var diagnostics = new GpuDiagnostics();

        // Assert
        Assert.NotNull(diagnostics.DetectedGpus);
        Assert.Empty(diagnostics.DetectedGpus);
        Assert.False(diagnostics.HasNvidiaGpu);
        Assert.False(diagnostics.HasIntelGpu);
        Assert.False(diagnostics.HasAmdGpu);
    }
}
