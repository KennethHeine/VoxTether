using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

/// <summary>
/// Mock implementation of ITextInjector for testing.
/// Captures injected text for verification.
/// </summary>
public class MockTextInjector : ITextInjector
{
    public List<string> InjectedTexts { get; } = [];
    public bool ShouldSucceed { get; set; } = true;

    public Task<bool> InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        InjectedTexts.Add(text);
        return Task.FromResult(ShouldSucceed);
    }

    public bool IsPasswordField() => false;
}
