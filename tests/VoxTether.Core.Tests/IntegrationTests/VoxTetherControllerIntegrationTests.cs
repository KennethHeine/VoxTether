using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Tests.Mocks;
using VoxTether.Core.Tests.Utilities;
using VoxTether.Transcription;
using Xunit.Abstractions;

namespace VoxTether.Core.Tests.IntegrationTests;

/// <summary>
/// Integration tests for VoxTetherController that verify the complete workflow
/// from hotkey press → audio recording → transcription → text injection.
/// </summary>
[Trait("Category", "Integration")]
public class VoxTetherControllerIntegrationTests : TestBase, IDisposable
{
    private readonly MockHotkeyService _hotkeyService;
    private readonly MockAudioRecorder _recorder;
    private readonly MockTranscriptionEngine _transcriptionEngine;
    private readonly MockTextInjector _textInjector;
    private readonly VoxTetherController _controller;
    private readonly SettingsService _settingsService;
    private readonly string _tempDir;

    public VoxTetherControllerIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _tempDir = TestResources.CreateTempDirectory();

        // Create a dummy audio file for testing
        var testAudioPath = Path.Combine(_tempDir, "test.wav");
        TestResources.CreateDummyWavFile(testAudioPath);

        // Create a dummy model file for testing (controller checks for model existence)
        var testModelPath = TestResources.CreateDummyModelFile(_tempDir);

        _hotkeyService = new MockHotkeyService();
        _recorder = new MockAudioRecorder(testAudioPath);
        _transcriptionEngine = new MockTranscriptionEngine("Hello, this is a test transcription.");
        _textInjector = new MockTextInjector();

        _settingsService = new SettingsService();
        // Configure settings to use the test model
        _settingsService.Settings.ModelPath = testModelPath;
        var postProcessor = new NoOpTextPostProcessor();

        _controller = new VoxTetherController(
            LoggerFactory.CreateLogger<VoxTetherController>(),
            _settingsService,
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
        _controller.TranscriptionComplete += (_, text) => transcriptionComplete.TrySetResult(text);
        _controller.Start();

        Logger.LogInformation("Starting push-to-talk workflow test");

        // Act - Simulate push-to-talk workflow
        TestAssertions.AssertWorkflowStep("Hotkey pressed starts recording", () =>
        {
            _hotkeyService.SimulatePushToTalkPress();
            Assert.True(_controller.IsRecording);
        });

        await Task.Delay(100); // Brief recording simulation

        TestAssertions.AssertWorkflowStep("Hotkey released stops recording", () =>
        {
            _hotkeyService.SimulatePushToTalkRelease();
            Assert.False(_controller.IsRecording);
        });

        // Wait for transcription to complete
        var result = await transcriptionComplete.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        TestAssertions.AssertWorkflowStep("Transcription result is correct", () =>
        {
            Assert.Equal("Hello, this is a test transcription.", result);
        });

        await TestAssertions.AssertEventuallyAsync(
            () => _textInjector.InjectedTexts.Count > 0,
            timeout: TimeSpan.FromSeconds(5),
            description: "Text was injected after transcription"
        );

        TestAssertions.AssertWorkflowStep("Injected text matches transcription", () =>
        {
            Assert.Single(_textInjector.InjectedTexts);
            Assert.Equal("Hello, this is a test transcription.", _textInjector.InjectedTexts[0]);
        });

        Logger.LogInformation("Push-to-talk workflow test completed successfully");
    }

