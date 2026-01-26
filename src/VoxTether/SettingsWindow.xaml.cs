using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace VoxTether;

/// <summary>
/// View model for model versions in the UI.
/// </summary>
public class ModelVersionViewModel
{
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public int SizeMb { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SizeText => $"{SizeMb} MB";
    public bool IsDownloaded { get; set; }
    public string ButtonText => IsDownloaded ? "Downloaded" : "Download";
    public bool IsEnabled => !IsDownloaded;
}

/// <summary>
/// View model for models in the UI.
/// </summary>
public class ModelInfoViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string InfoUrl { get; set; } = string.Empty;
    public List<ModelVersionViewModel> Versions { get; set; } = new();
}

/// <summary>
/// Settings window for VoxTether configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ModelDownloadService _downloadService;
    private readonly HashSet<Key> _pressedKeys = new();
    private bool _isCapturingHotkey;
    private bool _isDownloading;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _downloadService = new ModelDownloadService();
        _downloadService.DownloadProgressChanged += OnDownloadProgressChanged;
        _downloadService.StatusChanged += OnDownloadStatusChanged;
        LoadSettings();
        LoadModelCatalog();
        
        // Dispose the download service when the window is closed
        Closed += (s, e) =>
        {
            _downloadService.DownloadProgressChanged -= OnDownloadProgressChanged;
            _downloadService.StatusChanged -= OnDownloadStatusChanged;
            _downloadService.Dispose();
        };
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;

        // Hotkey
        HotkeyTextBox.Text = settings.Hotkey;
        HotkeyTextBox.GotFocus += HotkeyTextBox_GotFocus;
        HotkeyTextBox.LostFocus += HotkeyTextBox_LostFocus;
        HotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        HotkeyTextBox.PreviewKeyUp += HotkeyTextBox_PreviewKeyUp;

        // Models
        LoadModels();

        // Language
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag?.ToString() == settings.Language)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }

        // Options
        ShowNotificationsCheckBox.IsChecked = settings.ShowNotifications;
        ShowRecordingIndicatorCheckBox.IsChecked = settings.ShowRecordingIndicator;
        FallbackToTypingCheckBox.IsChecked = settings.FallbackToTyping;
    }

    private void LoadModels()
    {
        var models = _settingsService.GetAvailableModels();
        var currentModelPath = _settingsService.GetEffectiveModelPath();

        ModelComboBox.Items.Clear();

        if (models.Count == 0)
        {
            ModelComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = "(No models found)", 
                Tag = "" 
            });
            ModelComboBox.SelectedIndex = 0;
            return;
        }

        foreach (var model in models)
        {
            var item = new ComboBoxItem
            {
                Content = Path.GetFileName(model),
                Tag = model
            };
            ModelComboBox.Items.Add(item);

            if (model == currentModelPath)
            {
                ModelComboBox.SelectedItem = item;
            }
        }

        if (ModelComboBox.SelectedItem == null && ModelComboBox.Items.Count > 0)
        {
            ModelComboBox.SelectedIndex = 0;
        }
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        _pressedKeys.Clear();
        HotkeyTextBox.Text = "Press keys...";
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = false;
        if (HotkeyTextBox.Text == "Press keys...")
        {
            HotkeyTextBox.Text = _settingsService.Settings.Hotkey;
        }
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingHotkey) return;

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        
        _pressedKeys.Add(key);
        UpdateHotkeyDisplay();
    }

    private void HotkeyTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isCapturingHotkey) return;

        e.Handled = true;

        // When user releases keys, finalize the hotkey
        if (_pressedKeys.Count > 0)
        {
            _isCapturingHotkey = false;
            Keyboard.ClearFocus();
        }
    }

    private void UpdateHotkeyDisplay()
    {
        var parts = new List<string>();

        if (_pressedKeys.Any(k => k == Key.LeftCtrl || k == Key.RightCtrl))
            parts.Add("Ctrl");
        if (_pressedKeys.Any(k => k == Key.LeftAlt || k == Key.RightAlt))
            parts.Add("Alt");
        if (_pressedKeys.Any(k => k == Key.LeftShift || k == Key.RightShift))
            parts.Add("Shift");
        if (_pressedKeys.Any(k => k == Key.LWin || k == Key.RWin))
            parts.Add("Win");

        // Add non-modifier keys
        foreach (var key in _pressedKeys)
        {
            if (key != Key.LeftCtrl && key != Key.RightCtrl &&
                key != Key.LeftAlt && key != Key.RightAlt &&
                key != Key.LeftShift && key != Key.RightShift &&
                key != Key.LWin && key != Key.RWin)
            {
                parts.Add(key.ToString());
            }
        }

        HotkeyTextBox.Text = string.Join(" + ", parts);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Update(settings =>
        {
            settings.Hotkey = HotkeyTextBox.Text;
            
            if (ModelComboBox.SelectedItem is ComboBoxItem modelItem)
            {
                settings.ModelPath = modelItem.Tag?.ToString();
                settings.ModelName = modelItem.Content?.ToString();
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem)
            {
                settings.Language = langItem.Tag?.ToString() ?? "auto";
            }

            settings.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? true;
            settings.ShowRecordingIndicator = ShowRecordingIndicatorCheckBox.IsChecked ?? true;
            settings.FallbackToTyping = FallbackToTypingCheckBox.IsChecked ?? true;
        });

        MessageBox.Show(
            "Settings saved. Restart VoxTether for hotkey changes to take effect.",
            "Settings Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadModelCatalog()
    {
        var models = ModelCatalog.GetAvailableModels();
        var viewModels = models.Select(m => new ModelInfoViewModel
        {
            Name = m.Name,
            Description = m.Description,
            Quality = m.Quality,
            Speed = m.Speed,
            InfoUrl = m.InfoUrl,
            Versions = m.Versions.Select(v => new ModelVersionViewModel
            {
                Version = v.Version,
                FileName = v.FileName,
                DownloadUrl = v.DownloadUrl,
                SizeMb = v.SizeMb,
                Description = v.Description,
                IsDownloaded = _downloadService.IsModelDownloaded(v.FileName)
            }).ToList()
        }).ToList();

        ModelCatalogList.ItemsSource = viewModels;
    }

    private void AllModelsLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ModelCatalog.ModelsInfoUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            MessageBox.Show("A download is already in progress.", "Download In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is System.Windows.Controls.Button button && button.Tag is ModelVersionViewModel versionVm)
        {
            _isDownloading = true;
            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadStatusText.Text = $"Starting download: {versionVm.FileName}...";

            try
            {
                var modelVersion = new ModelVersion
                {
                    Version = versionVm.Version,
                    FileName = versionVm.FileName,
                    DownloadUrl = versionVm.DownloadUrl,
                    SizeMb = versionVm.SizeMb,
                    Description = versionVm.Description
                };

                var success = await _downloadService.DownloadModelAsync(modelVersion);

                if (success)
                {
                    MessageBox.Show(
                        $"Model '{versionVm.FileName}' downloaded successfully!\n\nYou can now select it from the model dropdown.",
                        "Download Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Refresh the model lists
                    LoadModels();
                    LoadModelCatalog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDownloading = false;
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadService.CancelDownload();
    }

    private void OnDownloadProgressChanged(int progress)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgressBar.Value = progress;
        });
    }

    private void OnDownloadStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadStatusText.Text = status;
        });
    }
}
