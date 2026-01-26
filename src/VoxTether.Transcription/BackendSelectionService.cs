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
        [TranscriptionBackendMode.Vulkan] = ["whisper_vulkan.exe", "vulkan/main.exe", "vulkan/whisper.exe"],
        [TranscriptionBackendMode.OpenVino] = ["whisper_openvino.exe", "openvino/main.exe", "openvino/whisper.exe"],
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
            // Check for NVIDIA driver hint via CUDA availability
            if (IsBackendAvailable(TranscriptionBackendMode.Cuda))
            {
                diagnostics.HasNvidiaGpu = true;
                diagnostics.DetectedGpus.Add("NVIDIA GPU (inferred from CUDA backend availability)");
            }

            // Check for Intel OpenVINO availability
            if (IsBackendAvailable(TranscriptionBackendMode.OpenVino))
            {
                diagnostics.HasIntelGpu = true;
                diagnostics.DetectedGpus.Add("Intel GPU/NPU (inferred from OpenVINO backend availability)");
            }

            // Check for Vulkan availability (cross-vendor)
            if (IsBackendAvailable(TranscriptionBackendMode.Vulkan))
            {
                // Vulkan could be AMD, NVIDIA, or Intel - we can't tell without DXGI
                diagnostics.DetectedGpus.Add("Vulkan-capable GPU (inferred from Vulkan backend availability)");
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
    /// Order of preference: CUDA > Vulkan > OpenVINO > CPU.
    /// Uses a simple "try-load" approach by checking for backend executables.
    /// </summary>
    private TranscriptionBackendMode DetermineBestAvailableBackend()
    {
        // Standard priority order for acceleration: CUDA > Vulkan > OpenVINO > CPU
        // This order is based on typical performance characteristics.
        var priorityOrder = new List<TranscriptionBackendMode>
        {
            TranscriptionBackendMode.Cuda,
            TranscriptionBackendMode.Vulkan,
            TranscriptionBackendMode.OpenVino,
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

        _logger.LogDebug("No executable found for backend {Backend}", backend);
        return null;
    }
}