    [Fact]
    public async Task ToggleMode_Workflow_TranscribesAndInjectsText()
    {
        // Arrange
        var transcriptionComplete = new TaskCompletionSource<string>();
        _controller.TranscriptionComplete += (_, text) => transcriptionComplete.TrySetResult(text);
        _controller.Start();

        Logger.LogInformation("Starting toggle mode workflow test");

        // Act - Simulate toggle mode workflow
        TestAssertions.AssertWorkflowStep("First toggle press starts recording", () =>
        {
            _hotkeyService.SimulateTogglePress();
            Assert.True(_controller.IsRecording);
        });

        await Task.Delay(100); // Brief recording simulation

        TestAssertions.AssertWorkflowStep("Second toggle press stops recording", () =>
        {
            _hotkeyService.SimulateTogglePress();
            Assert.False(_controller.IsRecording);
        });

        // Wait for transcription to complete
        var result = await transcriptionComplete.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal("Hello, this is a test transcription.", result);

        Logger.LogInformation("Toggle mode workflow test completed successfully");
    }

    [Fact]
    public async Task TranscriptionFailure_RaisesErrorEvent()
    {
        // Arrange with failing transcription engine
        // Use fresh instances to avoid interference from other tests
        var failingEngine = new MockTranscriptionEngine(
            shouldFail: true,
            errorMessage: "Model not found");

        var hotkeyService = new MockHotkeyService();
        var textInjector = new MockTextInjector();

        var errorOccurred = new TaskCompletionSource<string>();

        var controller = new VoxTetherController(
            LoggerFactory.CreateLogger<VoxTetherController>(),
            _settingsService,
            _recorder,
            hotkeyService,
            failingEngine,
            textInjector,
            new NoOpTextPostProcessor()
        );

        controller.ErrorOccurred += (_, error) => errorOccurred.TrySetResult(error);
        controller.Start();

        Logger.LogInformation("Starting transcription failure test");

        // Act
        hotkeyService.SimulatePushToTalkPress();
        await Task.Delay(50);
        hotkeyService.SimulatePushToTalkRelease();

        // Assert
        var error = await errorOccurred.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Model not found", error);
        Assert.Empty(textInjector.InjectedTexts); // No text should be injected on failure

        Logger.LogInformation("Transcription failure test completed successfully");

        controller.Stop();
        hotkeyService.Dispose();
    }

    [Fact]
    public void Controller_Start_EnablesHotkeyService()
    {
        // Act
        _controller.Start();

        // Assert
        Assert.True(_hotkeyService.IsRunning);
    }

    [Fact]
    public void Controller_Stop_DisablesHotkeyService()
    {
        // Arrange
        _controller.Start();
        Assert.True(_hotkeyService.IsRunning);

        // Act
        _controller.Stop();

        // Assert
        Assert.False(_hotkeyService.IsRunning);
    }

    [Fact]
    public async Task MultipleRecordings_EachGetTranscribed()
    {
        // Arrange
        var transcriptions = new List<string>();
        _controller.TranscriptionComplete += (_, text) => transcriptions.Add(text);
        _controller.Start();

        Logger.LogInformation("Starting multiple recordings test");

        // Act - First recording
        _hotkeyService.SimulatePushToTalkPress();
        await Task.Delay(50);
        _hotkeyService.SimulatePushToTalkRelease();

        // Wait for first transcription
        await TestAssertions.AssertEventuallyAsync(
            () => transcriptions.Count >= 1,
            timeout: TimeSpan.FromSeconds(5),
            description: "First transcription completed"
        );

        // Act - Second recording
        _hotkeyService.SimulatePushToTalkPress();
        await Task.Delay(50);
        _hotkeyService.SimulatePushToTalkRelease();

        // Wait for second transcription
        await TestAssertions.AssertEventuallyAsync(
            () => transcriptions.Count >= 2,
            timeout: TimeSpan.FromSeconds(5),
            description: "Second transcription completed"
        );

        // Assert
        Assert.Equal(2, transcriptions.Count);
        Assert.All(transcriptions, t => Assert.Equal("Hello, this is a test transcription.", t));

        Logger.LogInformation("Multiple recordings test completed successfully");
    }

    public void Dispose()
    {
        _controller.Stop();
        _recorder.Dispose();
        _hotkeyService.Dispose();
        TestResources.CleanupTempDirectory(_tempDir);
    }
}
