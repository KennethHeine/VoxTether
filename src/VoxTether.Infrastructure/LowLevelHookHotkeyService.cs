using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Low-level keyboard hook implementation for global hotkey detection.
/// Supports hold-to-talk functionality with debouncing.
/// </summary>
public class LowLevelHookHotkeyService : IHotkeyService
{
    private readonly ILogger<LowLevelHookHotkeyService> _logger;
    private readonly object _lock = new();
    private readonly HashSet<Key> _pressedKeys = new();
    
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _disposed;
    private bool _isPressed;
    private bool _toggleHotkeyWasPressed;
    private DateTime _lastPressTime = DateTime.MinValue;
    private DateTime _lastTogglePressTime = DateTime.MinValue;
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(50);

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
    public event EventHandler? ToggleHotkeyPressed;
    
    public HotkeyCombination Hotkey { get; set; } = HotkeyCombination.Default;
    public HotkeyCombination ToggleHotkey { get; set; } = HotkeyCombination.DefaultToggle;
    public bool IsPressed => _isPressed;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public LowLevelHookHotkeyService(ILogger<LowLevelHookHotkeyService> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            _logger.LogWarning("Hotkey service already started");
            return;
        }

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        
        if (curModule == null)
        {
            _logger.LogError("Could not get main module for hook");
            return;
        }

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        
        if (_hookId == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("Failed to set keyboard hook, error: {Error}", error);
        }
        else
        {
            _logger.LogInformation("Hotkey service started, listening for {Hotkey}", Hotkey);
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _logger.LogInformation("Hotkey service stopped");
        }

        lock (_lock)
        {
            _pressedKeys.Clear();
            _isPressed = false;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)hookStruct.vkCode);
            var message = (int)wParam;

            bool isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
            bool isKeyUp = message == WM_KEYUP || message == WM_SYSKEYUP;

            if (isKeyDown)
            {
                HandleKeyDown(key);
            }
            else if (isKeyUp)
            {
                HandleKeyUp(key);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void HandleKeyDown(Key key)
    {
        lock (_lock)
        {
            // Normalize modifier keys (left/right variants)
            var normalizedKey = NormalizeKey(key);
            _pressedKeys.Add(normalizedKey);
            _pressedKeys.Add(key);

            // Check if toggle hotkey combo is fully pressed
            if (!_toggleHotkeyWasPressed && IsToggleHotkeyComboPressed())
            {
                var now = DateTime.UtcNow;
                if (now - _lastTogglePressTime >= _debounceTime)
                {
                    _toggleHotkeyWasPressed = true;
                    _lastTogglePressTime = now;
                    _logger.LogDebug("Toggle hotkey pressed");
                    Task.Run(() => ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty));
                }
            }

            // Check if hotkey combo is fully pressed
            if (!_isPressed && IsHotkeyComboPressed())
            {
                var now = DateTime.UtcNow;
                if (now - _lastPressTime >= _debounceTime)
                {
                    _isPressed = true;
                    _lastPressTime = now;
                    _logger.LogDebug("Hotkey pressed");
                    Task.Run(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                }
            }
        }
    }

    private void HandleKeyUp(Key key)
    {
        lock (_lock)
        {
            var normalizedKey = NormalizeKey(key);
            _pressedKeys.Remove(normalizedKey);
            _pressedKeys.Remove(key);

            // Reset toggle hotkey state when keys are released
            if (_toggleHotkeyWasPressed && !IsToggleHotkeyComboPressed())
            {
                _toggleHotkeyWasPressed = false;
            }

            // If we were pressed and combo is no longer fully pressed
            if (_isPressed && !IsHotkeyComboPressed())
            {
                _isPressed = false;
                _logger.LogDebug("Hotkey released");
                Task.Run(() => HotkeyReleased?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    private bool IsHotkeyComboPressed()
    {
        return IsComboPressed(Hotkey);
    }

    private bool IsToggleHotkeyComboPressed()
    {
        return IsComboPressed(ToggleHotkey);
    }

    private bool IsComboPressed(HotkeyCombination combo)
    {
        // Check if all keys in the hotkey are pressed
        var allKeys = combo.AllKeys;
        
        foreach (var requiredKey in allKeys)
        {
            var normalizedRequired = NormalizeKey(requiredKey);
            if (!_pressedKeys.Contains(requiredKey) && !_pressedKeys.Contains(normalizedRequired))
            {
                // Check for left/right variants
                if (!CheckVariants(requiredKey))
                {
                    return false;
                }
            }
        }
        
        return true;
    }

    private bool CheckVariants(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => _pressedKeys.Contains(Key.LeftCtrl) || _pressedKeys.Contains(Key.RightCtrl),
            Key.LeftAlt or Key.RightAlt => _pressedKeys.Contains(Key.LeftAlt) || _pressedKeys.Contains(Key.RightAlt),
            Key.LeftShift or Key.RightShift => _pressedKeys.Contains(Key.LeftShift) || _pressedKeys.Contains(Key.RightShift),
            Key.LWin or Key.RWin => _pressedKeys.Contains(Key.LWin) || _pressedKeys.Contains(Key.RWin),
            _ => false
        };
    }

    private static Key NormalizeKey(Key key)
    {
        return key switch
        {
            Key.RightCtrl => Key.LeftCtrl,
            Key.RightAlt => Key.LeftAlt,
            Key.RightShift => Key.LeftShift,
            Key.RWin => Key.LWin,
            _ => key
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
        GC.SuppressFinalize(this);
    }
}
