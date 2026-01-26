using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxTether.Core.Models;

namespace VoxTether.Core.Interfaces;

/// <summary>
/// Service for downloading and managing backend packages.
/// </summary>
public interface IBackendDownloadService
{
    /// <summary>
    /// Gets the manifest of available backends for download.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Backend manifest.</returns>
    Task<BackendManifest> GetManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and installs a backend package.
    /// </summary>
    /// <param name="backendId">Backend identifier (cuda, vulkan, openvino).</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if download and installation succeeded.</returns>
    Task<bool> DownloadBackendAsync(
        string backendId, 
        IProgress<BackendDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an installed backend to free disk space.
    /// </summary>
    /// <param name="backendId">Backend identifier to remove.</param>
    /// <returns>True if removal succeeded.</returns>
    Task<bool> RemoveBackendAsync(string backendId);

    /// <summary>
    /// Gets recommended backends based on detected hardware.
    /// </summary>
    /// <returns>List of recommended backend IDs.</returns>
    List<string> GetRecommendedBackends();

    /// <summary>
    /// Checks if a backend is already installed.
    /// </summary>
    /// <param name="backendId">Backend identifier.</param>
    /// <returns>True if the backend is installed.</returns>
    bool IsBackendInstalled(string backendId);

    /// <summary>
    /// Gets available disk space in bytes at the installation location.
    /// </summary>
    /// <returns>Available disk space in bytes.</returns>
    long GetAvailableDiskSpace();

    /// <summary>
    /// Downloads and installs CUDA runtime DLLs from NVIDIA's redistribution site.
    /// This allows the CUDA backend to run without requiring users to install the full CUDA Toolkit.
    /// Downloads cublas64_11.dll, cublasLt64_11.dll, and cudart64_110.dll.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if download and installation succeeded.</returns>
    Task<bool> DownloadCudaDllsAsync(
        IProgress<BackendDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the CUDA runtime DLLs are installed in the CUDA backend directory.
    /// </summary>
    /// <returns>True if all required CUDA DLLs are present.</returns>
    bool AreCudaDllsInstalled();
}
