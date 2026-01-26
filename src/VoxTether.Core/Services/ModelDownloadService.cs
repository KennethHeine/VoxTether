using System.IO;
using System.Net.Http;
using VoxTether.Core.Models;

namespace VoxTether.Core.Services;

/// <summary>
/// Service for downloading speech-to-text models.
/// </summary>
public class ModelDownloadService : IDisposable
{
    private const int BufferSize = 8192;
    private const double BytesToMb = 1024.0 * 1024.0;
    
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    public ModelDownloadService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // Large files may take time
    }

    /// <summary>
    /// Event raised to report download progress.
    /// </summary>
    public event Action<int>? DownloadProgressChanged;

    /// <summary>
    /// Event raised when download status changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Checks if a model is already downloaded.
    /// </summary>
    public bool IsModelDownloaded(string fileName)
    {
        var userModelsPath = SettingsService.UserModelsPath;
        var modelPath = Path.Combine(userModelsPath, fileName);
        return File.Exists(modelPath);
    }

    /// <summary>
    /// Gets the file path for a downloaded model.
    /// </summary>
    public string GetModelPath(string fileName)
    {
        return Path.Combine(SettingsService.UserModelsPath, fileName);
    }

    /// <summary>
    /// Downloads a model file to the user's models folder.
    /// </summary>
    public async Task<bool> DownloadModelAsync(ModelVersion modelVersion, IProgress<int>? progress = null)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            StatusChanged?.Invoke($"Starting download: {modelVersion.FileName}...");

            var destinationPath = GetModelPath(modelVersion.FileName);
            var tempPath = destinationPath + ".download";

            // Ensure the models directory exists
            Directory.CreateDirectory(SettingsService.UserModelsPath);

            // Download with progress reporting
            using var response = await _httpClient.GetAsync(
                modelVersion.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalMb = totalBytes > 0 ? totalBytes / BytesToMb : modelVersion.SizeMb;

            StatusChanged?.Invoke($"Downloading {modelVersion.FileName} ({totalMb:F1} MB)...");

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

            var buffer = new byte[BufferSize];
            var totalBytesRead = 0L;
            var lastReportedProgress = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalBytesRead * 100) / totalBytes);
                    if (progressPercent != lastReportedProgress)
                    {
                        lastReportedProgress = progressPercent;
                        progress?.Report(progressPercent);
                        DownloadProgressChanged?.Invoke(progressPercent);
                        StatusChanged?.Invoke($"Downloading {modelVersion.FileName}: {progressPercent}%");
                    }
                }
            }

            // Close the file stream before renaming
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            // Rename temp file to final name
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);

            StatusChanged?.Invoke($"Download complete: {modelVersion.FileName}");
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke("Download cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Download failed: {ex.Message}");
            return false;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Cancels the current download.
    /// </summary>
    public void CancelDownload()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Deletes a downloaded model.
    /// </summary>
    public bool DeleteModel(string fileName)
    {
        try
        {
            var modelPath = GetModelPath(fileName);
            if (File.Exists(modelPath))
            {
                File.Delete(modelPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the HTTP client and cancellation token source.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _httpClient.Dispose();
        }

        _disposed = true;
    }
}
