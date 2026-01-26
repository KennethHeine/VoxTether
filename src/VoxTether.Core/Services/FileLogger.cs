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
    private readonly object _lock = new();
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private const int MaxLogFiles = 5;

    public FileLogger(string name, string logPath)
    {
        _name = name;
        _logPath = logPath;
    }

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
                var logFile = Path.Combine(_logPath, "voxtether.log");
                
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
        var logFile = Path.Combine(_logPath, "voxtether.log");

        // Delete oldest log if we have too many
        var oldestLog = Path.Combine(_logPath, $"voxtether.{MaxLogFiles}.log");
        if (File.Exists(oldestLog))
        {
            File.Delete(oldestLog);
        }

        // Rotate existing logs
        for (int i = MaxLogFiles - 1; i >= 1; i--)
        {
            var source = Path.Combine(_logPath, $"voxtether.{i}.log");
            var dest = Path.Combine(_logPath, $"voxtether.{i + 1}.log");
            if (File.Exists(source))
            {
                File.Move(source, dest);
            }
        }

        // Move current log
        if (File.Exists(logFile))
        {
            File.Move(logFile, Path.Combine(_logPath, "voxtether.1.log"));
        }
    }
}

/// <summary>
/// Logger provider that creates file loggers.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;

    public FileLoggerProvider(string logPath)
    {
        _logPath = logPath;
        Directory.CreateDirectory(logPath);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logPath);
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
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logPath)
    {
        builder.AddProvider(new FileLoggerProvider(logPath));
        return builder;
    }
}
