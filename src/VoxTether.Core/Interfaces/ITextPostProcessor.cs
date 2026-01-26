namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for post-processing transcribed text.
/// In V1, only a no-op implementation is provided.
/// V2 will add optional LLM-based post-processing.
/// </summary>
public interface ITextPostProcessor
{
    /// <summary>
    /// Processes the transcribed text.
    /// </summary>
    /// <param name="text">The input text to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed text.</returns>
    Task<string> ProcessAsync(string text, CancellationToken cancellationToken = default);
}
