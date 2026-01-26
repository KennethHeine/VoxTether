using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace VoxTether;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly ILogger<TrayIconManager> _logger;
    private readonly SettingsService _settingsService;
    private readonly VoxTetherController _controller;
    private readonly IUpdateService _updateService;
    private readonly IAudioRecorder _audioRecorder;
    private readonly IBackendSelectionService? _backendService;
    
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _startWithWindowsMenuItem;
    private bool _disposed;

    private const string AppName = "VoxTether";
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public TrayIconManager(
        ILogger<TrayIconManager> logger,
        SettingsService settingsService,
        VoxTetherController controller,
        IUpdateService updateService,
        IAudioRecorder audioRecorder,
        IBackendSelectionService? backendService = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _controller = controller;
        _updateService = updateService;
        _audioRecorder = audioRecorder;
        _backendService = backendService;
    }

    /// <summary>
    /// Initializes the tray icon and menu.
    /// </summary>
    public void Initialize()
    {
        _contextMenu = CreateContextMenu();
        
        _notifyIcon = new NotifyIcon
        {
            Text = AppName,
            Icon = CreateDefaultIcon(),
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        // Subscribe to controller events
        _controller.RecordingStateChanged += OnRecordingStateChanged;
        _controller.TranscriptionComplete += OnTranscriptionComplete;
        _controller.ErrorOccurred += OnErrorOccurred;

        _logger.LogInformation("Tray icon initialized");
        ShowNotification("VoxTether", $"Ready. Press {_settingsService.Settings.Hotkey} to record.");
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Settings
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        // Start with Windows
        _startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows");
        _startWithWindowsMenuItem.CheckOnClick = true;
        _startWithWindowsMenuItem.Checked = _settingsService.Settings.StartWithWindows;
        _startWithWindowsMenuItem.CheckedChanged += OnStartWithWindowsChanged;
        menu.Items.Add(_startWithWindowsMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        // Open Models Folder
        var modelsItem = new ToolStripMenuItem("Open Models Folder");
        modelsItem.Click += (_, _) => OpenFolder(SettingsService.UserModelsPath);
        menu.Items.Add(modelsItem);

        // Open Logs
        var logsItem = new ToolStripMenuItem("Open Logs");
        logsItem.Click += (_, _) => OpenFolder(SettingsService.LogsPath);
        menu.Items.Add(logsItem);

        menu.Items.Add(new ToolStripSeparator());

        // Test Microphone
        var testItem = new ToolStripMenuItem("Test Microphone");
        testItem.Click += async (_, _) => await TestMicrophone();
        menu.Items.Add(testItem);

        menu.Items.Add(new ToolStripSeparator());

        // Check for Updates
        var updateItem = new ToolStripMenuItem("Check for Updates...");
        updateItem.Click += async (_, _) => await CheckForUpdates();
        menu.Items.Add(updateItem);

        menu.Items.Add(new ToolStripSeparator());

        // About
        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnRecordingStateChanged(object? sender, bool isRecording)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = isRecording ? CreateRecordingIcon() : CreateDefaultIcon();
                
                if (isRecording && _settingsService.Settings.ShowNotifications)
                {
                    ShowNotification("Recording...", "Release hotkey to stop.");
                }
            }
        });
    }

    private void OnTranscriptionComplete(object? sender, string text)
    {
        if (_settingsService.Settings.ShowNotifications)
        {
            var displayText = text.Length > 100 ? text.Substring(0, 97) + "..." : text;
            ShowNotification("Transcription Complete", displayText);
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        ShowNotification("Error", error, System.Windows.Forms.ToolTipIcon.Error);
    }

    private void ShowSettings()
    {
        var window = new SettingsWindow(_settingsService, _audioRecorder, _backendService);
        window.ShowDialog();
    }

    private void ShowAbout()
    {
        var version = App.GetVersion();
        var hotkey = _settingsService.Settings.Hotkey;
        var modelPath = _settingsService.GetEffectiveModelPath();
        var modelName = modelPath != null ? Path.GetFileName(modelPath) : "None";

        MessageBox.Show(
            $"VoxTether v{version}\n\n" +
            $"Hotkey: {hotkey}\n" +
            $"Model: {modelName}\n\n" +
            "Push-to-talk dictation for Windows.\n" +
            "Fully offline - no cloud, no telemetry.",
            "About VoxTether",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task TestMicrophone()
    {
        ShowNotification("Testing...", "Recording for 2 seconds...");
        
        var result = await _controller.TestMicrophoneAsync();
        
        ShowNotification("Test Result", result);
    }

    private async Task CheckForUpdates()
    {
        ShowNotification("Checking for Updates", "Please wait...");
        
        try
        {
            var currentVersion = App.GetVersion();
            var updateInfo = await _updateService.CheckForUpdatesAsync(currentVersion);

            if (updateInfo == null)
            {
                MessageBox.Show(
                    "Unable to check for updates. Please check your internet connection.",
                    "Update Check Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (updateInfo.IsNewerVersion)
            {
                // Check if installer is available for in-app update
                if (!string.IsNullOrEmpty(updateInfo.InstallerUrl))
                {
                    var result = MessageBox.Show(
                        $"A new version of VoxTether is available!\n\n" +
                        $"Current version: v{currentVersion}\n" +
                        $"Latest version: v{updateInfo.Version}\n\n" +
                        "Would you like to download and install the update now?\n\n" +
                        "(VoxTether will restart after the update)",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        await DownloadAndInstallUpdate(updateInfo);
                    }
                }
                else
                {
                    // Fallback to opening release page if no installer available
                    var result = MessageBox.Show(
                        $"A new version of VoxTether is available!\n\n" +
                        $"Current version: v{currentVersion}\n" +
                        $"Latest version: v{updateInfo.Version}\n\n" +
                        "Would you like to open the download page?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        _updateService.OpenReleasePage(updateInfo);
                    }
                }
            }
            else
            {
                MessageBox.Show(
                    $"You are running the latest version (v{currentVersion}).",
                    "No Updates Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update check");
            MessageBox.Show(
                "An error occurred while checking for updates.",
                "Update Check Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task DownloadAndInstallUpdate(UpdateInfo updateInfo)
    {
        ShowNotification("Downloading Update", $"Downloading VoxTether v{updateInfo.Version}...");
        
        try
        {
            var success = await _updateService.DownloadAndInstallUpdateAsync(updateInfo);
            
            if (success)
            {
                // The installer will close the app and restart it
                // Exit gracefully to allow the installer to proceed
                _logger.LogInformation("Update installer launched, shutting down for update");
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show(
                    "Failed to download the update. Please try again later or download manually from the release page.",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading update");
            MessageBox.Show(
                $"An error occurred while downloading the update: {ex.Message}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnStartWithWindowsChanged(object? sender, EventArgs e)
    {
        var startWithWindows = _startWithWindowsMenuItem?.Checked ?? false;
        _settingsService.Update(s => s.StartWithWindows = startWithWindows);
        SetStartWithWindows(startWithWindows);
    }

    private void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    _logger.LogInformation("Enabled start with Windows");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                _logger.LogInformation("Disabled start with Windows");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set start with Windows");
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore errors opening folder
        }
    }

    private void ShowNotification(string title, string text, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    private static Icon CreateDefaultIcon()
    {
        // Try to load the embedded icon resource
        try
        {
            var resourceUri = new Uri("pack://application:,,,/Resources/VoxTether.ico", UriKind.Absolute);
            var resourceStream = Application.GetResourceStream(resourceUri);
            if (resourceStream != null)
            {
                using var stream = resourceStream.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
            // Fall back to creating icon programmatically
        }
        
        // Fallback: Create a simple blue microphone-like icon
        var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        
        // Draw a simple microphone shape
        using var brush = new SolidBrush(Color.FromArgb(66, 133, 244)); // Blue
        g.FillEllipse(brush, 10, 4, 12, 16);
        g.FillRectangle(brush, 13, 18, 6, 6);
        g.DrawArc(new Pen(brush, 2), 8, 12, 16, 14, 0, 180);
        g.FillRectangle(brush, 15, 24, 2, 4);

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static Icon CreateRecordingIcon()
    {
        // Create a red recording icon
        var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        
        // Draw a red circle (recording indicator)
        using var brush = new SolidBrush(Color.FromArgb(234, 67, 53)); // Red
        g.FillEllipse(brush, 6, 6, 20, 20);

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private void ExitApplication()
    {
        _logger.LogInformation("User requested exit");
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _controller.RecordingStateChanged -= OnRecordingStateChanged;
        _controller.TranscriptionComplete -= OnTranscriptionComplete;
        _controller.ErrorOccurred -= OnErrorOccurred;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
