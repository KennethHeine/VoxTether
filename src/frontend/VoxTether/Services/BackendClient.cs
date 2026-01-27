using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Services;

/// <summary>
/// HTTP client for communicating with the Python backend.
/// </summary>
public class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackendClient> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BackendClient(HttpClient httpClient, ILogger<BackendClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        string language = "auto",
        bool translate = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            
            // Add the audio file
            var fileBytes = await File.ReadAllBytesAsync(wavPath, cancellationToken);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", Path.GetFileName(wavPath));
            
            // Add parameters
            content.Add(new StringContent(language), "language");
            content.Add(new StringContent(translate.ToString().ToLower()), "translate");
            
            var response = await _httpClient.PostAsync("/api/transcribe", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(JsonOptions, cancellationToken);
            
            return new TranscriptionResult(
                result?.Text ?? "",
                result?.Success ?? false,
                result?.Duration ?? 0,
                result?.Language,
                result?.Error
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return new TranscriptionResult("", false, 0, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/models", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ModelsResponse>(JsonOptions, cancellationToken);
            
            return result?.Models.Select(m => new ModelInfo(
                m.Name,
                m.DisplayName,
                m.SizeMb,
                m.Downloaded,
                m.Path,
                m.Description
            )).ToList() ?? new List<ModelInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get models");
            throw;
        }
    }

    public async Task DownloadModelAsync(
        string modelName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/models/{modelName}/download",
                null,
                cancellationToken
            );
            
            // Read SSE stream
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            
            const string dataPrefix = "data: ";
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                
                if (string.IsNullOrEmpty(line) || !line.StartsWith(dataPrefix) || line.Length <= dataPrefix.Length)
                    continue;
                
                var json = line.Substring(dataPrefix.Length); // Remove "data: " prefix
                var progressData = JsonSerializer.Deserialize<DownloadProgressResponse>(json, JsonOptions);
                
                if (progressData != null)
                {
                    progress?.Report(new DownloadProgress(
                        progressData.Status,
                        progressData.Progress,
                        progressData.DownloadedMb,
                        progressData.TotalMb,
                        progressData.SpeedMbps,
                        progressData.Error
                    ));
                    
                    if (progressData.Status == "complete" || progressData.Status == "error")
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download model {ModelName}", modelName);
            throw;
        }
    }

    public async Task<bool> LoadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/models/{modelName}/load",
                null,
                cancellationToken
            );
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model {ModelName}", modelName);
            return false;
        }
    }

    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/devices", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<DeviceInfoResponse>(JsonOptions, cancellationToken);
            
            return new DeviceInfo(
                result?.CudaAvailable ?? false,
                result?.CudaVersion,
                result?.DeviceName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device info");
            return new DeviceInfo(false, null, null);
        }
    }

    #region Response DTOs

    private record TranscriptionResponse(
        string Text,
        bool Success,
        double Duration,
        string? Language,
        string? Error);

    private record ModelsResponse(List<ModelResponse> Models, string? CurrentModel);

    private record ModelResponse(
        string Name,
        string DisplayName,
        int SizeMb,
        bool Downloaded,
        string? Path,
        string Description);

    private record DownloadProgressResponse(
        string Status,
        double Progress,
        double DownloadedMb,
        double TotalMb,
        double SpeedMbps,
        string? Error);

    private record DeviceInfoResponse(
        bool CudaAvailable,
        string? CudaVersion,
        string? DeviceName);

    #endregion
}
