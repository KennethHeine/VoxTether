using System.Text.Json.Serialization;

namespace VoxTether.Core.Models;

/// <summary>
/// Application settings.
/// </summary>
public class VoxTetherSettings
{
    /// <summary>
    /// The hotkey for push-to-talk.
    /// </summary>
    public string Hotkey { get; set; } = "Ctrl+Shift+Space";

    /// <summary>
    /// The selected model name.
    /// </summary>
    public string ModelName { get; set; } = "small";

    /// <summary>
    /// The language for transcription.
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// The output mode for text injection.
    /// </summary>
    public string OutputMode { get; set; } = "ClipboardAndPaste";

    /// <summary>
    /// Whether to show notifications.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Whether to show the recording indicator overlay.
    /// </summary>
    public bool ShowRecordingIndicator { get; set; } = true;

    /// <summary>
    /// The selected audio device ID.
    /// </summary>
    public int AudioDeviceId { get; set; } = -1;

    /// <summary>
    /// Delay in milliseconds before pasting from clipboard.
    /// </summary>
    public int ClipboardDelayMs { get; set; } = 50;

    /// <summary>
    /// Whether the first run setup has been completed.
    /// </summary>
    public bool FirstRunCompleted { get; set; } = false;

    /// <summary>
    /// The backend server port.
    /// </summary>
    public int BackendPort { get; set; } = 5678;

    /// <summary>
    /// Whether to start the application minimized.
    /// </summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// Whether to start with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// The application theme.
    /// </summary>
    public string Theme { get; set; } = "System";
}
