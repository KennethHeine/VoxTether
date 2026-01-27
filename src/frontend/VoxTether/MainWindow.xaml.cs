using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VoxTether;

/// <summary>
/// Main settings window for VoxTether.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        // Set window title
        Title = "VoxTether Settings";
        
        // Handle window closing - minimize to tray instead of closing
        this.Closed += MainWindow_Closed;
        
        // Navigate to General page by default
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(Views.GeneralSettingsPage));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Don't actually close, just hide
        args.Handled = true;
        this.Hide();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            
            var pageType = tag switch
            {
                "General" => typeof(Views.GeneralSettingsPage),
                "Audio" => typeof(Views.AudioSettingsPage),
                "Models" => typeof(Views.ModelsPage),
                "About" => typeof(Views.AboutPage),
                _ => typeof(Views.GeneralSettingsPage)
            };
            
            ContentFrame.Navigate(pageType);
        }
    }

    /// <summary>
    /// Shows the window and activates it.
    /// </summary>
    public void Show()
    {
        this.Activate();
    }

    /// <summary>
    /// Hides the window.
    /// </summary>
    public void Hide()
    {
        // WinUI 3 doesn't have a built-in Hide method, so we minimize
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PInvoke.User32.ShowWindow(hwnd, PInvoke.User32.ShowWindowCommand.SW_HIDE);
    }
}

/// <summary>
/// P/Invoke for window operations.
/// </summary>
internal static partial class PInvoke
{
    internal static class User32
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

        internal enum ShowWindowCommand
        {
            SW_HIDE = 0,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_RESTORE = 9
        }
    }
}
