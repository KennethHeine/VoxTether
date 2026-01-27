namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for text injection into applications.
/// </summary>
public interface ITextInjector
{
    /// <summary>
    /// Injects text at the current cursor position.
    /// </summary>
    /// <param name="text">The text to inject.</param>
    /// <returns>True if injection was successful.</returns>
    bool InjectText(string text);

    /// <summary>
    /// Gets or sets the injection mode.
    /// </summary>
    TextInjectionMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the delay in milliseconds before pasting.
    /// </summary>
    int ClipboardDelayMs { get; set; }
}

/// <summary>
/// Text injection modes.
/// </summary>
public enum TextInjectionMode
{
    /// <summary>
    /// Copy to clipboard only.
    /// </summary>
    Clipboard,

    /// <summary>
    /// Copy to clipboard and paste.
    /// </summary>
    ClipboardAndPaste,

    /// <summary>
    /// Simulate typing.
    /// </summary>
    SimulateTyping
}
