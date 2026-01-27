using CommunityToolkit.Mvvm.ComponentModel;
using VoxTether.Core.Models;

namespace VoxTether.ViewModels;

/// <summary>
/// ViewModel for general settings.
/// </summary>
public partial class GeneralSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _hotkey = "Ctrl+Shift+Space";

    [ObservableProperty]
    private string _language = "auto";

    [ObservableProperty]
    private string _outputMode = "ClipboardAndPaste";

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private bool _showRecordingIndicator = true;

    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private bool _startMinimized = true;

    [ObservableProperty]
    private string _theme = "System";

    public List<string> Languages { get; } = new()
    {
        "auto",
        "en",
        "es",
        "fr",
        "de",
        "it",
        "pt",
        "nl",
        "ru",
        "zh",
        "ja",
        "ko"
    };

    public List<string> OutputModes { get; } = new()
    {
        "Clipboard",
        "ClipboardAndPaste",
        "SimulateTyping"
    };

    public List<string> Themes { get; } = new()
    {
        "System",
        "Light",
        "Dark"
    };

    public GeneralSettingsViewModel(VoxTetherSettings settings)
    {
        Hotkey = settings.Hotkey;
        Language = settings.Language;
        OutputMode = settings.OutputMode;
        ShowNotifications = settings.ShowNotifications;
        ShowRecordingIndicator = settings.ShowRecordingIndicator;
        StartWithWindows = settings.StartWithWindows;
        StartMinimized = settings.StartMinimized;
        Theme = settings.Theme;
    }

    public void ApplyTo(VoxTetherSettings settings)
    {
        settings.Hotkey = Hotkey;
        settings.Language = Language;
        settings.OutputMode = OutputMode;
        settings.ShowNotifications = ShowNotifications;
        settings.ShowRecordingIndicator = ShowRecordingIndicator;
        settings.StartWithWindows = StartWithWindows;
        settings.StartMinimized = StartMinimized;
        settings.Theme = Theme;
    }
}
