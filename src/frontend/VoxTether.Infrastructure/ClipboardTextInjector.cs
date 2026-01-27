using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;

namespace VoxTether.Infrastructure;

/// <summary>
/// Text injector using clipboard and Windows SendInput.
/// </summary>
public class ClipboardTextInjector : ITextInjector
{
    private readonly ILogger<ClipboardTextInjector> _logger;

    public ClipboardTextInjector(ILogger<ClipboardTextInjector> logger)
    {
        _logger = logger;
    }

    public TextInjectionMode Mode { get; set; } = TextInjectionMode.ClipboardAndPaste;
    public int ClipboardDelayMs { get; set; } = 50;

    public bool InjectText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Text is empty");
            return false;
        }

        try
        {
            switch (Mode)
            {
                case TextInjectionMode.Clipboard:
                    SetClipboardText(text);
                    _logger.LogInformation("Text copied to clipboard ({Length} chars)", text.Length);
                    return true;

                case TextInjectionMode.ClipboardAndPaste:
                    SetClipboardText(text);
                    Thread.Sleep(ClipboardDelayMs);
                    SendPasteCommand();
                    _logger.LogInformation("Text pasted from clipboard ({Length} chars)", text.Length);
                    return true;

                case TextInjectionMode.SimulateTyping:
                    SimulateTyping(text);
                    _logger.LogInformation("Text typed ({Length} chars)", text.Length);
                    return true;

                default:
                    _logger.LogWarning("Unknown injection mode: {Mode}", Mode);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject text");
            return false;
        }
    }

    private static void SetClipboardText(string text)
    {
        // Run clipboard operation on STA thread
        var thread = new Thread(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        try
                        {
                            EmptyClipboard();
                            
                            var hGlobal = Marshal.StringToHGlobalUni(text);
                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                            return;
                        }
                        finally
                        {
                            CloseClipboard();
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(1000);
    }

    private static void SendPasteCommand()
    {
        // Send Ctrl+V
        var inputs = new INPUT[4];

        // Ctrl down
        inputs[0] = CreateKeyInput(VK_CONTROL, KeyEventFlags.None);
        // V down
        inputs[1] = CreateKeyInput(VK_V, KeyEventFlags.None);
        // V up
        inputs[2] = CreateKeyInput(VK_V, KeyEventFlags.KeyUp);
        // Ctrl up
        inputs[3] = CreateKeyInput(VK_CONTROL, KeyEventFlags.KeyUp);

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SimulateTyping(string text)
    {
        foreach (char c in text)
        {
            var inputs = new INPUT[2];
            
            inputs[0] = CreateUnicodeInput(c, KeyEventFlags.Unicode);
            inputs[1] = CreateUnicodeInput(c, KeyEventFlags.Unicode | KeyEventFlags.KeyUp);
            
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            Thread.Sleep(5); // Small delay between characters
        }
    }

    private static INPUT CreateKeyInput(ushort vkCode, KeyEventFlags flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = 0,
                    dwFlags = (uint)flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static INPUT CreateUnicodeInput(char c, KeyEventFlags flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = (uint)flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    #region Win32 Interop

    private const uint INPUT_KEYBOARD = 1;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint CF_UNICODETEXT = 13;

    [Flags]
    private enum KeyEventFlags : uint
    {
        None = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        Scancode = 0x0008
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    #endregion
}
