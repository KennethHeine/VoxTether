namespace VoxTether.Core.Models;

/// <summary>
/// Specifies the transcription backend mode for whisper.cpp.
/// </summary>
public enum TranscriptionBackendMode
{
    /// <summary>
    /// Automatically select the best available backend based on hardware detection.
    /// Order of preference: CUDA > CPU.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Use CPU-only backend (baseline, always available).
    /// </summary>
    CpuOnly = 1,

    /// <summary>
    /// Force NVIDIA CUDA backend (fastest if NVIDIA GPU present).
    /// </summary>
    Cuda = 2
}
