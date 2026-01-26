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
    private readonly IAudioRecorder? _audioRecorder;
    private readonly HashSet<Key> _pressedKeys = new();
    private bool _isCapturingHotkey;
    private bool _isCapturingToggleHotkey;
    private bool _isDownloading;
    private bool _isTestingMicrophone;
    private System.Windows.Threading.DispatcherTimer? _testTimer;

    public SettingsWindow(SettingsService settingsService, IAudioRecorder? audioRecorder = null)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _audioRecorder = audioRecorder;
        _downloadService = new ModelDownloadService();
        _downloadService.DownloadProgressChanged += OnDownloadProgressChanged;
        _downloadService.StatusChanged += OnDownloadStatusChanged;
        
        if (_audioRecorder != null)
        {
            _audioRecorder.AudioLevelChanged += OnAudioLevelChanged;
        }
        
        LoadSettings();
        LoadModelCatalog();
        LoadMicrophones();
        
        // Dispose the download service when the window is closed
        Closed += (s, e) =>
        {
            StopMicrophoneTest();
            _downloadService.DownloadProgressChanged -= OnDownloadProgressChanged;
            _downloadService.StatusChanged -= OnDownloadStatusChanged;
            if (_audioRecorder != null)
            {
                _audioRecorder.AudioLevelChanged -= OnAudioLevelChanged;
            }
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

        // Toggle Hotkey
        ToggleHotkeyTextBox.Text = settings.ToggleHotkey;
        ToggleHotkeyTextBox.GotFocus += ToggleHotkeyTextBox_GotFocus;
        ToggleHotkeyTextBox.LostFocus += ToggleHotkeyTextBox_LostFocus;
        ToggleHotkeyTextBox.PreviewKeyDown += ToggleHotkeyTextBox_PreviewKeyDown;
        ToggleHotkeyTextBox.PreviewKeyUp += ToggleHotkeyTextBox_PreviewKeyUp;

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

        // Audio Recording
        SaveAudioRecordingsCheckBox.IsChecked = settings.SaveAudioRecordings;
        AudioSavePathTextBox.Text = settings.AudioSavePath ?? SettingsService.AudioRecordingsPath;
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

    private void ToggleHotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingToggleHotkey = true;
        _pressedKeys.Clear();
        ToggleHotkeyTextBox.Text = "Press keys...";
    }

    private void ToggleHotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingToggleHotkey = false;
        if (ToggleHotkeyTextBox.Text == "Press keys...")
        {
            ToggleHotkeyTextBox.Text = _settingsService.Settings.ToggleHotkey;
        }
    }

    private void ToggleHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingToggleHotkey) return;

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        
        _pressedKeys.Add(key);
        UpdateToggleHotkeyDisplay();
    }

    private void ToggleHotkeyTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isCapturingToggleHotkey) return;

        e.Handled = true;

        // When user releases keys, finalize the hotkey
        if (_pressedKeys.Count > 0)
        {
            _isCapturingToggleHotkey = false;
            Keyboard.ClearFocus();
        }
    }

    private void UpdateToggleHotkeyDisplay()
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

        ToggleHotkeyTextBox.Text = string.Join(" + ", parts);
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to save audio recordings",
            UseDescriptionForTitle = true,
            SelectedPath = AudioSavePathTextBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AudioSavePathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void LoadMicrophones()
    {
        MicrophoneComboBox.Items.Clear();
        
        if (_audioRecorder == null)
        {
            MicrophoneComboBox.Items.Add(new ComboBoxItem
            {
                Content = "(Audio recorder not available)",
                Tag = -1
            });
            MicrophoneComboBox.SelectedIndex = 0;
            TestMicrophoneButton.IsEnabled = false;
            return;
        }

        var devices = _audioRecorder.GetAvailableDevices();
        var settings = _settingsService.Settings;

        if (devices.Count == 0)
        {
            MicrophoneComboBox.Items.Add(new ComboBoxItem
            {
                Content = "(No microphones found)",
                Tag = -1
            });
            MicrophoneComboBox.SelectedIndex = 0;
            TestMicrophoneButton.IsEnabled = false;
            return;
        }

        // Add default option
        var defaultItem = new ComboBoxItem
        {
            Content = "System Default",
            Tag = -1
        };
        MicrophoneComboBox.Items.Add(defaultItem);

        // Add all devices
        foreach (var (deviceId, deviceName) in devices)
        {
            var item = new ComboBoxItem
            {
                Content = deviceName,
                Tag = deviceId
            };
            MicrophoneComboBox.Items.Add(item);
            
            if (deviceId == settings.SelectedMicrophoneDeviceId)
            {
                MicrophoneComboBox.SelectedItem = item;
            }
        }

        // Select default if nothing matched
        if (MicrophoneComboBox.SelectedItem == null)
        {
            MicrophoneComboBox.SelectedIndex = 0;
        }
    }

    private void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_audioRecorder == null) return;
        
        if (_isTestingMicrophone)
        {
            StopMicrophoneTest();
            return;
        }

        StartMicrophoneTest();
    }

    private void StopTestButton_Click(object sender, RoutedEventArgs e)
    {
        StopMicrophoneTest();
    }

    private void StartMicrophoneTest()
    {
        if (_audioRecorder == null) return;

        try
        {
            _isTestingMicrophone = true;
            
            // Set the selected device
            if (MicrophoneComboBox.SelectedItem is ComboBoxItem micItem && micItem.Tag is int deviceId)
            {
                _audioRecorder.SelectedDeviceId = deviceId;
            }

            // Show UI
            TestMicrophoneButton.Content = "Testing...";
            TestMicrophoneButton.IsEnabled = false;
            StopTestButton.Visibility = Visibility.Visible;
            AudioLevelPanel.Visibility = Visibility.Visible;
            AudioLevelMeter.Value = 0;
            AudioLevelText.Text = "Speak into the microphone...";

            // Create a temp file for recording with unique name
            var tempPath = Path.Combine(SettingsService.TempPath, $"mic_test_{Guid.NewGuid():N}.wav");
            _audioRecorder.StartRecording(tempPath);

            // Auto-stop after 10 seconds
            _testTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _testTimer.Tick += (s, e) => StopMicrophoneTest();
            _testTimer.Start();
        }
        catch (Exception ex)
        {
            _isTestingMicrophone = false;
            MessageBox.Show($"Failed to test microphone: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ResetMicrophoneTestUI();
        }
    }

    private void StopMicrophoneTest()
    {
        if (!_isTestingMicrophone) return;

        _isTestingMicrophone = false;
        _testTimer?.Stop();
        _testTimer = null;

        try
        {
            if (_audioRecorder?.IsRecording == true)
            {
                var path = _audioRecorder.StopRecording();
                
                // Delete the temp file
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // Ignore file cleanup errors - file may still be in use
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore permission errors during cleanup
                    }
                }
            }
        }
        catch
        {
            // Ignore errors during stop
        }

        ResetMicrophoneTestUI();
    }

    private void ResetMicrophoneTestUI()
    {
        TestMicrophoneButton.Content = "Test Microphone";
        TestMicrophoneButton.IsEnabled = true;
        StopTestButton.Visibility = Visibility.Collapsed;
        AudioLevelPanel.Visibility = Visibility.Collapsed;
        AudioLevelMeter.Value = 0;
    }

    private void OnAudioLevelChanged(object? sender, int level)
    {
        Dispatcher.Invoke(() =>
        {
            if (_isTestingMicrophone)
            {
                AudioLevelMeter.Value = level;
                
                // Update text based on level
                if (level < 5)
                {
                    AudioLevelText.Text = "Speak into the microphone...";
                }
                else if (level < 30)
                {
                    AudioLevelText.Text = "Detecting audio ▪";
                }
                else if (level < 60)
                {
                    AudioLevelText.Text = "Good level ▪▪";
                }
                else
                {
                    AudioLevelText.Text = "Strong signal ▪▪▪";
                }
            }
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Update(settings =>
        {
            settings.Hotkey = HotkeyTextBox.Text;
            settings.ToggleHotkey = ToggleHotkeyTextBox.Text;
            
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

            // Audio recording settings
            settings.SaveAudioRecordings = SaveAudioRecordingsCheckBox.IsChecked ?? false;
            settings.AudioSavePath = string.IsNullOrWhiteSpace(AudioSavePathTextBox.Text) 
                ? null 
                : AudioSavePathTextBox.Text;
            
            // Microphone selection
            if (MicrophoneComboBox.SelectedItem is ComboBoxItem micItem && micItem.Tag is int deviceId)
            {
                settings.SelectedMicrophoneDeviceId = deviceId;
            }
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
