using VoxTether.Core.Interfaces;
using System.Windows.Input;

namespace VoxTether.Core.Tests;

public class HotkeyTests
{
    [Fact]
    public void HotkeyCombination_Default_IsCtrlAltSpace()
    {
        var combo = HotkeyCombination.Default;
        
        Assert.Contains(Key.LeftCtrl, combo.Modifiers);
        Assert.Contains(Key.LeftAlt, combo.Modifiers);
        Assert.Equal(Key.Space, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_Parse_CtrlAltSpace()
    {
        var combo = HotkeyCombination.Parse("Ctrl + Alt + Space");
        
        Assert.Contains(Key.LeftCtrl, combo.Modifiers);
        Assert.Contains(Key.LeftAlt, combo.Modifiers);
        Assert.Equal(Key.Space, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_Parse_ShiftF1()
    {
        var combo = HotkeyCombination.Parse("Shift + F1");
        
        Assert.Contains(Key.LeftShift, combo.Modifiers);
        Assert.Equal(Key.F1, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_Parse_WinAltP()
    {
        var combo = HotkeyCombination.Parse("Win + Alt + P");
        
        Assert.Contains(Key.LWin, combo.Modifiers);
        Assert.Contains(Key.LeftAlt, combo.Modifiers);
        Assert.Equal(Key.P, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_ToString_ReturnsFormattedString()
    {
        var combo = HotkeyCombination.Default;
        var result = combo.ToString();
        
        Assert.Contains("Ctrl", result);
        Assert.Contains("Alt", result);
        Assert.Contains("Space", result);
    }

    [Fact]
    public void HotkeyCombination_AllKeys_ContainsAllModifiersAndMainKey()
    {
        var combo = HotkeyCombination.Default;
        var allKeys = combo.AllKeys;
        
        Assert.Contains(Key.LeftCtrl, allKeys);
        Assert.Contains(Key.LeftAlt, allKeys);
        Assert.Contains(Key.Space, allKeys);
        Assert.Equal(3, allKeys.Count);
    }

    [Fact]
    public void HotkeyCombination_Parse_CaseInsensitive()
    {
        var combo = HotkeyCombination.Parse("ctrl + ALT + space");
        
        Assert.Contains(Key.LeftCtrl, combo.Modifiers);
        Assert.Contains(Key.LeftAlt, combo.Modifiers);
        Assert.Equal(Key.Space, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_Parse_ControlAlias()
    {
        var combo = HotkeyCombination.Parse("Control + A");
        
        Assert.Contains(Key.LeftCtrl, combo.Modifiers);
        Assert.Equal(Key.A, combo.MainKey);
    }

    [Fact]
    public void HotkeyCombination_Parse_WindowsAlias()
    {
        var combo = HotkeyCombination.Parse("Windows + D");
        
        Assert.Contains(Key.LWin, combo.Modifiers);
        Assert.Equal(Key.D, combo.MainKey);
    }
}
