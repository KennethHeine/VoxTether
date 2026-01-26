namespace VoxTether.Core.Models;

/// <summary>
/// Specifies the transcription backend mode for whisper.cpp.
/// </summary>
public enum TranscriptionBackendMode
{
    /// <summary>
    /// Automatically select the best available backend based on hardware detection.
    /// Order of preference: CUDA > Vulkan > OpenVINO > CPU.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Use CPU-only backend (baseline, always available).
    /// </summary>
    CpuOnly = 1,

    /// <summary>
    /// Force NVIDIA CUDA backend (fastest if NVIDIA GPU present).
    /// </summary>
    Cuda = 2,

    /// <summary>
    /// Force Vulkan backend (cross-vendor GPU acceleration).
    /// </summary>
    Vulkan = 3,

    /// <summary>
    /// Force Intel OpenVINO backend (Intel NPU/iGPU acceleration).
    /// </summary>
    OpenVino = 4
}
