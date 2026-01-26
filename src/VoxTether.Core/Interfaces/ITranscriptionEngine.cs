namespace VoxTether.Core.Interfaces;

/// <summary>
/// Options for transcription.
/// </summary>
public class TranscriptionOptions
{
    /// <summary>
    /// Path to the model file.
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Language code for transcription (e.g., "en", "auto").
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Whether to translate to English.
    /// </summary>
    public bool Translate { get; set; } = false;
}

/// <summary>
/// Result of a transcription operation.
/// </summary>
public class TranscriptionResult
{
    /// <summary>
    /// The transcribed text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the transcription was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if transcription failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of the transcription operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Interface for speech-to-text transcription.
/// </summary>
public interface ITranscriptionEngine
{
    /// <summary>
    /// Transcribes the audio from the specified WAV file.
    /// </summary>
    /// <param name="wavPath">Path to the WAV file to transcribe.</param>
    /// <param name="options">Transcription options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the transcription engine is properly configured.
    /// </summary>
    /// <returns>True if the engine is ready to transcribe.</returns>
    bool IsConfigured();

    /// <summary>
    /// Gets the path to the whisper executable.
    /// </summary>
    string? GetWhisperPath();
}
