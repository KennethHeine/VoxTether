using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Tests.Utilities;
using VoxTether.Transcription;
using Xunit.Abstractions;

namespace VoxTether.Core.Tests.IntegrationTests;

/// <summary>
/// Integration tests for backend detection and selection.
/// Tests that backends are correctly detected and selected.
/// </summary>
[Trait("Category", "Integration")]
public class BackendIntegrationTests : TestBase
{
    public BackendIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void GetAvailableBackends_ReturnsAllBackendTypes()
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act
        var backends = selectionService.GetAvailableBackends();

        // Assert
        Assert.NotNull(backends);
        Assert.NotEmpty(backends);

        // Should have entries for all non-Auto backend types
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.CpuOnly);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.Cuda);

        // Log results for CI visibility
        foreach (var backend in backends)
        {
            var status = backend.IsAvailable ? "Available" : $"Unavailable ({backend.UnavailableReason})";
            Logger.LogInformation("Backend: {Id} - {Status}", backend.Backend, status);
        }
    }

    [Fact]
    public void GetGpuDiagnostics_ReturnsDiagnostics()
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act
        var diagnostics = selectionService.GetGpuDiagnostics();

        // Assert
        Assert.NotNull(diagnostics);
        Assert.NotNull(diagnostics.DetectedGpus);

        // Log results for CI visibility
        Logger.LogInformation("GPU Diagnostics:");
        Logger.LogInformation("  Has NVIDIA: {HasNvidia}", diagnostics.HasNvidiaGpu);
        Logger.LogInformation("  Has Intel: {HasIntel}", diagnostics.HasIntelGpu);
        Logger.LogInformation("  Has AMD: {HasAmd}", diagnostics.HasAmdGpu);
        foreach (var gpu in diagnostics.DetectedGpus)
        {
            Logger.LogInformation("  Detected: {Gpu}", gpu);
        }
    }

    [Theory]
    [InlineData(TranscriptionBackendMode.Auto)]
    [InlineData(TranscriptionBackendMode.CpuOnly)]
    [InlineData(TranscriptionBackendMode.Cuda)]
    public void DetermineBackend_AllModesAreHandled(TranscriptionBackendMode mode)
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act
        var result = selectionService.DetermineBackend(mode);

        // Assert - should return a valid backend mode
        Assert.True(Enum.IsDefined(typeof(TranscriptionBackendMode), result));

        // Log for CI visibility
        Logger.LogInformation("Mode: {Mode} -> Selected: {Result}", mode, result);

        // ActiveBackend should be set after DetermineBackend is called
        Assert.Equal(result, selectionService.ActiveBackend);
    }

    [Fact]
    public void DetermineBackend_Auto_SelectsBestAvailable()
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act
        var result = selectionService.DetermineBackend(TranscriptionBackendMode.Auto);

        // Assert - Auto should resolve to a specific backend (not Auto itself)
        Assert.NotEqual(TranscriptionBackendMode.Auto, result);
        Assert.Equal(result, selectionService.ActiveBackend);

        Logger.LogInformation("Auto mode resolved to: {Result}", result);
    }

    [Fact]
    public void DetermineBackend_CpuOnly_AlwaysSucceeds()
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act
        var result = selectionService.DetermineBackend(TranscriptionBackendMode.CpuOnly);

        // Assert - CPU mode should always work
        Assert.Equal(TranscriptionBackendMode.CpuOnly, result);
        Assert.False(selectionService.FellBackToCpu);

        Logger.LogInformation("CPU-only mode: {Result}", result);
    }

    [Fact]
    public void DetermineBackend_IsCalledOnce()
    {
        // Arrange
        var selectionService = new BackendSelectionService(
            LoggerFactory.CreateLogger<BackendSelectionService>(),
            skipRuntimeValidation: true);

        // Act - Call DetermineBackend twice with different modes
        var firstResult = selectionService.DetermineBackend(TranscriptionBackendMode.Auto);
        var secondResult = selectionService.DetermineBackend(TranscriptionBackendMode.CpuOnly);

        // Assert - Second call should return the same result (already initialized)
        Assert.Equal(firstResult, secondResult);
        Assert.Equal(firstResult, selectionService.ActiveBackend);

        Logger.LogInformation("Backend selection is idempotent: {Result}", firstResult);
    }

    [Fact]
    public void BackendDisplayNames_AreHumanReadable()
    {
        // Verify display names are properly mapped
        Assert.Equal("Auto", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.Auto));
        Assert.Equal("CPU Only", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.CpuOnly));
        Assert.Equal("NVIDIA CUDA", IBackendSelectionService.GetDisplayName(TranscriptionBackendMode.Cuda));
    }
}
