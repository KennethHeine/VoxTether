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
    private const string DefaultManifestJson = @"{
  ""version"": ""1.0"",
  ""backends"": [
    {
      ""id"": ""cuda"",
      ""name"": ""NVIDIA CUDA"",
      ""description"": ""GPU acceleration for NVIDIA graphics cards. Requires CUDA Toolkit 11.8 to be installed separately."",
      ""downloadUrl"": ""https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.3/whisper-cublas-11.8.0-bin-x64.zip"",
      ""size"": 61582231,
      ""checksum"": ""sha256:a5ef69599305bdf3e135047b1a2151dcea79bc0fa201e3ea8681069c2abc7a8c"",
      ""requirements"": ""NVIDIA GPU with CUDA support, up-to-date drivers, and CUDA Toolkit 11.8 (download from nvidia.com)""
    }
  ]
}";

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

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
