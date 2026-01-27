using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Infrastructure;
using VoxTether.Services;

namespace VoxTether;

/// <summary>
/// The main application class.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;
    private TrayIconManager? _trayIconManager;
    private VoxTetherController? _controller;
    
    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;
    
    /// <summary>
    /// Gets the current app instance.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the main window.
    /// </summary>
    public Window? MainWindow => _mainWindow;

    public App()
    {
        this.InitializeComponent();
        
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize components
        var settingsService = Services.GetRequiredService<SettingsService>();
        var settings = settingsService.Settings;
        
        // Create main window
        _mainWindow = new MainWindow();
        
        // Initialize tray icon
        _trayIconManager = Services.GetRequiredService<TrayIconManager>();
        _trayIconManager.Initialize(_mainWindow);
        
        // Initialize controller
        _controller = Services.GetRequiredService<VoxTetherController>();
        _ = _controller.StartAsync();
        
        // Show window or start minimized
        if (!settings.StartMinimized)
        {
            _mainWindow.Activate();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
#if DEBUG
            builder.AddDebug();
#endif
        });
        
        // Settings
        services.AddSingleton<SettingsService>();
        
        // Core services
        services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        services.AddSingleton<IHotkeyService, LowLevelHookHotkeyService>();
        services.AddSingleton<ITextInjector, ClipboardTextInjector>();
        
        // Backend client
        services.AddHttpClient<IBackendClient, BackendClient>(client =>
        {
            var settings = Services?.GetService<SettingsService>()?.Settings;
            var port = settings?.BackendPort ?? 5678;
            client.BaseAddress = new Uri($"http://127.0.0.1:{port}");
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        
        // Backend process manager
        services.AddSingleton<BackendProcessManager>();
        
        // App services
        services.AddSingleton<TrayIconManager>();
        services.AddSingleton<VoxTetherController>();
    }

    /// <summary>
    /// Shuts down the application.
    /// </summary>
    public void Shutdown()
    {
        _controller?.Stop();
        _trayIconManager?.Dispose();
        
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        Exit();
    }
}
