using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Text injector that uses clipboard paste as primary method
/// and falls back to SendInput typing if needed.
/// </summary>
public class ClipboardTextInjector : ITextInjector
{
    private readonly ILogger<ClipboardTextInjector> _logger;
    private readonly int _clipboardDelayMs;

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

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
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    public ClipboardTextInjector(ILogger<ClipboardTextInjector> logger, int clipboardDelayMs = 100)
    {
        _logger = logger;
        _clipboardDelayMs = clipboardDelayMs;
    }

    public async Task<bool> InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Empty text, nothing to inject");
            return true;
        }

        // Check for password field
        if (IsPasswordField())
        {
            _logger.LogWarning("Detected password field, skipping injection");
            return false;
        }

        // Wait for hotkey release and window focus stabilization
        // This delay is important because:
        // 1. The user just released the hotkey, and we need to ensure all keys are released
        // 2. The target application needs time to properly receive focus
        await Task.Delay(150, cancellationToken);

        // Verify there's a foreground window to inject into
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            _logger.LogWarning("No foreground window found, cannot inject text");
            return false;
        }

        // Try clipboard paste first
        bool success = await TryClipboardPaste(text, cancellationToken);
        
        if (!success)
        {
            _logger.LogWarning("Clipboard paste failed, falling back to SendInput typing");
            success = await TrySendInputTyping(text, cancellationToken);
        }

        return success;
    }

    private async Task<bool> TryClipboardPaste(string text, CancellationToken cancellationToken)
    {
        string? savedClipboard = null;
        bool hadClipboard = false;

        try
        {
            // Check if WPF Application dispatcher is available
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher == null)
            {
                _logger.LogWarning("WPF Application dispatcher not available, cannot use clipboard paste");
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

            // Send Ctrl+V
            SendCtrlV();

            _logger.LogInformation("Text injected via clipboard paste ({Length} chars)", text.Length);

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
            _logger.LogError(ex, "Clipboard paste failed");
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

    private async Task<bool> TrySendInputTyping(string text, CancellationToken cancellationToken)
    {
        try
        {
            foreach (char c in text)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                var inputs = new INPUT[]
                {
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new INPUTUNION
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE
                            }
                        }
                    },
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new INPUTUNION
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                            }
                        }
                    }
                };

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
                await Task.Delay(5, cancellationToken); // Small delay between characters
            }

            _logger.LogInformation("Text injected via SendInput typing ({Length} chars)", text.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendInput typing failed");
            return false;
        }
    }

    public bool IsPasswordField()
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            // Get the focused control's class name
            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(foregroundWindow, out _);

            // Attach to thread to get focus
            AttachThreadInput(currentThreadId, targetThreadId, true);
            
            var focusedWindow = GetFocus();
            
            AttachThreadInput(currentThreadId, targetThreadId, false);

            if (focusedWindow == IntPtr.Zero)
                focusedWindow = foregroundWindow;

            var className = new System.Text.StringBuilder(256);
            GetClassName(focusedWindow, className, 256);
            
            var classNameStr = className.ToString().ToLowerInvariant();
            
            // Heuristic: password fields often have these class names
            if (classNameStr.Contains("password") || 
                classNameStr.Contains("secret"))
            {
                return true;
            }

            // Check for common edit controls that might be password fields
            // This is a best-effort heuristic
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for password field");
            return false;
        }
    }
}
