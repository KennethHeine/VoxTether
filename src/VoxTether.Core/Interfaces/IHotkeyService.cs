using System.Windows.Input;

namespace VoxTether.Core.Interfaces;

/// <summary>
/// Represents a hotkey combination.
/// </summary>
public class HotkeyCombination
{
    /// <summary>
    /// The set of modifier keys required.
    /// </summary>
    public HashSet<Key> Modifiers { get; set; } = new();

    /// <summary>
    /// The main key.
    /// </summary>
    public Key MainKey { get; set; } = Key.Space;

    /// <summary>
    /// Creates a default hotkey (Ctrl + Alt + Space).
    /// </summary>
    public static HotkeyCombination Default => new()
    {
        Modifiers = new HashSet<Key> { Key.LeftCtrl, Key.LeftAlt },
        MainKey = Key.Space
    };

    /// <summary>
    /// Creates a default toggle hotkey (Ctrl + Alt + T).
    /// </summary>
    public static HotkeyCombination DefaultToggle => new()
    {
        Modifiers = new HashSet<Key> { Key.LeftCtrl, Key.LeftAlt },
        MainKey = Key.T
    };

    /// <summary>
    /// Gets all keys in this combination.
    /// </summary>
    public HashSet<Key> AllKeys
    {
        get
        {
            var keys = new HashSet<Key>(Modifiers);
            keys.Add(MainKey);
            return keys;
        }
    }

    /// <summary>
    /// Returns a string representation of the hotkey.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        
        if (Modifiers.Contains(Key.LeftCtrl) || Modifiers.Contains(Key.RightCtrl))
            parts.Add("Ctrl");
        if (Modifiers.Contains(Key.LeftAlt) || Modifiers.Contains(Key.RightAlt))
            parts.Add("Alt");
        if (Modifiers.Contains(Key.LeftShift) || Modifiers.Contains(Key.RightShift))
            parts.Add("Shift");
        if (Modifiers.Contains(Key.LWin) || Modifiers.Contains(Key.RWin))
            parts.Add("Win");
            
        parts.Add(MainKey.ToString());
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Parses a hotkey string into a HotkeyCombination.
    /// </summary>
    public static HotkeyCombination Parse(string hotkeyString)
    {
        var combo = new HotkeyCombination();
        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        
        foreach (var part in parts)
        {
            var upper = part.ToUpperInvariant();
            switch (upper)
            {
                case "CTRL":
                case "CONTROL":
                    combo.Modifiers.Add(Key.LeftCtrl);
                    break;
                case "ALT":
                    combo.Modifiers.Add(Key.LeftAlt);
                    break;
                case "SHIFT":
                    combo.Modifiers.Add(Key.LeftShift);
                    break;
                case "WIN":
                case "WINDOWS":
                    combo.Modifiers.Add(Key.LWin);
                    break;
                default:
                    if (Enum.TryParse<Key>(part, true, out var key))
                    {
                        combo.MainKey = key;
                    }
                    break;
            }
        }
        
        return combo;
    }
}

/// <summary>
/// Interface for global hotkey service with hold-to-talk support.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Event raised when the hotkey combination is pressed (all keys down).
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Event raised when any key in the hotkey combination is released.
    /// </summary>
    event EventHandler? HotkeyReleased;

    /// <summary>
    /// Event raised when the toggle hotkey is pressed (toggle recording mode).
    /// </summary>
    event EventHandler? ToggleHotkeyPressed;

    /// <summary>
    /// Gets or sets the hotkey combination.
    /// </summary>
    HotkeyCombination Hotkey { get; set; }

    /// <summary>
    /// Gets or sets the toggle hotkey combination for toggle recording mode.
    /// </summary>
    HotkeyCombination ToggleHotkey { get; set; }

    /// <summary>
    /// Gets a value indicating whether the hotkey is currently pressed.
    /// </summary>
    bool IsPressed { get; }

    /// <summary>
    /// Starts listening for the hotkey.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening for the hotkey.
    /// </summary>
    void Stop();
}
