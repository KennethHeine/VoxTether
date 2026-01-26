using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace VoxTether.Core.Tests.Utilities;

/// <summary>
/// Base class for tests that require logging support.
/// Provides ILogger and ILoggerFactory configured to output to xUnit test output.
/// </summary>
public abstract class TestBase
{
    protected readonly ILogger Logger;
    protected readonly ILoggerFactory LoggerFactory;

    protected TestBase(ITestOutputHelper output)
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddProvider(new XUnitLoggerProvider(output))
                .SetMinimumLevel(LogLevel.Debug);
        });

        Logger = LoggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Simple logger provider that writes to xUnit test output.
    /// </summary>
    private class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XUnitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XUnitLogger(_output, categoryName);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Simple logger that writes to xUnit test output.
    /// </summary>
    private class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XUnitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                var message = $"[{logLevel}] {_categoryName}: {formatter(state, exception)}";
                if (exception != null)
                {
                    message += $"\n{exception}";
                }
                _output.WriteLine(message);
            }
            catch (InvalidOperationException)
            {
                // xUnit output helper can throw if test is already completed
            }
        }
    }
}
