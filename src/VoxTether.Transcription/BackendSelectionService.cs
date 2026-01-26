using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Transcription;

/// <summary>
/// Service for detecting and selecting the best transcription backend.
/// Uses a "try-load" approach for robust detection:
/// - Checks for existence of backend-specific executables
/// - Optionally probes GPU hardware via DXGI for hints
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

        try
        {
            // Try to enumerate GPUs via DXGI
            var gpus = EnumerateGpusViaDxgi();
            diagnostics.DetectedGpus = gpus;

            foreach (var gpu in gpus)
            {
                var lowerGpu = gpu.ToLowerInvariant();
                if (lowerGpu.Contains("nvidia") || lowerGpu.Contains("geforce") || lowerGpu.Contains("quadro") || lowerGpu.Contains("rtx") || lowerGpu.Contains("gtx"))
                {
                    diagnostics.HasNvidiaGpu = true;
                }
                else if (lowerGpu.Contains("intel"))
                {
                    diagnostics.HasIntelGpu = true;
                }
                else if (lowerGpu.Contains("amd") || lowerGpu.Contains("radeon"))
                {
                    diagnostics.HasAmdGpu = true;
                }
            }

            _logger.LogDebug("GPU diagnostics: NVIDIA={HasNvidia}, Intel={HasIntel}, AMD={HasAmd}, GPUs={Gpus}",
                diagnostics.HasNvidiaGpu, diagnostics.HasIntelGpu, diagnostics.HasAmdGpu,
                string.Join(", ", diagnostics.DetectedGpus));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate GPUs via DXGI");
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
    /// Determines the best available backend based on hardware and executable availability.
    /// Order of preference: CUDA > Vulkan > OpenVINO > CPU.
    /// </summary>
    private TranscriptionBackendMode DetermineBestAvailableBackend()
    {
        // Get GPU diagnostics to guide selection
        var gpuInfo = GetGpuDiagnostics();

        // Priority order for acceleration
        var priorityOrder = new List<TranscriptionBackendMode>
        {
            TranscriptionBackendMode.Cuda,
            TranscriptionBackendMode.Vulkan,
            TranscriptionBackendMode.OpenVino,
            TranscriptionBackendMode.CpuOnly
        };

        // If NVIDIA is present, check CUDA first (already at top)
        // If Intel only, prioritize OpenVINO
        if (gpuInfo.HasIntelGpu && !gpuInfo.HasNvidiaGpu && !gpuInfo.HasAmdGpu)
        {
            priorityOrder = new List<TranscriptionBackendMode>
            {
                TranscriptionBackendMode.OpenVino,
                TranscriptionBackendMode.Vulkan,
                TranscriptionBackendMode.CpuOnly
            };
        }

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

    /// <summary>
    /// Enumerates GPU adapters using DXGI.
    /// </summary>
    private static List<string> EnumerateGpusViaDxgi()
    {
        var gpus = new List<string>();

        try
        {
            // Use DXGI to enumerate adapters
            var result = CreateDXGIFactory(typeof(IDXGIFactory).GUID, out var factoryPtr);
            if (result != 0 || factoryPtr == IntPtr.Zero)
            {
                return gpus;
            }

            var factory = (IDXGIFactory)Marshal.GetObjectForIUnknown(factoryPtr);
            
            uint adapterIndex = 0;
            while (true)
            {
                var enumResult = factory.EnumAdapters(adapterIndex, out var adapter);
                if (enumResult != 0)
                    break;

                if (adapter != null)
                {
                    var desc = new DXGI_ADAPTER_DESC();
                    adapter.GetDesc(ref desc);
                    
                    var description = desc.Description;
                    if (!string.IsNullOrWhiteSpace(description) && 
                        !description.Contains("Microsoft Basic Render Driver", StringComparison.OrdinalIgnoreCase))
                    {
                        gpus.Add(description.Trim());
                    }
                    
                    Marshal.ReleaseComObject(adapter);
                }
                
                adapterIndex++;
            }

            Marshal.ReleaseComObject(factory);
        }
        catch
        {
            // DXGI may not be available, return empty list
        }

        return gpus;
    }

    #region DXGI Interop

    [DllImport("dxgi.dll", PreserveSig = true)]
    private static extern int CreateDXGIFactory(in Guid riid, out IntPtr ppFactory);

    [ComImport]
    [Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory
    {
        [PreserveSig]
        int EnumAdapters(uint Adapter, out IDXGIAdapter? ppAdapter);
        
        // Other methods not needed - just declare to maintain vtable order
        void MakeWindowAssociation();
        void GetWindowAssociation();
        void CreateSwapChain();
        void CreateSoftwareAdapter();
    }

    [ComImport]
    [Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter
    {
        // EnumOutputs - not needed
        void EnumOutputs();
        
        [PreserveSig]
        int GetDesc(ref DXGI_ADAPTER_DESC pDesc);
        
        // Other methods not needed
        void CheckInterfaceSupport();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
    }

    #endregion
}
