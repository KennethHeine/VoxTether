using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

/// <summary>
/// Mock implementation of ITranscriptionEngine for testing.
/// Returns predictable transcription results.
/// </summary>
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
