using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Transcription;

/// <summary>
/// Service for detecting and selecting the best transcription backend.
/// Uses a "try-load" approach for robust detection:
/// - Checks for existence of backend-specific executables
/// - Falls back gracefully to CPU if requested backend is unavailable
/// </summary>
public class BackendSelectionService : IBackendSelectionService
{
    private readonly ILogger<BackendSelectionService> _logger;
    private readonly object _lock = new();
    
    private TranscriptionBackendMode _activeBackend = TranscriptionBackendMode.CpuOnly;
    private string? _activeWhisperPath;
    private bool _initialized;
    private bool _fellBackToCpu;
    private TranscriptionBackendMode? _requestedBackend;
    private GpuDiagnostics? _cachedGpuDiagnostics;

    // Executable name patterns for each backend
    // The whisper folder structure: whisper/<backend>/main.exe or whisper/whisper_<backend>.exe
    private static readonly Dictionary<TranscriptionBackendMode, string[]> BackendExecutablePatterns = new()
    {
        [TranscriptionBackendMode.CpuOnly] = ["whisper_cpu.exe", "cpu/main.exe", "cpu/whisper.exe", "main.exe", "whisper.exe"],
        [TranscriptionBackendMode.Cuda] = ["whisper_cuda.exe", "cuda/main.exe", "cuda/whisper.exe"],
    };

    public BackendSelectionService(ILogger<BackendSelectionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TranscriptionBackendMode ActiveBackend => _activeBackend;

    /// <inheritdoc />
    public string? ActiveWhisperPath => _activeWhisperPath;

    /// <inheritdoc />
    public bool FellBackToCpu => _fellBackToCpu;

    /// <inheritdoc />
    public TranscriptionBackendMode? RequestedBackend => _requestedBackend;

    /// <inheritdoc />
    public TranscriptionBackendMode DetermineBackend(TranscriptionBackendMode requestedMode)
    {
        lock (_lock)
        {
            if (_initialized)
            {
                _logger.LogDebug("Backend already determined: {Backend}", _activeBackend);
                return _activeBackend;
            }

            _logger.LogInformation("Determining transcription backend. Requested mode: {RequestedMode}", requestedMode);

            // Store original request for diagnostics
            _requestedBackend = requestedMode;
            _fellBackToCpu = false;

            // Determine actual backend based on request
            TranscriptionBackendMode resolvedBackend;

            if (requestedMode == TranscriptionBackendMode.Auto)
            {
                resolvedBackend = DetermineBestAvailableBackend();
                _logger.LogInformation("Auto mode selected: {Backend}", resolvedBackend);
            }
            else if (requestedMode == TranscriptionBackendMode.CpuOnly)
            {
                resolvedBackend = TranscriptionBackendMode.CpuOnly;
                _logger.LogInformation("CPU-only mode requested");
            }
            else
            {
                // Specific backend requested - try to use it, fall back to CPU if unavailable
                if (IsBackendAvailable(requestedMode))
                {
                    resolvedBackend = requestedMode;
                    _logger.LogInformation("Requested backend {Backend} is available", requestedMode);
                }
                else
                {
                    _logger.LogWarning("Requested backend {RequestedBackend} is not available, falling back to CPU", requestedMode);
                    resolvedBackend = TranscriptionBackendMode.CpuOnly;
                    _fellBackToCpu = true;
                }
            }

            // Find and store the executable path
            _activeWhisperPath = FindExecutableForBackend(resolvedBackend);
            _activeBackend = resolvedBackend;
            _initialized = true;

            _logger.LogInformation("Backend selection complete. Active: {Backend}, Path: {Path}", 
                _activeBackend, _activeWhisperPath ?? "(not found)");

            return _activeBackend;
        }
    }

    /// <inheritdoc />
    public List<BackendInfo> GetAvailableBackends()
    {
        var backends = new List<BackendInfo>();

        foreach (TranscriptionBackendMode backend in Enum.GetValues<TranscriptionBackendMode>())
        {
            if (backend == TranscriptionBackendMode.Auto)
                continue;

            var execPath = FindExecutableForBackend(backend);
            var isAvailable = !string.IsNullOrEmpty(execPath);

            backends.Add(new BackendInfo
            {
                Backend = backend,
                IsAvailable = isAvailable,
                ExecutablePath = execPath,
                UnavailableReason = isAvailable ? null : "Executable not found"
            });
        }

        return backends;
    }

    /// <inheritdoc />
    public GpuDiagnostics GetGpuDiagnostics()
    {
        if (_cachedGpuDiagnostics != null)
            return _cachedGpuDiagnostics;

        var diagnostics = new GpuDiagnostics();

        // Instead of using fragile DXGI COM interop which can crash on some systems,
        // we use a simpler approach: infer GPU presence from available backend executables
        // and environment. This follows the "try-load" robustness principle.
        try
        {
            // First try actual hardware detection
            DetectHardware(diagnostics);

            // Also check for backend availability (for backwards compatibility)
            if (IsBackendAvailable(TranscriptionBackendMode.Cuda))
            {
                if (!diagnostics.HasNvidiaGpu)
                {
                    diagnostics.HasNvidiaGpu = true;
                    diagnostics.DetectedGpus.Add("NVIDIA GPU (inferred from CUDA backend availability)");
                }
            }

            // If no accelerated backends found, just note that CPU is available
            if (diagnostics.DetectedGpus.Count == 0)
            {
                diagnostics.DetectedGpus.Add("(No GPU-accelerated backends detected)");
            }

            _logger.LogDebug("GPU diagnostics: NVIDIA={HasNvidia}, Intel={HasIntel}, AMD={HasAmd}",
                diagnostics.HasNvidiaGpu, diagnostics.HasIntelGpu, diagnostics.HasAmdGpu);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather GPU diagnostics");
            diagnostics.DetectedGpus = ["(Detection failed)"];
        }

        _cachedGpuDiagnostics = diagnostics;
        return diagnostics;
    }

    /// <summary>
    /// Detects GPU hardware by checking for vendor-specific drivers and environment hints.
    /// This is a simple heuristic-based approach that avoids fragile COM interop.
    /// </summary>
    private void DetectHardware(GpuDiagnostics diagnostics)
    {
        try
        {
            // Check for NVIDIA GPU by looking for common NVIDIA environment variables/paths
            var nvidiaPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvcuda.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll")
            };

            if (nvidiaPaths.Any(p => Directory.Exists(p) || File.Exists(p)))
            {
                diagnostics.HasNvidiaGpu = true;
                diagnostics.DetectedGpus.Add("NVIDIA GPU (detected from driver files)");
                _logger.LogDebug("Detected NVIDIA GPU from driver files");
            }

            // Check for Intel GPU/CPU
            var processorName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "";
            if (processorName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.HasIntelGpu = true;
                diagnostics.DetectedGpus.Add("Intel CPU/GPU (detected from processor)");
                _logger.LogDebug("Detected Intel hardware");
            }

            // Check for AMD GPU by looking for AMD paths
            var amdPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AMD"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "amdvlk64.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "atiadlxx.dll")
            };

