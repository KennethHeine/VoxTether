using VoxTether.Core.Models;

namespace VoxTether.Core.Interfaces;

/// <summary>
/// Information about a detected transcription backend.
/// </summary>
public class BackendInfo
{
    /// <summary>
    /// The backend type.
    /// </summary>
    public TranscriptionBackendMode Backend { get; set; }

    /// <summary>
    /// Whether this backend is available on the current system.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Path to the executable for this backend.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Reason if not available.
    /// </summary>
    public string? UnavailableReason { get; set; }
}

/// <summary>
/// Diagnostics information about detected GPU hardware.
/// </summary>
public class GpuDiagnostics
{
    /// <summary>
    /// List of detected GPU adapters with vendor information.
    /// </summary>
    public List<string> DetectedGpus { get; set; } = [];

    /// <summary>
    /// Whether an NVIDIA GPU was detected.
    /// </summary>
    public bool HasNvidiaGpu { get; set; }

    /// <summary>
    /// Whether an Intel GPU was detected.
    /// </summary>
    public bool HasIntelGpu { get; set; }

    /// <summary>
    /// Whether an AMD GPU was detected.
    /// </summary>
    public bool HasAmdGpu { get; set; }
}

/// <summary>
/// Service for detecting and selecting the best transcription backend.
/// </summary>
public interface IBackendSelectionService
{
    /// <summary>
    /// Gets the currently active backend.
    /// </summary>
    TranscriptionBackendMode ActiveBackend { get; }

    /// <summary>
    /// Gets the path to the whisper executable for the active backend.
    /// </summary>
    string? ActiveWhisperPath { get; }

    /// <summary>
    /// Gets whether a specific backend was requested but unavailable, 
    /// resulting in fallback to CPU.
    /// </summary>
    bool FellBackToCpu { get; }

    /// <summary>
    /// Gets the original requested backend mode if fallback occurred.
    /// </summary>
    TranscriptionBackendMode? RequestedBackend { get; }

    /// <summary>
    /// Determines the best available backend based on the requested mode 
    /// and available hardware/executables.
    /// This should be called once at startup before any transcription occurs.
    /// </summary>
    /// <param name="requestedMode">The user's requested backend mode.</param>
    /// <returns>The backend that will actually be used.</returns>
    TranscriptionBackendMode DetermineBackend(TranscriptionBackendMode requestedMode);

    /// <summary>
    /// Gets information about all available backends.
    /// </summary>
    /// <returns>List of backend information for all supported backends.</returns>
    List<BackendInfo> GetAvailableBackends();

    /// <summary>
    /// Gets GPU diagnostics information.
    /// </summary>
    /// <returns>Information about detected GPUs.</returns>
    GpuDiagnostics GetGpuDiagnostics();

    /// <summary>
    /// Checks if a specific backend is available.
    /// </summary>
    /// <param name="backend">The backend to check.</param>
    /// <returns>True if the backend is available.</returns>
    bool IsBackendAvailable(TranscriptionBackendMode backend);

    /// <summary>
    /// Gets a human-readable display name for the backend.
    /// </summary>
    /// <param name="backend">The backend mode.</param>
    /// <returns>Display name.</returns>
    static string GetDisplayName(TranscriptionBackendMode backend) => backend switch
    {
        TranscriptionBackendMode.Auto => "Auto",
        TranscriptionBackendMode.CpuOnly => "CPU Only",
        TranscriptionBackendMode.Cuda => "NVIDIA CUDA",
        TranscriptionBackendMode.Vulkan => "Vulkan",
        TranscriptionBackendMode.OpenVino => "Intel OpenVINO",
        _ => backend.ToString()
    };
}
