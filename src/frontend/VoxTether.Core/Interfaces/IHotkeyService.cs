namespace VoxTether.Core.Interfaces;

/// <summary>
/// Interface for global hotkey detection.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Registers a push-to-talk hotkey.
    /// </summary>
    /// <param name="hotkey">The hotkey string (e.g., "Ctrl+Shift+Space").</param>
    /// <param name="onPressed">Callback when hotkey is pressed.</param>
    /// <param name="onReleased">Callback when hotkey is released.</param>
    /// <returns>True if registration was successful.</returns>
    bool RegisterPushToTalk(string hotkey, Action onPressed, Action onReleased);

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Gets the currently registered hotkey.
    /// </summary>
    string? CurrentHotkey { get; }

    /// <summary>
    /// Event raised when a key is pressed during hotkey capture mode.
    /// </summary>
    event EventHandler<string>? HotkeyCaptured;

    /// <summary>
    /// Starts capturing keystrokes for hotkey assignment.
    /// </summary>
    void StartCapture();

    /// <summary>
    /// Stops capturing keystrokes.
    /// </summary>
    void StopCapture();

    /// <summary>
    /// Gets a value indicating whether capture mode is active.
    /// </summary>
    bool IsCapturing { get; }
}
