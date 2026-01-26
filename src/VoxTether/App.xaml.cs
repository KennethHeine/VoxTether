using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Core.Services;
using VoxTether.Infrastructure;
using VoxTether.Transcription;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace VoxTether;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TrayIconManager? _trayIconManager;
    private VoxTetherController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for healthcheck command
        if (e.Args.Length > 0 && e.Args[0] == "--healthcheck")
        {
            RunHealthCheck();
            Shutdown(0);
            return;
        }

        // Check if a model is available, prompt user to download if not
        if (!HasModel())
        {
            var setupWindow = new ModelSetupWindow();
            var result = setupWindow.ShowDialog();
            
            // If user closed without downloading a model, exit the application
            if (result != true || !setupWindow.ModelDownloaded)
            {
                MessageBox.Show(
                    "VoxTether requires a speech recognition model to function.\n\n" +
                    "Please restart the application and download a model to continue.",
                    "Model Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown(0);
                return;
            }
        }

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Create controller and start
        _controller = _serviceProvider.GetRequiredService<VoxTetherController>();
        _trayIconManager = _serviceProvider.GetRequiredService<TrayIconManager>();

        _trayIconManager.Initialize();
        _controller.Start();
    }

    /// <summary>
    /// Checks if a speech recognition model is available.
    /// </summary>
    private static bool HasModel()
    {
        // Check user models folder first (this persists across updates)
        if (Directory.Exists(SettingsService.UserModelsPath))
        {
            var userModels = Directory.GetFiles(SettingsService.UserModelsPath, "*.bin");
            if (userModels.Length > 0)
            {
                return true;
            }
        }

        // Check installed models folder (bundled with app, if any)
        if (Directory.Exists(SettingsService.InstalledModelsPath))
        {
            var installedModels = Directory.GetFiles(SettingsService.InstalledModelsPath, "*.bin");
            if (installedModels.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Settings
        services.AddSingleton<SettingsService>();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFileLogger(SettingsService.LogsPath);
#if DEBUG
            builder.AddDebug();
#endif
        });

        // Core services
        services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        services.AddSingleton<IHotkeyService, LowLevelHookHotkeyService>();
        services.AddSingleton<ITextInjector>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ClipboardTextInjector>>();
            var settings = sp.GetRequiredService<SettingsService>().Settings;
            return new ClipboardTextInjector(logger, settings.ClipboardDelayMs);
        });
        services.AddSingleton<ITranscriptionEngine, WhisperCppEngine>();
        services.AddSingleton<ITextPostProcessor, NoOpTextPostProcessor>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        // App services
        services.AddSingleton<VoxTetherController>();
        services.AddSingleton<TrayIconManager>();
    }

    private void RunHealthCheck()
    {
        Console.WriteLine($"VoxTether v{GetVersion()}");
        Console.WriteLine();

        var settingsService = new SettingsService();
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Check recording device
        var recorderLogger = loggerFactory.CreateLogger<NAudioRecorder>();
        var recorder = new NAudioRecorder(recorderLogger);
        
        if (recorder.HasRecordingDevice())
        {
            var deviceName = recorder.GetDefaultDeviceName();
            Console.WriteLine($"[OK] Recording device found: {deviceName}");
        }
        else
        {
            Console.WriteLine("[FAIL] No recording device found");
            Environment.ExitCode = 1;
        }
        recorder.Dispose();

        // Check whisper binary
        var whisperLogger = loggerFactory.CreateLogger<WhisperCppEngine>();
        var whisper = new WhisperCppEngine(whisperLogger);
        var whisperPath = whisper.GetWhisperPath();
        
        if (whisperPath != null)
        {
            Console.WriteLine($"[OK] Whisper binary found: {whisperPath}");
        }
        else
        {
            Console.WriteLine("[FAIL] Whisper binary not found");
            Environment.ExitCode = 1;
        }

        // Check model file
        var modelPath = settingsService.GetEffectiveModelPath();
        if (modelPath != null && File.Exists(modelPath))
        {
            var modelSize = new FileInfo(modelPath).Length / (1024 * 1024);
            Console.WriteLine($"[OK] Model file found: {modelPath} ({modelSize} MB)");
        }
        else
        {
            Console.WriteLine("[FAIL] Model file not found");
            Environment.ExitCode = 1;
        }

        Console.WriteLine();
        Console.WriteLine($"Settings path: {settingsService.SettingsPath}");
        Console.WriteLine($"Logs path: {SettingsService.LogsPath}");
        Console.WriteLine($"Models path (user): {SettingsService.UserModelsPath}");
        Console.WriteLine($"Models path (installed): {SettingsService.InstalledModelsPath}");
    }

    public static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Stop();
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

