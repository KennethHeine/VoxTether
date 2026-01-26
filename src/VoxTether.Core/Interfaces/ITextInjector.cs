namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for injecting text into the currently focused application.
/// </summary>
public interface ITextInjector
{
    /// <summary>
    /// Injects the specified text into the currently focused application.
    /// </summary>
    /// <param name="text">The text to inject.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if injection was successful.</returns>
    Task<bool> InjectAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current focused window is a password field.
    /// </summary>
    /// <returns>True if the current field appears to be a password field.</returns>
    bool IsPasswordField();
}