            if (amdPaths.Any(p => Directory.Exists(p) || File.Exists(p)))
            {
                diagnostics.HasAmdGpu = true;
                diagnostics.DetectedGpus.Add("AMD GPU (detected from driver files)");
                _logger.LogDebug("Detected AMD GPU from driver files");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during hardware detection, will rely on backend availability");
        }
    }

    /// <inheritdoc />
    public bool IsBackendAvailable(TranscriptionBackendMode backend)
    {
        if (backend == TranscriptionBackendMode.Auto)
            return true;

        var execPath = FindExecutableForBackend(backend);
        return !string.IsNullOrEmpty(execPath);
    }

    /// <summary>
    /// Determines the best available backend based on executable availability.
    /// Order of preference: CUDA > CPU.
    /// Uses a simple "try-load" approach by checking for backend executables.
    /// </summary>
    private TranscriptionBackendMode DetermineBestAvailableBackend()
    {
        // Standard priority order for acceleration: CUDA > CPU
        // This order is based on typical performance characteristics.
        var priorityOrder = new List<TranscriptionBackendMode>
        {
            TranscriptionBackendMode.Cuda,
            TranscriptionBackendMode.CpuOnly
        };

        foreach (var backend in priorityOrder)
        {
            if (IsBackendAvailable(backend))
            {
                _logger.LogDebug("Auto-selected backend: {Backend}", backend);
                return backend;
            }
            else
            {
                _logger.LogDebug("Backend {Backend} not available, trying next", backend);
            }
        }

        // Fallback to CPU (should always be available)
        return TranscriptionBackendMode.CpuOnly;
    }

    /// <summary>
    /// Finds the executable path for a specific backend.
    /// Checks multiple possible locations and naming patterns.
    /// Also searches subdirectories (e.g., "Release/") for whisper.cpp distribution compatibility.
    /// </summary>
    private string? FindExecutableForBackend(TranscriptionBackendMode backend)
    {
        if (!BackendExecutablePatterns.TryGetValue(backend, out var patterns))
        {
            // For Auto mode, this shouldn't be called directly
            return null;
        }

        var baseDir = AppContext.BaseDirectory;
        var whisperDir = Path.Combine(baseDir, "whisper");

        // Check each pattern in order of preference
        foreach (var pattern in patterns)
        {
            // Check in whisper subdirectory first
            var whisperPath = Path.Combine(whisperDir, pattern);
            if (File.Exists(whisperPath))
            {
                _logger.LogDebug("Found {Backend} executable at: {Path}", backend, whisperPath);
                return whisperPath;
            }

            // Check in base directory
            var basePath = Path.Combine(baseDir, pattern);
            if (File.Exists(basePath))
            {
                _logger.LogDebug("Found {Backend} executable at: {Path}", backend, basePath);
                return basePath;
            }
        }

        // Search in backend-specific subdirectories (e.g., whisper/cuda/Release/)
        // This handles whisper.cpp release zip structure which extracts to a "Release" subfolder
        var backendSubdir = GetBackendSubdirectory(backend);
        if (!string.IsNullOrEmpty(backendSubdir))
        {
            var backendDir = Path.Combine(whisperDir, backendSubdir);
            if (Directory.Exists(backendDir))
            {
                var foundPath = SearchDirectoryForExecutable(backendDir, backend);
                if (foundPath != null)
                {
                    return foundPath;
                }
            }
        }

        _logger.LogDebug("No executable found for backend {Backend}", backend);
        return null;
    }

    /// <summary>
    /// Gets the subdirectory name for a backend (e.g., "cuda" for CUDA backend).
    /// </summary>
    private static string? GetBackendSubdirectory(TranscriptionBackendMode backend)
    {
        return backend switch
        {
            TranscriptionBackendMode.Cuda => "cuda",
            TranscriptionBackendMode.CpuOnly => "cpu",
            _ => null
        };
    }

    /// <summary>
    /// Searches a directory and its subdirectories for a whisper executable.
    /// Uses executable names from BackendExecutablePatterns plus common whisper.cpp release names.
    /// </summary>
    private string? SearchDirectoryForExecutable(string directory, TranscriptionBackendMode backend)
    {
        try
        {
            // Get executable names from patterns, extracting just the filename part
            var preferredNames = GetPreferredExecutableNames(backend);

            // Search in immediate subdirectories (e.g., "Release/")
            foreach (var subdir in Directory.GetDirectories(directory))
            {
                foreach (var preferredName in preferredNames)
                {
                    var execPath = Path.Combine(subdir, preferredName);
                    if (File.Exists(execPath))
                    {
                        _logger.LogDebug("Found {Backend} executable at: {Path}", backend, execPath);
                        return execPath;
                    }
                }
            }

            // Also check the directory itself
            foreach (var preferredName in preferredNames)
            {
                var execPath = Path.Combine(directory, preferredName);
                if (File.Exists(execPath))
                {
                    _logger.LogDebug("Found {Backend} executable at: {Path}", backend, execPath);
                    return execPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error searching for executable in {Directory}", directory);
        }

        return null;
    }

    /// <summary>
    /// Gets the preferred executable names for a backend, extracted from patterns.
    /// Also includes whisper-cli.exe which is used in newer whisper.cpp releases.
    /// </summary>
    private static string[] GetPreferredExecutableNames(TranscriptionBackendMode backend)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract filenames from existing patterns
        if (BackendExecutablePatterns.TryGetValue(backend, out var patterns))
        {
            foreach (var pattern in patterns)
            {
                // Extract just the filename from patterns like "cuda/main.exe"
                var filename = Path.GetFileName(pattern);
                names.Add(filename);
            }
        }

        // Add common whisper.cpp release executable names
        // whisper-cli.exe is used in newer whisper.cpp releases
        names.Add("main.exe");
        names.Add("whisper-cli.exe");
        names.Add("whisper.exe");

        return names.ToArray();
    }
}
