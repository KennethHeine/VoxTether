using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Services;

namespace VoxTether.Transcription;

/// <summary>
/// Service for downloading and managing transcription backend packages.
/// </summary>
public class BackendDownloadService : IBackendDownloadService, IDisposable
{
    private readonly ILogger<BackendDownloadService> _logger;
    private readonly IBackendSelectionService _backendSelection;
    private readonly HttpClient _httpClient;
    private readonly string _whisperDirectory;

    // Embedded manifest as fallback
    // Note: Only CUDA is available as a pre-built binary from ggml-org/whisper.cpp
    // Vulkan and OpenVINO require compilation from source and are not offered for download
    // CUDA 12.4 build is self-contained (bundles all required DLLs) - no separate CUDA Toolkit needed!
    private const string DefaultManifestJson = @"{
  ""version"": ""1.0"",
  ""backends"": [
    {
      ""id"": ""cuda"",
      ""name"": ""NVIDIA CUDA"",
      ""description"": ""GPU acceleration for NVIDIA graphics cards. Self-contained build with all required DLLs included."",
      ""downloadUrl"": ""https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.3/whisper-cublas-12.4.0-bin-x64.zip"",
      ""size"": 459854042,
      ""checksum"": ""sha256:c12a563333d3c3707be70754dc0e87c1cb58aa6333a87055bbcf9b524488dfb0"",
      ""requirements"": ""NVIDIA GPU with CUDA support and up-to-date drivers (version 525.60+ recommended)""
    }
  ]
}";

    // CUDA 12.4 build is self-contained - all DLLs are bundled in the whisper.cpp release
    // No separate downloads from NVIDIA redistribution site needed!
    // These constants are kept for reference/fallback but normally not used
    private const string CudaRuntimeVersion = "12.4.127";
    private const string CublasVersion = "12.4.5.8";
    
    private static readonly string CudaRuntimeUrl = $"https://developer.download.nvidia.com/compute/cuda/redist/cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-{CudaRuntimeVersion}-archive.zip";
    private const long CudaRuntimeSize = 3_000_000; // ~3MB
    
    private static readonly string CublasUrl = $"https://developer.download.nvidia.com/compute/cuda/redist/libcublas/windows-x86_64/libcublas-windows-x86_64-{CublasVersion}-archive.zip";
    private const long CublasSize = 500_000_000; // ~500MB
    
    // Required DLL files for CUDA 12 backend (bundled in the whisper.cpp CUDA 12.4 release)
    private static readonly string[] RequiredCudaDlls = ["cublas64_12.dll", "cublasLt64_12.dll", "cudart64_12.dll"];

    public BackendDownloadService(
        ILogger<BackendDownloadService> logger,
        IBackendSelectionService backendSelection)
    {
        _logger = logger;
        _backendSelection = backendSelection;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30) // Allow long downloads
        };

        var baseDir = AppContext.BaseDirectory;
        _whisperDirectory = Path.Combine(baseDir, "whisper");
        
        try
        {
            Directory.CreateDirectory(_whisperDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create whisper directory in constructor. Will retry on demand.");
        }
    }

    /// <inheritdoc />
    public async Task<BackendManifest> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // For now, use the embedded manifest
            // In the future, this could fetch from a remote URL with fallback to embedded
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var manifest = JsonSerializer.Deserialize<BackendManifest>(DefaultManifestJson, options);
            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to parse backend manifest");
            }

            _logger.LogDebug("Loaded backend manifest with {Count} backends", manifest.Backends.Count);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backend manifest");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DownloadBackendAsync(
        string backendId,
        IProgress<BackendDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting download for backend: {BackendId}", backendId);

            // Get manifest
            var manifest = await GetManifestAsync(cancellationToken);
            var packageInfo = manifest.Backends.FirstOrDefault(b => 
                b.Id.Equals(backendId, StringComparison.OrdinalIgnoreCase));

            if (packageInfo == null)
            {
                _logger.LogError("Backend {BackendId} not found in manifest", backendId);
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed, 
                    0, 0, $"Backend '{backendId}' not found", "Backend not found in manifest");
                return false;
            }

            // Check disk space
            var availableSpace = GetAvailableDiskSpace();
            if (availableSpace < packageInfo.Size * 2) // Need 2x space for zip + extraction
            {
                var errorMsg = $"Insufficient disk space. Need {FormatUtility.FormatBytes(packageInfo.Size * 2)}, " +
                              $"have {FormatUtility.FormatBytes(availableSpace)}";
                _logger.LogError(errorMsg);
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed, 
                    0, 0, "Insufficient disk space", errorMsg);
                return false;
            }

            // Create temp download path
            var tempDir = Path.Combine(Path.GetTempPath(), "VoxTether", "downloads");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"{backendId}.zip");

            // Download the file
            ReportProgress(progress, backendId, BackendDownloadStatus.Downloading, 
                0, packageInfo.Size, "Starting download...");

            using (var response = await _httpClient.GetAsync(packageInfo.DownloadUrl, 
                HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? packageInfo.Size;
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                var buffer = new byte[8192];
                long bytesRead = 0;
                int lastReportedPercent = -1;

                while (true)
                {
                    var read = await downloadStream.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    bytesRead += read;

                    // Report progress every 1%
                    var percent = (int)((bytesRead * 100) / totalBytes);
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        ReportProgress(progress, backendId, BackendDownloadStatus.Downloading,
                            bytesRead, totalBytes, 
                            $"Downloading... {FormatUtility.FormatBytes(bytesRead)} / {FormatUtility.FormatBytes(totalBytes)}");
                    }
                }
            }

            _logger.LogInformation("Download completed for {BackendId}, {Size} bytes", 
                backendId, new FileInfo(zipPath).Length);

            // Validate checksum (skip if exactly "sha256:pending" placeholder)
            if (!packageInfo.Checksum.Equals("sha256:pending", StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Validating,
                    0, 0, "Validating checksum...");

                if (!await ValidateChecksumAsync(zipPath, packageInfo.Checksum, cancellationToken))
                {
                    _logger.LogError("Checksum validation failed for {BackendId}", backendId);
                    ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                        0, 0, "Checksum validation failed", "Downloaded file is corrupted");
                    File.Delete(zipPath);
                    return false;
                }
            }

            // Extract to backend folder
            var backendDir = Path.Combine(_whisperDirectory, backendId);
            ReportProgress(progress, backendId, BackendDownloadStatus.Extracting,
                0, 0, "Extracting files...");

            // Remove existing backend folder if present
            if (Directory.Exists(backendDir))
            {
                Directory.Delete(backendDir, true);
            }

            Directory.CreateDirectory(backendDir);
            ZipFile.ExtractToDirectory(zipPath, backendDir, true);

            // Clean up temp file
            File.Delete(zipPath);

            _logger.LogInformation("Backend {BackendId} installed successfully to {Path}", 
                backendId, backendDir);

            ReportProgress(progress, backendId, BackendDownloadStatus.Completed,
                0, 0, "Installation complete");

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Download cancelled for backend: {BackendId}", backendId);
            ReportProgress(progress, backendId, BackendDownloadStatus.Cancelled,
                0, 0, "Download cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download backend: {BackendId}", backendId);
            ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                0, 0, "Download failed", ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveBackendAsync(string backendId)
    {
        try
        {
            var backendDir = Path.Combine(_whisperDirectory, backendId);
            if (Directory.Exists(backendDir))
            {
                Directory.Delete(backendDir, true);
                _logger.LogInformation("Removed backend: {BackendId} from {Path}", backendId, backendDir);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Backend {BackendId} not found for removal", backendId);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove backend: {BackendId}", backendId);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public List<string> GetRecommendedBackends()
    {
        var recommended = new List<string>();
        var gpuDiagnostics = _backendSelection.GetGpuDiagnostics();

        // Only CUDA is available as a downloadable pre-built binary
        // Vulkan and OpenVINO require compilation from source
        if (gpuDiagnostics.HasNvidiaGpu)
        {
            recommended.Add("cuda");
        }

        // If no NVIDIA GPU detected, no recommendations (user can use CPU)
        _logger.LogDebug("Recommended backends: {Backends}", string.Join(", ", recommended));
        return recommended;
    }

    /// <inheritdoc />
    public bool IsBackendInstalled(string backendId)
    {
        var backendDir = Path.Combine(_whisperDirectory, backendId);
        var isInstalled = Directory.Exists(backendDir) && 
                         Directory.GetFiles(backendDir, "*.exe", SearchOption.AllDirectories).Length > 0;
        
        _logger.LogDebug("Backend {BackendId} installed: {IsInstalled}", backendId, isInstalled);
        return isInstalled;
    }

    /// <inheritdoc />
    public long GetAvailableDiskSpace()
    {
        try
        {
            var pathRoot = Path.GetPathRoot(_whisperDirectory);
            if (string.IsNullOrEmpty(pathRoot))
            {
                _logger.LogWarning("Unable to determine path root for {Path}", _whisperDirectory);
                return long.MaxValue;
            }
            
            var driveInfo = new DriveInfo(pathRoot);
            return driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get available disk space");
            return long.MaxValue; // Assume enough space if we can't check
        }
    }

    private async Task<bool> ValidateChecksumAsync(string filePath, string expectedChecksum, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Expected format: "sha256:hash"
            var parts = expectedChecksum.Split(':', 2);
            if (parts.Length != 2 || !parts[0].Equals("sha256", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Invalid checksum format: {Checksum}", expectedChecksum);
                return false;
            }

            var expectedHash = parts[1].ToLowerInvariant();

            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var isValid = actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
            if (!isValid)
            {
                _logger.LogError("Checksum mismatch. Expected: {Expected}, Actual: {Actual}",
                    expectedHash, actualHash);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate checksum");
            return false;
        }
    }

    private void ReportProgress(IProgress<BackendDownloadProgress>? progress, string backendId,
        BackendDownloadStatus status, long bytesDownloaded, long totalBytes, string message,
        string? errorMessage = null)
    {
        progress?.Report(new BackendDownloadProgress
        {
            BackendId = backendId,
            Status = status,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            Message = message,
            ErrorMessage = errorMessage
        });
    }

    /// <inheritdoc />
    public bool AreCudaDllsInstalled()
    {
        // First check the directory where the CUDA executable is located
        var cudaReleaseDir = GetCudaReleaseDirectory();
        if (Directory.Exists(cudaReleaseDir))
        {
            if (BackendSelectionService.AreCudaDllsInDirectory(cudaReleaseDir))
            {
                _logger.LogDebug("All required CUDA DLLs found in: {Path}", cudaReleaseDir);
                return true;
            }
        }
        else
        {
            _logger.LogDebug("CUDA release directory does not exist: {Path}", cudaReleaseDir);
        }

        // Also check the system PATH as a fallback
        if (BackendSelectionService.AreCudaDllsInPath())
        {
            _logger.LogDebug("All required CUDA DLLs found in system PATH");
            return true;
        }

        _logger.LogDebug("Required CUDA DLLs not found in {Dir} or PATH", cudaReleaseDir);
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> DownloadCudaDllsAsync(
        IProgress<BackendDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const string backendId = "cuda-dlls";
        
        try
        {
            _logger.LogInformation("Starting download of CUDA runtime DLLs from NVIDIA redistribution site");

            // Ensure CUDA backend directory exists
            var cudaReleaseDir = GetCudaReleaseDirectory();
            Directory.CreateDirectory(cudaReleaseDir);

            // Check disk space - need space for both downloads plus extraction
            var totalRequiredSpace = (CudaRuntimeSize + CublasSize) * 2;
            var availableSpace = GetAvailableDiskSpace();
            if (availableSpace < totalRequiredSpace)
            {
                var errorMsg = $"Insufficient disk space. Need {FormatUtility.FormatBytes(totalRequiredSpace)}, " +
                              $"have {FormatUtility.FormatBytes(availableSpace)}";
                _logger.LogError(errorMsg);
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Insufficient disk space", errorMsg);
                return false;
            }

            // Create temp download directory
            var tempDir = Path.Combine(Path.GetTempPath(), "VoxTether", "cuda-dlls");
            Directory.CreateDirectory(tempDir);

            var totalSize = CudaRuntimeSize + CublasSize;
            var totalDownloaded = 0L;

            // Download and extract CUDA Runtime (contains cudart64_110.dll)
            ReportProgress(progress, backendId, BackendDownloadStatus.Downloading,
                totalDownloaded, totalSize, "Downloading CUDA Runtime...");

            var cudaRuntimeZip = Path.Combine(tempDir, "cuda_cudart.zip");
            if (!await DownloadFileAsync(CudaRuntimeUrl, cudaRuntimeZip, (downloaded, total) =>
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Downloading,
                    downloaded, totalSize,
                    $"Downloading CUDA Runtime... {FormatUtility.FormatBytes(downloaded)}");
            }, cancellationToken))
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Failed to download CUDA Runtime", "Download failed");
                return false;
            }

            totalDownloaded = CudaRuntimeSize;

            // Extract CUDA Runtime DLLs
            ReportProgress(progress, backendId, BackendDownloadStatus.Extracting,
                totalDownloaded, totalSize, "Extracting CUDA Runtime...");

            if (!ExtractCudaDllsFromArchive(cudaRuntimeZip, cudaReleaseDir, ["cudart64_12.dll"]))
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Failed to extract CUDA Runtime", "Extraction failed");
                return false;
            }

            // Download cuBLAS (contains cublas64_12.dll and cublasLt64_12.dll)
            ReportProgress(progress, backendId, BackendDownloadStatus.Downloading,
                totalDownloaded, totalSize, "Downloading cuBLAS library...");

            var cublasZip = Path.Combine(tempDir, "libcublas.zip");
            if (!await DownloadFileAsync(CublasUrl, cublasZip, (downloaded, total) =>
            {
                var currentTotal = CudaRuntimeSize + downloaded;
                ReportProgress(progress, backendId, BackendDownloadStatus.Downloading,
                    currentTotal, totalSize,
                    $"Downloading cuBLAS... {FormatUtility.FormatBytes(downloaded)} / {FormatUtility.FormatBytes(CublasSize)}");
            }, cancellationToken))
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Failed to download cuBLAS", "Download failed");
                return false;
            }

            totalDownloaded = totalSize;

            // Extract cuBLAS DLLs
            ReportProgress(progress, backendId, BackendDownloadStatus.Extracting,
                totalDownloaded, totalSize, "Extracting cuBLAS...");

            if (!ExtractCudaDllsFromArchive(cublasZip, cudaReleaseDir, ["cublas64_12.dll", "cublasLt64_12.dll"]))
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Failed to extract cuBLAS", "Extraction failed");
                return false;
            }

            // Clean up temp files
            try
            {
                File.Delete(cudaRuntimeZip);
                File.Delete(cublasZip);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp files");
            }

            // Verify all DLLs are installed
            if (!AreCudaDllsInstalled())
            {
                ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                    0, 0, "Verification failed", "Not all required DLLs were installed");
                return false;
            }

            _logger.LogInformation("CUDA runtime DLLs installed successfully to {Path}", cudaReleaseDir);
            ReportProgress(progress, backendId, BackendDownloadStatus.Completed,
                totalSize, totalSize, "CUDA DLLs installed successfully");

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CUDA DLL download cancelled");
            ReportProgress(progress, backendId, BackendDownloadStatus.Cancelled,
                0, 0, "Download cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download CUDA DLLs");
            ReportProgress(progress, backendId, BackendDownloadStatus.Failed,
                0, 0, "Download failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the path to the CUDA backend's directory where DLLs should be placed.
    /// This is the same directory as the CUDA whisper executable.
    /// If the CUDA executable is found, returns its directory.
    /// Otherwise falls back to the default whisper/cuda/Release/ path.
    /// </summary>
    private string GetCudaReleaseDirectory()
    {
        // Try to find the CUDA executable and use its directory
        var cudaExePath = FindCudaExecutable();
        if (!string.IsNullOrEmpty(cudaExePath))
        {
            var exeDir = Path.GetDirectoryName(cudaExePath);
            if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
            {
                _logger.LogDebug("Using CUDA executable directory for DLLs: {Path}", exeDir);
                return exeDir;
            }
        }

        // Fallback: The whisper.cpp CUDA release typically extracts to whisper/cuda/Release/
        var fallbackDir = Path.Combine(_whisperDirectory, "cuda", "Release");
        _logger.LogDebug("Using fallback CUDA DLL directory: {Path}", fallbackDir);
        return fallbackDir;
    }

    /// <summary>
    /// Finds the CUDA whisper executable path.
    /// Searches in common locations where the CUDA backend might be installed.
    /// </summary>
    private string? FindCudaExecutable()
    {
        var executableNames = new[] { "whisper-cli.exe", "whisper.exe", "main.exe" };
        var searchPaths = new[]
        {
            Path.Combine(_whisperDirectory, "cuda", "Release"),
            Path.Combine(_whisperDirectory, "cuda", "bin"),
            Path.Combine(_whisperDirectory, "cuda"),
            _whisperDirectory
        };

        foreach (var searchPath in searchPaths)
        {
            if (!Directory.Exists(searchPath))
                continue;

            foreach (var exeName in executableNames)
            {
                var fullPath = Path.Combine(searchPath, exeName);
                if (File.Exists(fullPath))
                {
                    _logger.LogDebug("Found CUDA executable at: {Path}", fullPath);
                    return fullPath;
                }
            }
        }

        // Also check for whisper_cuda.exe at the whisper directory level
        var cudaExePath = Path.Combine(_whisperDirectory, "whisper_cuda.exe");
        if (File.Exists(cudaExePath))
        {
            _logger.LogDebug("Found CUDA executable at: {Path}", cudaExePath);
            return cudaExePath;
        }

        _logger.LogDebug("CUDA executable not found");
        return null;
    }

    /// <summary>
    /// Downloads a file from a URL with progress reporting.
    /// </summary>
    private async Task<bool> DownloadFileAsync(
        string url,
        string destinationPath,
        Action<long, long> onProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Downloading from {Url} to {Path}", url, destinationPath);

            using var response = await _httpClient.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[8192];
            long bytesRead = 0;
            int lastReportedPercent = -1;

            while (true)
            {
                var read = await downloadStream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                // Report progress every 1%
                if (totalBytes > 0)
                {
                    var percent = (int)((bytesRead * 100) / totalBytes);
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        onProgress(bytesRead, totalBytes);
                    }
                }
            }

            _logger.LogDebug("Download completed: {Bytes} bytes", bytesRead);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download from {Url}", url);
            return false;
        }
    }

    /// <summary>
    /// Extracts specific DLL files from a CUDA redistribution archive to the destination directory.
    /// NVIDIA archives have structure: archive-name/bin/dll-files
    /// </summary>
    private bool ExtractCudaDllsFromArchive(string archivePath, string destinationDir, string[] dllNames)
    {
        try
        {
            _logger.LogDebug("Extracting DLLs from {Archive} to {Destination}", archivePath, destinationDir);

            using var archive = ZipFile.OpenRead(archivePath);

            foreach (var dllName in dllNames)
            {
                // Find the DLL in the archive (it's in the bin subdirectory)
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.Contains("/bin/", StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    _logger.LogError("DLL {DllName} not found in archive {Archive}", dllName, archivePath);
                    return false;
                }

                var destPath = Path.Combine(destinationDir, dllName);
                _logger.LogDebug("Extracting {Entry} to {Destination}", entry.FullName, destPath);

                // Extract the file, overwriting if it exists
                entry.ExtractToFile(destPath, overwrite: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract DLLs from archive {Archive}", archivePath);
            return false;
        }
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
