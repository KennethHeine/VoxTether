using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Text injector that can either copy text to clipboard or paste it into the focused application.
/// </summary>
public class ClipboardTextInjector : ITextInjector
{
    private readonly ILogger<ClipboardTextInjector> _logger;
    private readonly int _clipboardDelayMs;
    private bool _pasteToFocusedApp;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    public ClipboardTextInjector(ILogger<ClipboardTextInjector> logger, int clipboardDelayMs = 100)
    {
        _logger = logger;
        _clipboardDelayMs = clipboardDelayMs;
        _pasteToFocusedApp = false; // Default to clipboard only
    }

    /// <summary>
    /// Sets whether to paste text into the focused application or just copy to clipboard.
    /// </summary>
    public bool PasteToFocusedApp
    {
        get => _pasteToFocusedApp;
        set => _pasteToFocusedApp = value;
    }

    public async Task<bool> InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Empty text, nothing to process");
            return true;
        }

        if (_pasteToFocusedApp)
        {
            // Check for password field
            if (IsPasswordField())
            {
                _logger.LogWarning("Detected password field, skipping paste");
                return false;
            }

            // Wait for hotkey release and window focus stabilization
            await Task.Delay(150, cancellationToken);

            // Verify there's a foreground window to paste into
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                _logger.LogWarning("No foreground window found, copying to clipboard only");
                return await CopyToClipboardAsync(text);
            }

            // Copy to clipboard and paste into focused app
            return await CopyAndPasteAsync(text, cancellationToken);
        }
        else
        {
            // Just copy to clipboard - user can paste when ready
            return await CopyToClipboardAsync(text);
        }
    }

    private async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher == null)
            {
                _logger.LogWarning("WPF Application dispatcher not available, cannot copy to clipboard");
                return false;
            }

            bool clipboardSet = false;
            await app.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    clipboardSet = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy text to clipboard");
                }
            });

            if (clipboardSet)
            {
                _logger.LogInformation("Transcript copied to clipboard ({Length} chars)", text.Length);
            }

            return clipboardSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy to clipboard");
            return false;
        }
    }

    private async Task<bool> CopyAndPasteAsync(string text, CancellationToken cancellationToken)
    {
        string? savedClipboard = null;
        bool hadClipboard = false;

        try
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher == null)
            {
                _logger.LogWarning("WPF Application dispatcher not available, cannot paste");
                return false;
            }

            // Save current clipboard content (best effort)
            await app.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        savedClipboard = System.Windows.Clipboard.GetText();
                        hadClipboard = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not read clipboard");
                }
            });

            await Task.Delay(_clipboardDelayMs, cancellationToken);

            // Set clipboard to our text
            bool clipboardSet = false;
            await app.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    clipboardSet = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set clipboard");
                }
            });

            if (!clipboardSet)
            {
                return false;
            }

            await Task.Delay(_clipboardDelayMs, cancellationToken);

            // Send Ctrl+V to paste
            SendCtrlV();

            _logger.LogInformation("Text pasted into focused app ({Length} chars)", text.Length);

            // Wait a bit then restore clipboard
            await Task.Delay(_clipboardDelayMs * 2, cancellationToken);

            if (hadClipboard && savedClipboard != null)
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(savedClipboard);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not restore clipboard");
                    }
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paste to focused app failed");
            return false;
        }
    }

    private void SendCtrlV()
    {
        var inputs = new INPUT[]
        {
            // Press Ctrl
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = VK_CONTROL }
                }
            },
            // Press V
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = VK_V }
                }
            },
            // Release V
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP }
                }
            },
            // Release Ctrl
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP }
                }
            }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public bool IsPasswordField()
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(foregroundWindow, out _);

            AttachThreadInput(currentThreadId, targetThreadId, true);
            var focusedWindow = GetFocus();
            AttachThreadInput(currentThreadId, targetThreadId, false);

            if (focusedWindow == IntPtr.Zero)
                focusedWindow = foregroundWindow;

            var className = new System.Text.StringBuilder(256);
            GetClassName(focusedWindow, className, 256);

            var classNameStr = className.ToString().ToLowerInvariant();

            if (classNameStr.Contains("password") || classNameStr.Contains("secret"))
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for password field");
            return false;
        }
    }
}
