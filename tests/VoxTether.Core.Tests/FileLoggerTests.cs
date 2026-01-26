using Microsoft.Extensions.Logging;
using VoxTether.Core.Services;

namespace VoxTether.Core.Tests;

public class FileLoggerTests
{
    private readonly string _testLogPath;

    public FileLoggerTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), "VoxTetherTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogPath);
    }

    [Fact]
    public void FileLogger_WritesLogFileWithVersionInName()
    {
        // Arrange
        var version = "1.0.0";
        var logger = new FileLogger("TestCategory", _testLogPath, version);
        
        // Act
        logger.Log(LogLevel.Information, new EventId(1), "Test message", null, (state, ex) => state);
        
        // Assert
        var expectedFileName = $"voxtether-{version}.log";
        var logFilePath = Path.Combine(_testLogPath, expectedFileName);
        Assert.True(File.Exists(logFilePath), $"Expected log file {expectedFileName} was not created");
        
        var logContent = File.ReadAllText(logFilePath);
        Assert.Contains("Test message", logContent);
    }

    [Fact]
    public void FileLogger_DifferentVersionsCreateDifferentLogFiles()
    {
        // Arrange
        var version1 = "1.0.0";
        var version2 = "2.0.0";
        var logger1 = new FileLogger("TestCategory", _testLogPath, version1);
        var logger2 = new FileLogger("TestCategory", _testLogPath, version2);
        
        // Act
        logger1.Log(LogLevel.Information, new EventId(1), "Message from v1", null, (state, ex) => state);
        logger2.Log(LogLevel.Information, new EventId(2), "Message from v2", null, (state, ex) => state);
        
        // Assert
        var logFile1 = Path.Combine(_testLogPath, $"voxtether-{version1}.log");
        var logFile2 = Path.Combine(_testLogPath, $"voxtether-{version2}.log");
        
        Assert.True(File.Exists(logFile1), $"Log file for version {version1} should exist");
        Assert.True(File.Exists(logFile2), $"Log file for version {version2} should exist");
        
        var content1 = File.ReadAllText(logFile1);
        var content2 = File.ReadAllText(logFile2);
        
        Assert.Contains("Message from v1", content1);
        Assert.Contains("Message from v2", content2);
        
        // Verify messages are in separate files
        Assert.DoesNotContain("Message from v2", content1);
        Assert.DoesNotContain("Message from v1", content2);
    }

    [Fact]
    public void FileLoggerProvider_CreatesLoggerWithVersion()
    {
        // Arrange
        var version = "3.0.0";
        using var provider = new FileLoggerProvider(_testLogPath, version);
        
        // Act
        var logger = provider.CreateLogger("TestCategory");
        logger.Log(LogLevel.Information, new EventId(1), "Provider test message", null, (state, ex) => state);
        
        // Assert
        var expectedFileName = $"voxtether-{version}.log";
        var logFilePath = Path.Combine(_testLogPath, expectedFileName);
        Assert.True(File.Exists(logFilePath), $"Expected log file {expectedFileName} was not created");
    }

    [Fact]
    public void FileLogger_VersionWithPreReleaseSuffix_CreatesLogFileCorrectly()
    {
        // Arrange - Tests with version that has additional info like git commit
        var version = "1.2.3+abc123";
        var logger = new FileLogger("TestCategory", _testLogPath, version);
        
        // Act
        logger.Log(LogLevel.Information, new EventId(1), "Pre-release test", null, (state, ex) => state);
        
        // Assert
        var expectedFileName = $"voxtether-{version}.log";
        var logFilePath = Path.Combine(_testLogPath, expectedFileName);
        Assert.True(File.Exists(logFilePath), $"Expected log file {expectedFileName} was not created");
    }
}
