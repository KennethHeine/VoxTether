using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Low-level keyboard hook for global hotkey detection.
/// </summary>
public class LowLevelHookHotkeyService : IHotkeyService
{
    private readonly ILogger<LowLevelHookHotkeyService> _logger;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private HashSet<int> _pressedKeys = new();
    private string? _registeredHotkey;
    private HashSet<int> _hotkeyKeys = new();
    private Action? _onPressed;
    private Action? _onReleased;
    private bool _hotkeyActive = false;
    private bool _disposed;

    public LowLevelHookHotkeyService(ILogger<LowLevelHookHotkeyService> logger)
    {
        _logger = logger;
    }

    public string? CurrentHotkey => _registeredHotkey;
    public bool IsCapturing { get; private set; }

    public event EventHandler<string>? HotkeyCaptured;

    public bool RegisterPushToTalk(string hotkey, Action onPressed, Action onReleased)
    {
        try
        {
            UnregisterAll();

            _registeredHotkey = hotkey;
            _hotkeyKeys = ParseHotkey(hotkey);
            _onPressed = onPressed;
            _onReleased = onReleased;

            InstallHook();

            _logger.LogInformation("Registered hotkey: {Hotkey}", hotkey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkey: {Hotkey}", hotkey);
            return false;
        }
    }

    public void UnregisterAll()
    {
        UninstallHook();
        _registeredHotkey = null;
        _hotkeyKeys.Clear();
        _onPressed = null;
        _onReleased = null;
        _hotkeyActive = false;
    }

    public void StartCapture()
    {
        IsCapturing = true;
        _pressedKeys.Clear();
    }

    public void StopCapture()
    {
        IsCapturing = false;
        _pressedKeys.Clear();
    }

    private void InstallHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule.ModuleName), 0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to install keyboard hook: {Marshal.GetLastWin32Error()}");
        }

        _logger.LogDebug("Keyboard hook installed");
    }

    private void UninstallHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _logger.LogDebug("Keyboard hook uninstalled");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

            if (isKeyDown)
            {
                _pressedKeys.Add(vkCode);

                if (IsCapturing)
                {
                    // Build hotkey string from pressed keys
                    var hotkeyString = BuildHotkeyString(_pressedKeys);
                    if (!string.IsNullOrEmpty(hotkeyString))
                    {
                        HotkeyCaptured?.Invoke(this, hotkeyString);
                    }
                }
                else if (!_hotkeyActive && IsHotkeyPressed())
                {
                    _hotkeyActive = true;
                    _logger.LogDebug("Hotkey pressed");
                    Task.Run(() => _onPressed?.Invoke());
                }
            }
            else if (isKeyUp)
            {
                _pressedKeys.Remove(vkCode);

                if (!IsCapturing && _hotkeyActive && !IsHotkeyPressed())
                {
                    _hotkeyActive = false;
                    _logger.LogDebug("Hotkey released");
                    Task.Run(() => _onReleased?.Invoke());
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsHotkeyPressed()
    {
        return _hotkeyKeys.Count > 0 && _hotkeyKeys.All(k => _pressedKeys.Contains(k));
    }

    private static HashSet<int> ParseHotkey(string hotkey)
    {
        var keys = new HashSet<int>();
        var parts = hotkey.Split('+').Select(p => p.Trim().ToLowerInvariant());

        foreach (var part in parts)
        {
            int vk = part switch
            {
                "ctrl" or "control" => VK_CONTROL,
                "alt" => VK_MENU,
                "shift" => VK_SHIFT,
                "win" or "windows" => VK_LWIN,
                "space" => VK_SPACE,
                "enter" or "return" => VK_RETURN,
                "tab" => VK_TAB,
                "escape" or "esc" => VK_ESCAPE,
                "backspace" => VK_BACK,
                "delete" => VK_DELETE,
                "insert" => VK_INSERT,
                "home" => VK_HOME,
                "end" => VK_END,
                "pageup" => VK_PRIOR,
                "pagedown" => VK_NEXT,
                "up" => VK_UP,
                "down" => VK_DOWN,
                "left" => VK_LEFT,
                "right" => VK_RIGHT,
                "f1" => VK_F1, "f2" => VK_F2, "f3" => VK_F3, "f4" => VK_F4,
                "f5" => VK_F5, "f6" => VK_F6, "f7" => VK_F7, "f8" => VK_F8,
                "f9" => VK_F9, "f10" => VK_F10, "f11" => VK_F11, "f12" => VK_F12,
                _ when part.Length == 1 => char.ToUpperInvariant(part[0]),
                _ => 0
            };

            if (vk != 0)
            {
                keys.Add(vk);
            }
        }

        return keys;
    }

    private static string BuildHotkeyString(HashSet<int> keys)
    {
        var parts = new List<string>();

        if (keys.Contains(VK_CONTROL) || keys.Contains(VK_LCONTROL) || keys.Contains(VK_RCONTROL))
            parts.Add("Ctrl");
        if (keys.Contains(VK_MENU) || keys.Contains(VK_LMENU) || keys.Contains(VK_RMENU))
            parts.Add("Alt");
        if (keys.Contains(VK_SHIFT) || keys.Contains(VK_LSHIFT) || keys.Contains(VK_RSHIFT))
            parts.Add("Shift");
        if (keys.Contains(VK_LWIN) || keys.Contains(VK_RWIN))
            parts.Add("Win");

        foreach (var key in keys)
        {
            var name = key switch
            {
                VK_SPACE => "Space",
                VK_RETURN => "Enter",
                VK_TAB => "Tab",
                VK_ESCAPE => "Escape",
                >= 0x41 and <= 0x5A => ((char)key).ToString(),
                >= VK_F1 and <= VK_F12 => $"F{key - VK_F1 + 1}",
                _ => null
            };

            if (name != null && !parts.Contains(name))
            {
                parts.Add(name);
            }
        }

        return string.Join("+", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UninstallHook();
        GC.SuppressFinalize(this);
    }

    #region Win32 Interop

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_SPACE = 0x20;
    private const int VK_RETURN = 0x0D;
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_BACK = 0x08;
    private const int VK_DELETE = 0x2E;
    private const int VK_INSERT = 0x2D;
    private const int VK_HOME = 0x24;
    private const int VK_END = 0x23;
    private const int VK_PRIOR = 0x21;
    private const int VK_NEXT = 0x22;
    private const int VK_UP = 0x26;
    private const int VK_DOWN = 0x28;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;
    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;
    private const int VK_F3 = 0x72;
    private const int VK_F4 = 0x73;
    private const int VK_F5 = 0x74;
    private const int VK_F6 = 0x75;
    private const int VK_F7 = 0x76;
    private const int VK_F8 = 0x77;
    private const int VK_F9 = 0x78;
    private const int VK_F10 = 0x79;
    private const int VK_F11 = 0x7A;
    private const int VK_F12 = 0x7B;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion
}
