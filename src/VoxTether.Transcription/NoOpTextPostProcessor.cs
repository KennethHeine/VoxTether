using VoxTether.Core.Interfaces;

namespace VoxTether.Transcription;

/// <summary>
/// No-op text post-processor for V1.
/// Simply returns the input text unchanged.
/// </summary>
public class NoOpTextPostProcessor : ITextPostProcessor
{
    /// <summary>
    /// Returns the input text unchanged.
    /// </summary>
    public Task<string> ProcessAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(text);
    }
}
