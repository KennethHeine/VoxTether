using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Transcription;

/// <summary>
/// Transcription engine that wraps the whisper.cpp CLI.
/// Supports CPU and CUDA backends for hardware acceleration.
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
        // Note: whisper-cli.exe is preferred over main.exe as main.exe is deprecated in whisper.cpp
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "whisper", "whisper-cli.exe"),
            Path.Combine(AppContext.BaseDirectory, "whisper", "whisper.exe"),
            Path.Combine(AppContext.BaseDirectory, "whisper", "main.exe"),
            Path.Combine(AppContext.BaseDirectory, "whisper-cli.exe"),
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

        return Path.Combine(AppContext.BaseDirectory, "whisper", "whisper-cli.exe");
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

            // Use TaskCompletionSource to signal when output/error streams are fully read
            // This fixes a race condition where WaitForExitAsync() returns before
            // all async output handlers have finished processing
            var outputDone = new TaskCompletionSource<bool>();
            var errorDone = new TaskCompletionSource<bool>();

            // Register cancellation to prevent hanging if process is killed before streams close
            using var registration = cancellationToken.Register(() =>
            {
                outputDone.TrySetCanceled(cancellationToken);
                errorDone.TrySetCanceled(cancellationToken);
            });

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
                else
                {
                    // Null data signals end of stream
                    outputDone.TrySetResult(true);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
                else
                {
                    // Null data signals end of stream
                    errorDone.TrySetResult(true);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process exit AND both streams to finish reading
            // This ensures all stderr/stdout data is captured before we access it
            await Task.WhenAll(
                process.WaitForExitAsync(cancellationToken),
                outputDone.Task,
                errorDone.Task
            );

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (process.ExitCode != 0)
            {
                var stderr = errorBuilder.ToString().Trim();
                var stdout = outputBuilder.ToString().Trim();
                
                // Check for Windows NTSTATUS error codes that indicate DLL or runtime issues
                // STATUS_DLL_NOT_FOUND (0xC0000135 = -1073741515) - Missing DLL at load time
                // STATUS_STACK_BUFFER_OVERRUN (0xC0000409 = -1073740791) - Often indicates DLL version mismatch during CUDA operations
                const int STATUS_DLL_NOT_FOUND = unchecked((int)0xC0000135);
                const int STATUS_STACK_BUFFER_OVERRUN = unchecked((int)0xC0000409);
                
                if (process.ExitCode == STATUS_DLL_NOT_FOUND)
                {
                    result.Error = "Missing required DLLs. If using CUDA backend, please install the NVIDIA CUDA Toolkit 11.8 or switch to CPU backend in Settings.";
                    _logger.LogError("Whisper transcription failed due to missing DLLs (likely CUDA runtime). " +
                        "Exit code: {ExitCode}. Consider switching to CPU backend or installing CUDA Toolkit 11.8.", 
                        process.ExitCode);
                    return result;
                }
                
                if (process.ExitCode == STATUS_STACK_BUFFER_OVERRUN)
                {
                    // This error often occurs when there's a version mismatch between CUDA DLLs
                    // and what whisper.cpp was compiled against. The downloaded redistribution DLLs
                    // may not be fully compatible with the pre-built whisper.cpp binary.
                    result.Error = "CUDA runtime error during transcription. This is often caused by CUDA DLL version mismatch. " +
                        "Please install the full NVIDIA CUDA Toolkit 11.8 from https://developer.nvidia.com/cuda-11-8-0-download-archive, " +
                        "or switch to CPU backend in Settings. See docs/cuda-troubleshooting.md for more information.";
                    _logger.LogError("Whisper transcription failed with STATUS_STACK_BUFFER_OVERRUN (0xC0000409). " +
                        "This typically indicates CUDA DLL version mismatch. The auto-downloaded CUDA DLLs may not be fully compatible " +
                        "with this whisper.cpp build. Install the full CUDA Toolkit 11.8 from https://developer.nvidia.com/cuda-11-8-0-download-archive " +
                        "or switch to CPU backend. Exit code: {ExitCode}, stderr: {StdErr}", 
                        process.ExitCode, stderr);
                    return result;
                }
                
                // Include both stderr and stdout in error message for better diagnostics
                // whisper.cpp often writes error info to stdout (e.g., model loading, CUDA initialization)
                var errorDetails = new StringBuilder();
                if (!string.IsNullOrEmpty(stderr))
                {
                    errorDetails.AppendLine($"stderr: {stderr}");
                }
                if (!string.IsNullOrEmpty(stdout))
                {
                    errorDetails.AppendLine($"stdout: {stdout}");
                }
                
                result.Error = $"Whisper exited with code {process.ExitCode}";
                if (errorDetails.Length > 0)
                {
                    result.Error += $"\n{errorDetails.ToString().TrimEnd()}";
                }
                
                _logger.LogError("Whisper transcription failed. Exit code: {ExitCode}, stderr: {StdErr}, stdout: {StdOut}", 
                    process.ExitCode, stderr, stdout);
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
