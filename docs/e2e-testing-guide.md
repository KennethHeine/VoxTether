# End-to-End Testing Guide for VoxTether

This guide explains how to create better end-to-end (e2e) tests for VoxTether to ensure the application works correctly and errors are captured in CI workflow logs.

## Table of Contents

1. [Current Testing Architecture](#current-testing-architecture)
2. [Challenges with E2E Testing](#challenges-with-e2e-testing)
3. [Recommended E2E Testing Strategy](#recommended-e2e-testing-strategy)
4. [Mock Implementations](#mock-implementations)
5. [Integration Test Examples](#integration-test-examples)
6. [CI Workflow Integration](#ci-workflow-integration)
7. [Logging for CI Visibility](#logging-for-ci-visibility)
8. [Test Fixtures and Utilities](#test-fixtures-and-utilities)

---

## Current Testing Architecture

VoxTether uses **xUnit** for testing with the following structure:

```
tests/
└── VoxTether.Core.Tests/
    ├── BackendDownloadTests.cs
    ├── BackendSelectionTests.cs
    ├── FileLoggerTests.cs
    ├── HotkeyTests.cs
    ├── SettingsTests.cs
    ├── UpdateServiceTests.cs
    └── VoxTether.Core.Tests.csproj
```

The current tests are **unit tests** that test individual components in isolation. To achieve comprehensive e2e testing, we need **integration tests** that verify the entire workflow from hotkey press → audio recording → transcription → text injection.

---

## Challenges with E2E Testing

VoxTether has several components that are challenging to test in a CI environment:

| Component | Challenge | Solution |
|-----------|-----------|----------|
| **Audio Recording** | Requires physical microphone hardware | Use mock `IAudioRecorder` |
| **whisper.cpp** | External binary, large model files | Use mock `ITranscriptionEngine` |
| **Global Hotkeys** | Requires Windows UI interaction | Use mock `IHotkeyService` |
| **Text Injection** | Requires clipboard/window focus | Use mock `ITextInjector` |
| **WPF UI** | Requires Windows Desktop App runtime | Separate UI tests from logic tests |

---

## Recommended E2E Testing Strategy

### Strategy 1: Controller Integration Tests (Recommended)

Test the `VoxTetherController` with mock dependencies to verify the complete workflow:

```
[HotkeyPressed] → [StartRecording] → [HotkeyReleased] → [StopRecording] → [Transcribe] → [InjectText]
```

This approach tests all the orchestration logic without requiring actual hardware.

### Strategy 2: Component Integration Tests

Test pairs of components together:
- `AudioRecorder` + `TranscriptionEngine` (with pre-recorded audio files)
- `TranscriptionEngine` + `TextInjector`

### Strategy 3: Headless UI Tests

For WPF-specific behavior, use automation frameworks like:
- **FlaUI** - UI Automation for Windows apps
- **Appium** - Cross-platform app automation

---

## Mock Implementations

### MockAudioRecorder

Create a mock that returns pre-recorded audio files for testing:

```csharp
// tests/VoxTether.Core.Tests/Mocks/MockAudioRecorder.cs
using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

public class MockAudioRecorder : IAudioRecorder
{
    private readonly string _testAudioPath;
    private bool _isRecording;
    
    public bool IsRecording => _isRecording;
    public int SelectedDeviceId { get; set; } = -1;
    
    public event EventHandler? RecordingStarted;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<int>? AudioLevelChanged;
    
    public MockAudioRecorder(string testAudioPath)
    {
        _testAudioPath = testAudioPath;
    }
    
    public void StartRecording(string outputWavPath)
    {
        _isRecording = true;
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }
    
    public string StopRecording()
    {
        _isRecording = false;
        // Return the test audio file path instead of recording
        RecordingStopped?.Invoke(this, _testAudioPath);
        return _testAudioPath;
    }
    
    public bool HasRecordingDevice() => true;
    public string? GetDefaultDeviceName() => "Mock Microphone";
    public List<(int DeviceId, string DeviceName)> GetAvailableDevices() 
        => [(0, "Mock Microphone")];
    
    public void Dispose() { }
}
```

### MockTranscriptionEngine

Create a mock that returns predictable transcription results:

```csharp
// tests/VoxTether.Core.Tests/Mocks/MockTranscriptionEngine.cs
using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

public class MockTranscriptionEngine : ITranscriptionEngine
{
    private readonly string _expectedTranscription;
    private readonly bool _shouldFail;
    private readonly string? _errorMessage;
    
    public string? LastTranscribedFile { get; private set; }
    public TranscriptionOptions? LastOptions { get; private set; }
    
    public MockTranscriptionEngine(
        string expectedTranscription = "Hello world",
        bool shouldFail = false,
        string? errorMessage = null)
    {
        _expectedTranscription = expectedTranscription;
        _shouldFail = shouldFail;
        _errorMessage = errorMessage;
    }
    
    public Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        LastTranscribedFile = wavPath;
        LastOptions = options;
        
        if (_shouldFail)
        {
            return Task.FromResult(new TranscriptionResult
            {
                Success = false,
                Error = _errorMessage ?? "Mock transcription failure"
            });
        }
        
        return Task.FromResult(new TranscriptionResult
        {
            Success = true,
            Text = _expectedTranscription,
            Duration = TimeSpan.FromMilliseconds(100)
        });
    }
    
    public bool IsConfigured() => true;
    public string? GetWhisperPath() => "/mock/whisper/path";
}
```

### MockHotkeyService

Create a mock that allows programmatic triggering of hotkey events:

```csharp
// tests/VoxTether.Core.Tests/Mocks/MockHotkeyService.cs
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.Core.Tests.Mocks;

public class MockHotkeyService : IHotkeyService
{
    public HotkeyCombination? Hotkey { get; set; }
    public HotkeyCombination? ToggleHotkey { get; set; }
    public bool IsRunning { get; private set; }
    
    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
    public event EventHandler? ToggleHotkeyPressed;
    
    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
    
    // Methods to simulate user input in tests
    public void SimulatePushToTalkPress() 
        => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    
    public void SimulatePushToTalkRelease() 
        => HotkeyReleased?.Invoke(this, EventArgs.Empty);
    
    public void SimulateTogglePress() 
        => ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);
    
    public void Dispose() { }
}
```

### MockTextInjector

Create a mock to capture injected text:

```csharp
// tests/VoxTether.Core.Tests/Mocks/MockTextInjector.cs
using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

public class MockTextInjector : ITextInjector
{
    public List<string> InjectedTexts { get; } = [];
    public bool ShouldSucceed { get; set; } = true;
    
    public Task<bool> InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        InjectedTexts.Add(text);
        return Task.FromResult(ShouldSucceed);
    }
}
```

---

## Integration Test Examples

### Full Workflow Integration Test

```csharp
// tests/VoxTether.Core.Tests/IntegrationTests/VoxTetherControllerIntegrationTests.cs
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Tests.Mocks;
using VoxTether.Transcription;

namespace VoxTether.Core.Tests.IntegrationTests;

public class VoxTetherControllerIntegrationTests : IDisposable
{
    private readonly MockHotkeyService _hotkeyService;
    private readonly MockAudioRecorder _recorder;
    private readonly MockTranscriptionEngine _transcriptionEngine;
    private readonly MockTextInjector _textInjector;
    private readonly VoxTetherController _controller;
    private readonly string _tempDir;
    
    public VoxTetherControllerIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"VoxTetherTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        
        // Create a dummy audio file for testing
        var testAudioPath = Path.Combine(_tempDir, "test.wav");
        CreateDummyWavFile(testAudioPath);
        
        _hotkeyService = new MockHotkeyService();
        _recorder = new MockAudioRecorder(testAudioPath);
        _transcriptionEngine = new MockTranscriptionEngine("Hello, this is a test transcription.");
        _textInjector = new MockTextInjector();
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var settingsService = new SettingsService();
        var postProcessor = new NoOpTextPostProcessor();
        
        _controller = new VoxTetherController(
            loggerFactory.CreateLogger<VoxTetherController>(),
            settingsService,
            _recorder,
            _hotkeyService,
            _transcriptionEngine,
            _textInjector,
            postProcessor
        );
    }
    
    [Fact]
    public async Task PushToTalk_Workflow_TranscribesAndInjectsText()
    {
        // Arrange
        var transcriptionComplete = new TaskCompletionSource<string>();
        _controller.TranscriptionComplete += (_, text) => transcriptionComplete.SetResult(text);
        _controller.Start();
        
        // Act - Simulate push-to-talk workflow
        _hotkeyService.SimulatePushToTalkPress();
        Assert.True(_controller.IsRecording);
        
        await Task.Delay(100); // Brief recording simulation
        _hotkeyService.SimulatePushToTalkRelease();
        
        // Wait for transcription to complete
        var result = await transcriptionComplete.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        // Assert
        Assert.Equal("Hello, this is a test transcription.", result);
        Assert.Single(_textInjector.InjectedTexts);
        Assert.Equal("Hello, this is a test transcription.", _textInjector.InjectedTexts[0]);
    }
    
    [Fact]
    public async Task ToggleMode_Workflow_TranscribesAndInjectsText()
    {
        // Arrange
        var transcriptionComplete = new TaskCompletionSource<string>();
        _controller.TranscriptionComplete += (_, text) => transcriptionComplete.SetResult(text);
        _controller.Start();
        
        // Act - Simulate toggle mode workflow
        _hotkeyService.SimulateTogglePress(); // Start recording
        Assert.True(_controller.IsRecording);
        
        await Task.Delay(100); // Brief recording simulation
        _hotkeyService.SimulateTogglePress(); // Stop recording
        
        // Wait for transcription to complete
        var result = await transcriptionComplete.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        // Assert
        Assert.Equal("Hello, this is a test transcription.", result);
    }
    
    [Fact]
    public async Task TranscriptionFailure_RaisesErrorEvent()
    {
        // Arrange with failing transcription engine
        var failingEngine = new MockTranscriptionEngine(
            shouldFail: true,
            errorMessage: "Model not found");
        
        var errorOccurred = new TaskCompletionSource<string>();
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var controller = new VoxTetherController(
            loggerFactory.CreateLogger<VoxTetherController>(),
            new SettingsService(),
            _recorder,
            _hotkeyService,
            failingEngine,
            _textInjector,
            new NoOpTextPostProcessor()
        );
        
        controller.ErrorOccurred += (_, error) => errorOccurred.SetResult(error);
        controller.Start();
        
        // Act
        _hotkeyService.SimulatePushToTalkPress();
        await Task.Delay(50);
        _hotkeyService.SimulatePushToTalkRelease();
        
        // Assert
        var error = await errorOccurred.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Model not found", error);
        Assert.Empty(_textInjector.InjectedTexts); // No text should be injected on failure
    }
    
    private static void CreateDummyWavFile(string path)
    {
        // Create a minimal valid WAV header for testing
        // 44-byte header + 0 bytes of audio data
        byte[] wavHeader = [
            0x52, 0x49, 0x46, 0x46, // "RIFF"
            0x24, 0x00, 0x00, 0x00, // File size - 8
            0x57, 0x41, 0x56, 0x45, // "WAVE"
            0x66, 0x6D, 0x74, 0x20, // "fmt "
            0x10, 0x00, 0x00, 0x00, // Subchunk1Size (16 for PCM)
            0x01, 0x00,             // AudioFormat (1 = PCM)
            0x01, 0x00,             // NumChannels (1 = mono)
            0x80, 0x3E, 0x00, 0x00, // SampleRate (16000 Hz)
            0x00, 0x7D, 0x00, 0x00, // ByteRate
            0x02, 0x00,             // BlockAlign
            0x10, 0x00,             // BitsPerSample (16)
            0x64, 0x61, 0x74, 0x61, // "data"
            0x00, 0x00, 0x00, 0x00  // Subchunk2Size (0 bytes of audio)
        ];
        
        File.WriteAllBytes(path, wavHeader);
    }
    
    public void Dispose()
    {
        _controller.Stop();
        _recorder.Dispose();
        _hotkeyService.Dispose();
        
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
```

### Backend Integration Test

Test that backends are correctly detected and selected:

```csharp
// tests/VoxTether.Core.Tests/IntegrationTests/BackendIntegrationTests.cs
using Microsoft.Extensions.Logging;
using VoxTether.Core.Models;
using VoxTether.Transcription;

namespace VoxTether.Core.Tests.IntegrationTests;

public class BackendIntegrationTests
{
    [Fact]
    public async Task FullBackendWorkflow_ManifestToSelection()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var selectionService = new BackendSelectionService(
            loggerFactory.CreateLogger<BackendSelectionService>());
        
        using var downloadService = new BackendDownloadService(
            loggerFactory.CreateLogger<BackendDownloadService>(),
            selectionService);
        
        // Act - Get manifest
        var manifest = await downloadService.GetManifestAsync();
        
        // Assert - Manifest should contain expected backends
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.Backends);
        Assert.Contains(manifest.Backends, b => b.Id == "cuda");
        
        // Act - Get recommended backends
        var recommended = downloadService.GetRecommendedBackends();
        
        // Assert - Should always return a list (may be empty if no GPU)
        Assert.NotNull(recommended);
        
        // Log results for CI visibility
        foreach (var backend in manifest.Backends)
        {
            Console.WriteLine($"Available backend: {backend.Id} - {backend.Name}");
        }
        
        foreach (var backend in recommended)
        {
            Console.WriteLine($"Recommended backend: {backend.Id}");
        }
    }
    
    [Theory]
    [InlineData(TranscriptionBackendMode.Auto)]
    [InlineData(TranscriptionBackendMode.CpuOnly)]
    [InlineData(TranscriptionBackendMode.Cuda)]
    public void BackendSelection_AllModesAreValid(TranscriptionBackendMode mode)
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.SetMinimumLevel(LogLevel.Warning));
        
        var selectionService = new BackendSelectionService(
            loggerFactory.CreateLogger<BackendSelectionService>());
        
        // Act & Assert - Should not throw
        var result = selectionService.SelectBackend(mode);
        
        // Log for CI visibility
        Console.WriteLine($"Mode: {mode} -> Selected: {result.SelectedMode}, Reason: {result.Reason}");
        
        Assert.NotNull(result.Reason);
    }
}
```

---

## CI Workflow Integration

### Enhanced CI Workflow

Update `.github/workflows/ci.yml` to include integration tests with better error visibility:

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v6

    - name: Setup .NET
      uses: actions/setup-dotnet@v5
      with:
        dotnet-version: 8.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore --configuration Release

    - name: Run Unit Tests
      run: |
        dotnet test --no-build --configuration Release `
          --verbosity normal `
          --logger "console;verbosity=detailed" `
          --logger "trx;LogFileName=unit-test-results.trx" `
          --filter "Category!=Integration"
      continue-on-error: false

    - name: Run Integration Tests
      run: |
        dotnet test --no-build --configuration Release `
          --verbosity normal `
          --logger "console;verbosity=detailed" `
          --logger "trx;LogFileName=integration-test-results.trx" `
          --filter "Category=Integration"
      continue-on-error: true

    - name: Upload test results
      uses: actions/upload-artifact@v6
      if: always()
      with:
        name: test-results
        path: |
          **/unit-test-results.trx
          **/integration-test-results.trx
        retention-days: 7

    - name: Publish test results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Test Results
        path: '**/*.trx'
        reporter: dotnet-trx
```

### Test Categories

Mark your tests with categories to enable selective test runs:

```csharp
// Unit test - no category needed (default)
[Fact]
public void Settings_DefaultValues_AreCorrect() { ... }

// Integration test - mark with trait
[Fact]
[Trait("Category", "Integration")]
public async Task FullWorkflow_TranscribesAndInjectsText() { ... }

// Slow test - can be skipped in quick CI runs
[Fact]
[Trait("Category", "Slow")]
public async Task Download_LargeBackend_CompletesSuccessfully() { ... }
```

---

## Logging for CI Visibility

### Configure Detailed Logging in Tests

Ensure test output appears in CI logs:

```csharp
// tests/VoxTether.Core.Tests/TestBase.cs
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace VoxTether.Core.Tests;

public abstract class TestBase
{
    protected readonly ILogger Logger;
    protected readonly ILoggerFactory LoggerFactory;
    
    protected TestBase(ITestOutputHelper output)
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddXUnit(output)
                .SetMinimumLevel(LogLevel.Debug);
        });
        
        Logger = LoggerFactory.CreateLogger(GetType());
    }
}

// Example usage
public class MyIntegrationTests : TestBase
{
    public MyIntegrationTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public void TestWithLogging()
    {
        Logger.LogInformation("Starting test...");
        // Test code here
        Logger.LogInformation("Test completed successfully");
    }
}
```

> **Note:** You need to add the `Xunit.Extensions.Logging` NuGet package for `AddXUnit` support:
> ```xml
> <PackageReference Include="Xunit.Extensions.Logging" Version="1.1.0" />
> ```

### Structured Error Messages

Create helper methods for better error visibility:

```csharp
// tests/VoxTether.Core.Tests/Utilities/TestAssertions.cs
using Xunit.Sdk;

namespace VoxTether.Core.Tests.Utilities;

public static class TestAssertions
{
    public static void AssertWorkflowStep(
        string stepName,
        Action assertion,
        string? contextMessage = null)
    {
        try
        {
            assertion();
            Console.WriteLine($"✓ {stepName}");
        }
        catch (XunitException ex)
        {
            var message = $"✗ {stepName} FAILED\n";
            if (contextMessage != null)
            {
                message += $"  Context: {contextMessage}\n";
            }
            message += $"  Error: {ex.Message}";
            
            Console.WriteLine(message);
            throw new XunitException(message, ex);
        }
    }
    
    public static async Task AssertEventuallyAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string description,
        TimeSpan? interval = null)
    {
        var checkInterval = interval ?? TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow + timeout;
        
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                Console.WriteLine($"✓ {description} (within {timeout.TotalSeconds}s)");
                return;
            }
            
            await Task.Delay(checkInterval);
        }
        
        throw new XunitException(
            $"✗ {description} - condition not met within {timeout.TotalSeconds} seconds");
    }
}

// Usage example
[Fact]
public async Task TranscriptionWorkflow_AllStepsComplete()
{
    // Arrange
    var controller = CreateTestController();
    
    // Act & Assert with clear step-by-step visibility
    TestAssertions.AssertWorkflowStep("Controller started", () =>
    {
        controller.Start();
        Assert.True(_hotkeyService.IsRunning);
    });
    
    TestAssertions.AssertWorkflowStep("Recording started on hotkey press", () =>
    {
        _hotkeyService.SimulatePushToTalkPress();
        Assert.True(controller.IsRecording);
    });
    
    TestAssertions.AssertWorkflowStep("Recording stopped on hotkey release", () =>
    {
        _hotkeyService.SimulatePushToTalkRelease();
        Assert.False(controller.IsRecording);
    });
    
    await TestAssertions.AssertEventuallyAsync(
        async () => _textInjector.InjectedTexts.Count > 0,
        timeout: TimeSpan.FromSeconds(5),
        description: "Text was injected after transcription"
    );
}
```

---

## Test Fixtures and Utilities

### Test Audio Files

Create a test fixtures folder with sample audio files:

```
tests/
└── VoxTether.Core.Tests/
    └── Fixtures/
        └── Audio/
            ├── silence-1s.wav      # 1 second of silence
            ├── hello-world.wav     # "Hello world" recording
            └── long-recording.wav  # Multi-sentence recording
```

Include the fixtures in the test project:

```xml
<!-- tests/VoxTether.Core.Tests/VoxTether.Core.Tests.csproj -->
<ItemGroup>
  <Content Include="Fixtures\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Test Utilities Class

```csharp
// tests/VoxTether.Core.Tests/Utilities/TestResources.cs
namespace VoxTether.Core.Tests.Utilities;

public static class TestResources
{
    public static string FixturesPath => 
        Path.Combine(AppContext.BaseDirectory, "Fixtures");
    
    public static string GetAudioFixture(string fileName) =>
        Path.Combine(FixturesPath, "Audio", fileName);
    
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"VoxTetherTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
    
    public static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
```

---

## Summary

To create effective e2e tests for VoxTether in a CI environment:

1. **Use mock implementations** for hardware-dependent components (audio, hotkeys, clipboard)
2. **Create integration tests** that test the controller workflow with mock dependencies
3. **Enable detailed logging** to ensure errors are visible in CI logs
4. **Use test traits/categories** to separate unit and integration tests
5. **Upload test artifacts** (`.trx` files) for detailed analysis
6. **Create reusable test utilities** for common operations
7. **Use structured assertions** that provide clear step-by-step visibility in logs

### Next Steps

1. Create the `Mocks/` folder with mock implementations
2. Create the `IntegrationTests/` folder with controller tests
3. Create the `Utilities/` folder with test helpers
4. Add the `Xunit.Extensions.Logging` package for test output
5. Update the CI workflow for integration test support
6. Add test audio fixtures for real transcription tests (when whisper.cpp is available)

### Running Tests Locally

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category!=Integration"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run with detailed output
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```
