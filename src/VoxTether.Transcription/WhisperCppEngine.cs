using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Transcription;

/// <summary>
/// Transcription engine that wraps the whisper.cpp CLI.
/// Supports multiple backend executables (CPU, CUDA, Vulkan, OpenVINO) for hardware acceleration.
/// </summary>
public class WhisperCppEngine : ITranscriptionEngine
{
    private readonly ILogger<WhisperCppEngine> _logger;
    private readonly IBackendSelectionService? _backendService;
    private readonly string _whisperPath;

    /// <summary>
    /// Creates a new WhisperCppEngine with automatic backend selection.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="backendService">Backend selection service for hardware acceleration.</param>
    public WhisperCppEngine(ILogger<WhisperCppEngine> logger, IBackendSelectionService? backendService = null)
    {
        _logger = logger;
        _backendService = backendService;
        _whisperPath = DetermineWhisperPath();
    }

    /// <summary>
    /// Creates a new WhisperCppEngine with a specific executable path (for testing).
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="whisperPath">Explicit path to whisper executable.</param>
    public WhisperCppEngine(ILogger<WhisperCppEngine> logger, string whisperPath)
    {
        _logger = logger;
        _backendService = null;
        _whisperPath = whisperPath;
    }

    /// <summary>
    /// Gets the active backend mode used by this engine.
    /// </summary>
    public TranscriptionBackendMode ActiveBackend => _backendService?.ActiveBackend ?? TranscriptionBackendMode.CpuOnly;

    private string DetermineWhisperPath()
    {
        // If a backend service is available, use the path it determined
        if (_backendService?.ActiveWhisperPath != null)
        {
            _logger.LogDebug("Using backend-selected whisper path: {Path}", _backendService.ActiveWhisperPath);
            return _backendService.ActiveWhisperPath;
        }

        // Fall back to legacy path detection
        return FindWhisperPath();
    }

    private static string FindWhisperPath()
    {
        // Look for whisper.cpp binary in various locations (legacy behavior)
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "whisper", "main.exe"),
            Path.Combine(AppContext.BaseDirectory, "whisper", "whisper.exe"),
            Path.Combine(AppContext.BaseDirectory, "whisper.exe"),
            Path.Combine(AppContext.BaseDirectory, "main.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "whisper", "main.exe");
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TranscriptionResult();

        try
        {
            if (!File.Exists(wavPath))
            {
                result.Error = $"WAV file not found: {wavPath}";
                _logger.LogError(result.Error);
                return result;
            }

            if (!File.Exists(options.ModelPath))
            {
                result.Error = $"Model file not found: {options.ModelPath}";
                _logger.LogError(result.Error);
                return result;
            }

            if (!File.Exists(_whisperPath))
            {
                result.Error = $"Whisper executable not found: {_whisperPath}";
                _logger.LogError(result.Error);
                return result;
            }

            // Build arguments
            var args = new StringBuilder();
            args.Append($"-m \"{options.ModelPath}\"");
            args.Append($" -f \"{wavPath}\"");
            args.Append(" --no-timestamps");
            args.Append(" -otxt");
            
            if (!string.IsNullOrEmpty(options.Language) && options.Language != "auto")
            {
                args.Append($" -l {options.Language}");
            }

            if (options.Translate)
            {
                args.Append(" --translate");
            }

            _logger.LogInformation("Starting transcription: {WhisperPath} {Args}", _whisperPath, args);

            var startInfo = new ProcessStartInfo
            {
                FileName = _whisperPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_whisperPath) ?? AppContext.BaseDirectory
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process with cancellation support
            var completionTask = process.WaitForExitAsync(cancellationToken);
            await completionTask;

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (process.ExitCode != 0)
            {
                result.Error = $"Whisper exited with code {process.ExitCode}: {errorBuilder}";
                _logger.LogError(result.Error);
                return result;
            }

            // Parse output - whisper outputs the text to a .txt file
            var txtPath = wavPath + ".txt";
            if (File.Exists(txtPath))
            {
                result.Text = (await File.ReadAllTextAsync(txtPath, cancellationToken)).Trim();
                
                // Clean up the txt file
                try { File.Delete(txtPath); } catch (IOException) { /* Ignore cleanup errors */ }
            }
            else
            {
                // Fallback: try to parse stdout
                result.Text = ParseWhisperOutput(outputBuilder.ToString());
            }

            result.Success = true;
            _logger.LogInformation("Transcription completed in {Duration}ms: {Text}",
                result.Duration.TotalMilliseconds, result.Text.Substring(0, Math.Min(50, result.Text.Length)));

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Error = "Transcription was cancelled";
            _logger.LogWarning(result.Error);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Error = ex.Message;
            _logger.LogError(ex, "Transcription failed");
            return result;
        }
    }

    private static string ParseWhisperOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var textBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip timing lines like "[00:00:00.000 --> 00:00:02.000]"
            if (trimmed.StartsWith("[") && trimmed.Contains("-->"))
                continue;
            
            // Skip whisper info lines
            if (trimmed.StartsWith("whisper_") || 
                trimmed.StartsWith("main:") ||
                trimmed.Contains("sampling") ||
                trimmed.Contains("load time"))
                continue;

            textBuilder.AppendLine(trimmed);
        }

        return textBuilder.ToString().Trim();
    }

    public bool IsConfigured()
    {
        return File.Exists(_whisperPath);
    }

    public string? GetWhisperPath()
    {
        return File.Exists(_whisperPath) ? _whisperPath : null;
    }
}
