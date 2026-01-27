using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VoxTether.Core.Interfaces;
using VoxTether.Services;
using VoxTether.ViewModels;

namespace VoxTether.Views;

/// <summary>
/// General settings page.
/// </summary>
public sealed partial class GeneralSettingsPage : Page
{
    public GeneralSettingsViewModel ViewModel { get; }
    
    private readonly IHotkeyService _hotkeyService;
    private readonly SettingsService _settingsService;
    private bool _isCapturingHotkey;

    public GeneralSettingsPage()
    {
        _settingsService = App.Services.GetRequiredService<SettingsService>();
        _hotkeyService = App.Services.GetRequiredService<IHotkeyService>();
        
        ViewModel = new GeneralSettingsViewModel(_settingsService.Settings);
        
        this.InitializeComponent();
        
        // Subscribe to hotkey capture
        _hotkeyService.HotkeyCaptured += OnHotkeyCaptured;
    }

    private void CaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingHotkey)
        {
            _hotkeyService.StopCapture();
            _isCapturingHotkey = false;
            HotkeyTextBox.PlaceholderText = "Press to capture hotkey";
        }
        else
        {
            _hotkeyService.StartCapture();
            _isCapturingHotkey = true;
            HotkeyTextBox.PlaceholderText = "Press keys...";
            HotkeyTextBox.Text = "";
        }
    }

    private void OnHotkeyCaptured(object? sender, string hotkey)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.Hotkey = hotkey;
            HotkeyTextBox.Text = hotkey;
        });
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // Stop hotkey capture if active
        if (_isCapturingHotkey)
        {
            _hotkeyService.StopCapture();
            _isCapturingHotkey = false;
        }
        
        // Apply settings
        ViewModel.ApplyTo(_settingsService.Settings);
        _settingsService.Save();
        
        // Re-register hotkey
        var controller = App.Services.GetRequiredService<VoxTetherController>();
        controller.UpdateHotkey(_settingsService.Settings.Hotkey);
        
        // Show confirmation
        var dialog = new ContentDialog
        {
            Title = "Settings Saved",
            Content = "Your settings have been saved.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        
        await dialog.ShowAsync();
    }

    ~GeneralSettingsPage()
    {
        _hotkeyService.HotkeyCaptured -= OnHotkeyCaptured;
    }
}
