using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace VoxTether.Services;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly ILogger<TrayIconManager> _logger;
    private readonly SettingsService _settingsService;
    private readonly VoxTetherController _controller;
    
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;
    private bool _disposed;

    public TrayIconManager(
        ILogger<TrayIconManager> logger,
        SettingsService settingsService,
        VoxTetherController controller)
    {
        _logger = logger;
        _settingsService = settingsService;
        _controller = controller;
    }

    /// <summary>
    /// Initializes the tray icon.
    /// </summary>
    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
        
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "VoxTether - Ready",
            IconSource = new GeneratedIconSource
            {
                Text = "V",
                BackgroundType = BackgroundType.Ellipse,
                Foreground = Microsoft.UI.Colors.White,
                Background = Microsoft.UI.Colors.DodgerBlue
            }
        };

        // Build context menu
        var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        
        var settingsItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Settings..." };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);
        
        var testMicItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Test Microphone" };
        testMicItem.Click += (_, _) => TestMicrophone();
        menu.Items.Add(testMicItem);
        
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        
        var openModelsItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Open Models Folder" };
        openModelsItem.Click += (_, _) => OpenModelsFolder();
        menu.Items.Add(openModelsItem);
        
        var openLogsItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Open Logs Folder" };
        openLogsItem.Click += (_, _) => OpenLogsFolder();
        menu.Items.Add(openLogsItem);
        
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        
        var aboutItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "About" };
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);
        
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        
        var exitItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => Exit();
        menu.Items.Add(exitItem);
        
        _trayIcon.ContextFlyout = menu;
        
        // Handle double-click to open settings
        _trayIcon.TrayIconLeftMouseDoubleClick += (_, _) => ShowSettings();
        
        // Subscribe to controller events
        _controller.RecordingStateChanged += OnRecordingStateChanged;
        _controller.StatusChanged += OnStatusChanged;
        
        _logger.LogDebug("Tray icon initialized");
    }

    private void OnRecordingStateChanged(object? sender, bool isRecording)
    {
        if (_trayIcon == null) return;
        
        _trayIcon.IconSource = new GeneratedIconSource
        {
            Text = "V",
            BackgroundType = BackgroundType.Ellipse,
            Foreground = Microsoft.UI.Colors.White,
            Background = isRecording ? Microsoft.UI.Colors.Red : Microsoft.UI.Colors.DodgerBlue
        };
    }

    private void OnStatusChanged(object? sender, string status)
    {
        if (_trayIcon == null) return;
        
        _trayIcon.ToolTipText = $"VoxTether - {status}";
    }

    private void ShowSettings()
    {
        if (_mainWindow is MainWindow mainWindow)
        {
            mainWindow.Show();
        }
    }

    private void ShowAbout()
    {
        if (_mainWindow is MainWindow mainWindow)
        {
            mainWindow.Show();
            // Navigate to About page
            // TODO: Add navigation command
        }
    }

    private async void TestMicrophone()
    {
        var audioRecorder = App.Services.GetRequiredService<Core.Interfaces.IAudioRecorder>();
        var backendClient = App.Services.GetRequiredService<Core.Interfaces.IBackendClient>();
        
        try
        {
            OnStatusChanged(this, "Testing microphone...");
            
            var tempPath = Path.Combine(Path.GetTempPath(), $"voxtether_test_{Guid.NewGuid()}.wav");
            audioRecorder.StartRecording(tempPath);
            
            await Task.Delay(2000);
            
            audioRecorder.StopRecording();
            
            var result = await backendClient.TranscribeAsync(tempPath);
            
            if (result.Success && !string.IsNullOrEmpty(result.Text))
            {
                ShowNotification("Microphone Test", $"Heard: \"{result.Text}\"");
            }
            else
            {
                ShowNotification("Microphone Test", "Recording worked, but no speech detected.");
            }
            
            try { File.Delete(tempPath); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microphone test failed");
            ShowNotification("Microphone Test Failed", ex.Message);
        }
        finally
        {
            OnStatusChanged(this, "Ready");
        }
    }

    private void OpenModelsFolder()
    {
        System.Diagnostics.Process.Start("explorer.exe", SettingsService.ModelsPath);
    }

    private void OpenLogsFolder()
    {
        System.Diagnostics.Process.Start("explorer.exe", SettingsService.LogsPath);
    }

    private void Exit()
    {
        App.Current.Shutdown();
    }

    /// <summary>
    /// Shows a notification balloon.
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        _trayIcon?.ShowNotification(title, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _controller.RecordingStateChanged -= OnRecordingStateChanged;
        _controller.StatusChanged -= OnStatusChanged;
        
        _trayIcon?.Dispose();
        _trayIcon = null;
        
        GC.SuppressFinalize(this);
    }
}
