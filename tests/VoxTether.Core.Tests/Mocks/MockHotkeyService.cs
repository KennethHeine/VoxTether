using VoxTether.Core.Interfaces;

namespace VoxTether.Core.Tests.Mocks;

/// <summary>
/// Mock implementation of IHotkeyService for testing.
/// Allows programmatic triggering of hotkey events.
/// </summary>
public class MockHotkeyService : IHotkeyService
{
    public HotkeyCombination Hotkey { get; set; } = HotkeyCombination.Default;
    public HotkeyCombination ToggleHotkey { get; set; } = HotkeyCombination.DefaultToggle;
    public bool IsRunning { get; private set; }
    public bool IsPressed { get; private set; }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
    public event EventHandler? ToggleHotkeyPressed;

    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;

    // Methods to simulate user input in tests
    public void SimulatePushToTalkPress()
    {
        IsPressed = true;
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    public void SimulatePushToTalkRelease()
    {
        IsPressed = false;
        HotkeyReleased?.Invoke(this, EventArgs.Empty);
    }

    public void SimulateTogglePress()
        => ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}
