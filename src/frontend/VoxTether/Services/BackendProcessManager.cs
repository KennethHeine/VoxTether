using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace VoxTether.Services;

/// <summary>
/// Manages the Python backend process.
/// </summary>
public class BackendProcessManager : IDisposable
{
    private readonly ILogger<BackendProcessManager> _logger;
    private Process? _backendProcess;
    private bool _disposed;
    private readonly string _backendPath;
    private readonly int _port;

    public BackendProcessManager(ILogger<BackendProcessManager> logger, SettingsService settingsService)
    {
        _logger = logger;
        _port = settingsService.Settings.BackendPort;
        
        // Look for backend executable
        var baseDir = AppContext.BaseDirectory;
        _backendPath = Path.Combine(baseDir, "backend", "vox-backend.exe");
        
        // Fallback to development path
        if (!File.Exists(_backendPath))
        {
            _backendPath = Path.Combine(baseDir, "..", "..", "..", "..", "backend", "vox-backend.exe");
        }
    }

    /// <summary>
    /// Gets whether the backend process is running.
    /// </summary>
    public bool IsRunning => _backendProcess?.HasExited == false;

    /// <summary>
    /// Starts the backend process.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogDebug("Backend is already running");
            return;
        }

        if (!File.Exists(_backendPath))
        {
            _logger.LogWarning("Backend executable not found at {Path}", _backendPath);
            
            // Try running Python directly in development
            await StartPythonBackendAsync(cancellationToken);
            return;
        }

        try
        {
            _logger.LogInformation("Starting backend from {Path}", _backendPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = _backendPath,
                Arguments = $"--port {_port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_backendPath)
            };

            // Set environment variables
            startInfo.EnvironmentVariables["VOXTETHER_PORT"] = _port.ToString();

            _backendProcess = Process.Start(startInfo);

            if (_backendProcess == null)
            {
                throw new Exception("Failed to start backend process");
            }

            // Log output
            _backendProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogDebug("[Backend] {Output}", e.Data);
            };
            _backendProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("[Backend] {Error}", e.Data);
            };

            _backendProcess.BeginOutputReadLine();
            _backendProcess.BeginErrorReadLine();

            // Wait for backend to be ready
            await WaitForReadyAsync(TimeSpan.FromSeconds(30), cancellationToken);

            _logger.LogInformation("Backend started successfully on port {Port}", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start backend");
            throw;
        }
    }

    private async Task StartPythonBackendAsync(CancellationToken cancellationToken)
    {
        // For development, try to run the Python backend directly
        var srcPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "backend");
        var mainPy = Path.Combine(srcPath, "main.py");

        if (!File.Exists(mainPy))
        {
            _logger.LogWarning("Python backend not found at {Path}", mainPy);
            return;
        }

        try
        {
            _logger.LogInformation("Starting Python backend from {Path}", mainPy);

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m uvicorn main:app --host 127.0.0.1 --port {_port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = srcPath
            };

            _backendProcess = Process.Start(startInfo);

            if (_backendProcess != null)
            {
                _backendProcess.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogDebug("[Backend] {Output}", e.Data);
                };
                _backendProcess.BeginOutputReadLine();
                _backendProcess.BeginErrorReadLine();

                await WaitForReadyAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Python backend");
        }
    }

    private async Task WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Backend is ready after {Elapsed}ms", stopwatch.ElapsedMilliseconds);
                    return;
                }
            }
            catch
            {
                // Backend not ready yet
            }

            await Task.Delay(500, cancellationToken);
        }

        _logger.LogWarning("Backend did not become ready within {Timeout}", timeout);
    }

    /// <summary>
    /// Stops the backend process.
    /// </summary>
    public void Stop()
    {
        if (_backendProcess == null || _backendProcess.HasExited)
            return;

        try
        {
            _logger.LogInformation("Stopping backend process");
            _backendProcess.Kill(entireProcessTree: true);
            _backendProcess.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping backend process");
        }
        finally
        {
            _backendProcess?.Dispose();
            _backendProcess = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
