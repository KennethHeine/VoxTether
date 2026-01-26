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

        // Assert - Should include CPU and CUDA (not Auto)
        Assert.Equal(2, backends.Count);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.CpuOnly);
        Assert.Contains(backends, b => b.Backend == TranscriptionBackendMode.Cuda);
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

    [Fact]
    public void FindsBackendInSubdirectory_Integration()
    {
        // This test verifies that the BackendSelectionService can find executables
        // in subdirectories like whisper/cuda/Release/ which is the structure
        // used by whisper.cpp releases
        
        // Arrange
        var logger = CreateTestLogger();
        // Skip runtime validation since we're using dummy files
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        // Create a temporary directory structure that mimics the whisper.cpp release
        var baseDir = AppContext.BaseDirectory;
        var cudaReleaseDir = Path.Combine(baseDir, "whisper", "cuda", "Release");
        
        try
        {
            Directory.CreateDirectory(cudaReleaseDir);
            var mainExePath = Path.Combine(cudaReleaseDir, "main.exe");
            
            // Create a dummy executable file
            File.WriteAllText(mainExePath, "dummy");
            
            // Act - Check if the backend is now available
            var isAvailable = service.IsBackendAvailable(TranscriptionBackendMode.Cuda);
            var backends = service.GetAvailableBackends();
            var cudaBackend = backends.FirstOrDefault(b => b.Backend == TranscriptionBackendMode.Cuda);
            
            // Assert - The CUDA backend should now be detected as available
            Assert.True(isAvailable, "CUDA backend should be available when main.exe is in whisper/cuda/Release/");
            Assert.NotNull(cudaBackend);
            Assert.True(cudaBackend.IsAvailable);
            Assert.NotNull(cudaBackend.ExecutablePath);
            Assert.Contains("Release", cudaBackend.ExecutablePath);
            Assert.EndsWith("main.exe", cudaBackend.ExecutablePath);
        }
        finally
        {
            CleanupTestBackendDirectory(baseDir);
        }
    }

    [Fact]
    public void FindsWhisperCliExe_Integration()
    {
        // Test that whisper-cli.exe is also found as a valid executable
        
        // Arrange
        var logger = CreateTestLogger();
        // Skip runtime validation since we're using dummy files
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var baseDir = AppContext.BaseDirectory;
        var cudaReleaseDir = Path.Combine(baseDir, "whisper", "cuda", "Release");
        
        try
        {
            Directory.CreateDirectory(cudaReleaseDir);
            var whisperCliPath = Path.Combine(cudaReleaseDir, "whisper-cli.exe");
            
            // Create a dummy executable file
            File.WriteAllText(whisperCliPath, "dummy");
            
            // Act
            var isAvailable = service.IsBackendAvailable(TranscriptionBackendMode.Cuda);
            var backends = service.GetAvailableBackends();
            var cudaBackend = backends.FirstOrDefault(b => b.Backend == TranscriptionBackendMode.Cuda);
            
            // Assert
            Assert.True(isAvailable, "CUDA backend should be available when whisper-cli.exe is in whisper/cuda/Release/");
            Assert.NotNull(cudaBackend);
            Assert.True(cudaBackend.IsAvailable);
        }
        finally
        {
            CleanupTestBackendDirectory(baseDir);
        }
    }

    [Fact]
    public void PrefersWhisperCliExeOverMainExe_Integration()
    {
        // Test that whisper-cli.exe is preferred over main.exe (main.exe is deprecated)
        
        // Arrange
        var logger = CreateTestLogger();
        // Skip runtime validation since we're using dummy files
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var baseDir = AppContext.BaseDirectory;
        var cudaReleaseDir = Path.Combine(baseDir, "whisper", "cuda", "Release");
        
        try
        {
            Directory.CreateDirectory(cudaReleaseDir);
            
            // Create both executables
            var mainExePath = Path.Combine(cudaReleaseDir, "main.exe");
            var whisperCliPath = Path.Combine(cudaReleaseDir, "whisper-cli.exe");
            File.WriteAllText(mainExePath, "dummy-main");
            File.WriteAllText(whisperCliPath, "dummy-whisper-cli");
            
            // Act
            var backends = service.GetAvailableBackends();
            var cudaBackend = backends.FirstOrDefault(b => b.Backend == TranscriptionBackendMode.Cuda);
            
            // Assert - whisper-cli.exe should be preferred over main.exe
            Assert.NotNull(cudaBackend);
            Assert.True(cudaBackend.IsAvailable);
            Assert.NotNull(cudaBackend.ExecutablePath);
            Assert.EndsWith("whisper-cli.exe", cudaBackend.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTestBackendDirectory(baseDir);
        }
    }

    /// <summary>
    /// Cleans up the test CUDA backend directory.
    /// </summary>
    private static void CleanupTestBackendDirectory(string baseDir)
    {
        var cudaDir = Path.Combine(baseDir, "whisper", "cuda");
        if (Directory.Exists(cudaDir))
        {
            Directory.Delete(cudaDir, true);
        }
    }

    [Fact]
    public void RequiredCudaDlls_ContainsExpectedDlls()
    {
        // Assert - Verify the CUDA DLL list contains expected files for CUDA 11.8
        var dlls = BackendSelectionService.RequiredCudaDlls;
        Assert.NotNull(dlls);
        Assert.Equal(3, dlls.Length);
        Assert.Contains("cublas64_11.dll", dlls);
        Assert.Contains("cublasLt64_11.dll", dlls);
        Assert.Contains("cudart64_110.dll", dlls);
    }

    [Fact]
    public void AreCudaDllsInDirectory_EmptyDirectory_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "VoxTetherTest_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Act
            var result = BackendSelectionService.AreCudaDllsInDirectory(tempDir);
            
            // Assert
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreCudaDllsInDirectory_WithAllDlls_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "VoxTetherTest_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Create all required CUDA DLLs
            foreach (var dll in BackendSelectionService.RequiredCudaDlls)
            {
                File.WriteAllText(Path.Combine(tempDir, dll), "dummy");
            }
            
            // Act
            var result = BackendSelectionService.AreCudaDllsInDirectory(tempDir);
            
            // Assert
            Assert.True(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreCudaDllsInDirectory_WithPartialDlls_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "VoxTetherTest_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Create only one of the required DLLs
            File.WriteAllText(Path.Combine(tempDir, "cublas64_11.dll"), "dummy");
            
            // Act
            var result = BackendSelectionService.AreCudaDllsInDirectory(tempDir);
            
            // Assert
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreCudaDllsInDirectory_NullOrEmptyPath_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(BackendSelectionService.AreCudaDllsInDirectory(null!));
        Assert.False(BackendSelectionService.AreCudaDllsInDirectory(string.Empty));
        Assert.False(BackendSelectionService.AreCudaDllsInDirectory("/nonexistent/path"));
    }

    [Fact]
    public void GetCudaExecutableDirectory_ValidPath_ReturnsDirectory()
    {
        // Arrange
        var testPath = @"C:\VoxTether\whisper\cuda\Release\whisper-cli.exe";
        
        // Act
        var result = BackendSelectionService.GetCudaExecutableDirectory(testPath);
        
        // Assert
        Assert.Equal(@"C:\VoxTether\whisper\cuda\Release", result);
    }

    [Fact]
    public void GetCudaExecutableDirectory_NullOrEmptyPath_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(BackendSelectionService.GetCudaExecutableDirectory(null));
        Assert.Null(BackendSelectionService.GetCudaExecutableDirectory(string.Empty));
    }

    [Fact]
    public void AreCudaDllsAvailable_WithDllsNextToExe_ReturnsTrue()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var tempDir = Path.Combine(Path.GetTempPath(), "VoxTetherTest_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Create a dummy executable and all required CUDA DLLs
            var exePath = Path.Combine(tempDir, "whisper-cli.exe");
            File.WriteAllText(exePath, "dummy");
            
            foreach (var dll in BackendSelectionService.RequiredCudaDlls)
            {
                File.WriteAllText(Path.Combine(tempDir, dll), "dummy");
            }
            
            // Act
            var result = service.AreCudaDllsAvailable(exePath);
            
            // Assert
            Assert.True(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreCudaDllsAvailable_WithNoDlls_ReturnsFalse()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var tempDir = Path.Combine(Path.GetTempPath(), "VoxTetherTest_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Create only a dummy executable (no DLLs)
            var exePath = Path.Combine(tempDir, "whisper-cli.exe");
            File.WriteAllText(exePath, "dummy");
            
            // Act
            var result = service.AreCudaDllsAvailable(exePath);
            
            // Assert
            // This will be false unless the DLLs are in PATH (unlikely in test environment)
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CudaBackend_WithMissingDlls_ReportsUnavailableReason()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var baseDir = AppContext.BaseDirectory;
        var cudaReleaseDir = Path.Combine(baseDir, "whisper", "cuda", "Release");
        
        try
        {
            Directory.CreateDirectory(cudaReleaseDir);
            
            // Create executable but NO CUDA DLLs
            var exePath = Path.Combine(cudaReleaseDir, "whisper-cli.exe");
            File.WriteAllText(exePath, "dummy");
            
            // Act
            var backends = service.GetAvailableBackends();
            var cudaBackend = backends.FirstOrDefault(b => b.Backend == TranscriptionBackendMode.Cuda);
            
            // Assert
            Assert.NotNull(cudaBackend);
            Assert.NotNull(cudaBackend.ExecutablePath);
            Assert.False(cudaBackend.IsAvailable);
            Assert.NotNull(cudaBackend.UnavailableReason);
            Assert.Contains("Missing CUDA 11.8 runtime DLLs", cudaBackend.UnavailableReason);
            Assert.Contains("Get CUDA DLLs", cudaBackend.UnavailableReason);
        }
        finally
        {
            CleanupTestBackendDirectory(baseDir);
        }
    }

    [Fact]
    public void CudaBackend_WithAllDlls_IsAvailable()
    {
        // Arrange
        var logger = CreateTestLogger();
        var service = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var baseDir = AppContext.BaseDirectory;
        var cudaReleaseDir = Path.Combine(baseDir, "whisper", "cuda", "Release");
        
        try
        {
            Directory.CreateDirectory(cudaReleaseDir);
            
            // Create executable AND all CUDA DLLs
            var exePath = Path.Combine(cudaReleaseDir, "whisper-cli.exe");
            File.WriteAllText(exePath, "dummy");
            
            foreach (var dll in BackendSelectionService.RequiredCudaDlls)
            {
                File.WriteAllText(Path.Combine(cudaReleaseDir, dll), "dummy");
            }
            
            // Act
            var backends = service.GetAvailableBackends();
            var cudaBackend = backends.FirstOrDefault(b => b.Backend == TranscriptionBackendMode.Cuda);
            
            // Assert
            Assert.NotNull(cudaBackend);
            Assert.NotNull(cudaBackend.ExecutablePath);
            Assert.True(cudaBackend.IsAvailable);
            Assert.Null(cudaBackend.UnavailableReason);
        }
        finally
        {
            CleanupTestBackendDirectory(baseDir);
        }
    }
}
