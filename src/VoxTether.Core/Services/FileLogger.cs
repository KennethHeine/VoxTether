using System.IO;
using Microsoft.Extensions.Logging;

namespace VoxTether.Core.Services;

/// <summary>
/// File-based logger that writes to rolling log files.
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _name;
    private readonly string _logPath;
    private readonly string _version;
    private readonly object _lock = new();
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private const int MaxLogFiles = 5;

    public FileLogger(string name, string logPath, string version)
    {
        _name = name;
        _logPath = logPath;
        _version = version;
    }

    /// <summary>
    /// Gets the base log filename including the version (e.g., "voxtether-1.0.0.log").
    /// </summary>
    private string LogFileName => $"voxtether-{_version}.log";

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{_name}] {formatter(state, exception)}";
        if (exception != null)
        {
            message += $"\n{exception}";
        }

        lock (_lock)
        {
            try
            {
                var logFile = Path.Combine(_logPath, LogFileName);
                
                // Check for log rotation
                if (File.Exists(logFile))
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.Length > MaxFileSizeBytes)
                    {
                        RotateLogs();
                    }
                }

                File.AppendAllText(logFile, message + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging should never throw - file may be locked or inaccessible
                // This is intentional: logging failures should not crash the application
            }
            catch (UnauthorizedAccessException)
            {
                // Logging should never throw - we may not have permission to write
            }
        }
    }

    private void RotateLogs()
    {
        // Get the base name without extension (e.g., "voxtether-1.0.0")
        var baseName = Path.GetFileNameWithoutExtension(LogFileName);
        var logFile = Path.Combine(_logPath, LogFileName);

        // Delete oldest log if we have too many
        var oldestLog = Path.Combine(_logPath, $"{baseName}.{MaxLogFiles}.log");
        if (File.Exists(oldestLog))
        {
            File.Delete(oldestLog);
        }

        // Rotate existing logs
        for (int i = MaxLogFiles - 1; i >= 1; i--)
        {
            var source = Path.Combine(_logPath, $"{baseName}.{i}.log");
            var dest = Path.Combine(_logPath, $"{baseName}.{i + 1}.log");
            if (File.Exists(source))
            {
                File.Move(source, dest);
            }
        }

        // Move current log
        if (File.Exists(logFile))
        {
            File.Move(logFile, Path.Combine(_logPath, $"{baseName}.1.log"));
        }
    }
}

/// <summary>
/// Logger provider that creates file loggers.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;
    private readonly string _version;

    public FileLoggerProvider(string logPath, string version)
    {
        _logPath = logPath;
        _version = version;
        Directory.CreateDirectory(logPath);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logPath, _version);
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Extension methods for adding file logging.
/// </summary>
public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logPath, string version)
    {
        builder.AddProvider(new FileLoggerProvider(logPath, version));
        return builder;
    }
}
