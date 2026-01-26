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
        if (!SettingsService.HasAnyModel())
        {
            var setupWindow = new ModelSetupWindow();
            var result = setupWindow.ShowDialog();
            
            // If user closed without downloading a model, exit the application
            // We check ModelDownloaded because the user might close the window without clicking Continue
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

        // Check for recommended backend downloads (first-run experience)
        CheckAndOfferBackendDownload();

        // Create controller and start
        _controller = _serviceProvider.GetRequiredService<VoxTetherController>();
        _trayIconManager = _serviceProvider.GetRequiredService<TrayIconManager>();

        _trayIconManager.Initialize();
        _controller.Start();
    }

    private void CheckAndOfferBackendDownload()
    {
        try
        {
            var settingsService = _serviceProvider?.GetRequiredService<SettingsService>();
            var backendSelection = _serviceProvider?.GetRequiredService<IBackendSelectionService>();
            var backendDownload = _serviceProvider?.GetRequiredService<IBackendDownloadService>();

            if (settingsService == null || backendSelection == null || backendDownload == null)
                return;

            var settings = settingsService.Settings;

            // Only offer if hardware acceleration is enabled
            if (!settings.EnableHardwareAcceleration)
                return;

            // Check if we've already shown the recommendation dialog
            if (settings.BackendRecommendationShown)
                return;

            // Check if any GPU backend is already installed
            if (backendSelection.IsBackendAvailable(TranscriptionBackendMode.Cuda) ||
                backendSelection.IsBackendAvailable(TranscriptionBackendMode.Vulkan) ||
                backendSelection.IsBackendAvailable(TranscriptionBackendMode.OpenVino))
            {
                return; // Already have a backend
            }

            // Get recommended backends based on hardware
            var recommended = backendDownload.GetRecommendedBackends();
            if (recommended.Count == 0)
                return; // No hardware detected

            // Show recommendation dialog
            var recommendedNames = string.Join(", ", recommended.Select(id =>
            {
                return id switch
                {
                    "cuda" => "NVIDIA CUDA",
                    "vulkan" => "Vulkan",
                    "openvino" => "Intel OpenVINO",
                    _ => id
                };
            }));

            var result = MessageBox.Show(
                $"VoxTether detected compatible GPU hardware and recommends downloading the {recommendedNames} backend(s) for faster transcription.\n\n" +
                "You can download backends now from Settings, or skip to use CPU-only mode.\n\n" +
                "Would you like to open Settings to download backends now?",
                "GPU Acceleration Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            // Mark that we've shown the recommendation dialog
            settingsService.Update(s => s.BackendRecommendationShown = true);

            if (result == MessageBoxResult.Yes)
            {
                // We'll open settings after the tray icon is initialized
                // Set a flag or queue it to open
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_trayIconManager != null)
                    {
                        // The tray icon manager has a method to open settings
                        // We'll need to call it, but we need to ensure it's initialized first
                        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                var audioRecorder = _serviceProvider?.GetRequiredService<IAudioRecorder>();
                                var window = new SettingsWindow(settingsService, audioRecorder, backendSelection, backendDownload);
                                window.ShowDialog();
                            });
                        });
                    }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash - this is not critical
            System.Diagnostics.Debug.WriteLine($"Error checking backend download: {ex.Message}");
        }
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

        // Backend selection service - must be registered and initialized before transcription engine
        services.AddSingleton<IBackendSelectionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BackendSelectionService>>();
            var settings = sp.GetRequiredService<SettingsService>().Settings;
            var service = new BackendSelectionService(logger);
            
            // Determine effective backend mode based on settings
            var requestedMode = settings.EnableHardwareAcceleration 
                ? settings.TranscriptionBackend 
                : TranscriptionBackendMode.CpuOnly;
            
            // Initialize backend selection - this must happen before any transcription
            service.DetermineBackend(requestedMode);
            
            return service;
        });

        // Backend download service
        services.AddSingleton<IBackendDownloadService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BackendDownloadService>>();
            var backendSelection = sp.GetRequiredService<IBackendSelectionService>();
            return new BackendDownloadService(logger, backendSelection);
        });

        // Core services
        services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        services.AddSingleton<IHotkeyService, LowLevelHookHotkeyService>();
        services.AddSingleton<ITextInjector>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ClipboardTextInjector>>();
            var settings = sp.GetRequiredService<SettingsService>().Settings;
            var injector = new ClipboardTextInjector(logger, settings.ClipboardDelayMs);
            injector.PasteToFocusedApp = settings.OutputMode == "FocusedApp";
            return injector;
        });
        services.AddSingleton<ITranscriptionEngine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WhisperCppEngine>>();
            var backendService = sp.GetRequiredService<IBackendSelectionService>();
            return new WhisperCppEngine(logger, backendService);
        });
        services.AddSingleton<ITextPostProcessor, NoOpTextPostProcessor>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        // App services
        services.AddSingleton<VoxTetherController>();
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TrayIconManager>>();
            var settingsService = sp.GetRequiredService<SettingsService>();
            var controller = sp.GetRequiredService<VoxTetherController>();
            var updateService = sp.GetRequiredService<IUpdateService>();
            var audioRecorder = sp.GetRequiredService<IAudioRecorder>();
            var backendService = sp.GetRequiredService<IBackendSelectionService>();
            var backendDownloadService = sp.GetRequiredService<IBackendDownloadService>();
            return new TrayIconManager(logger, settingsService, controller, updateService, audioRecorder, backendService, backendDownloadService);
        });
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

        // Check backend selection and whisper binary
        var backendLogger = loggerFactory.CreateLogger<BackendSelectionService>();
        var backendService = new BackendSelectionService(backendLogger);
        
        // Get GPU diagnostics
        var gpuDiagnostics = backendService.GetGpuDiagnostics();
        Console.WriteLine();
        Console.WriteLine("=== GPU Diagnostics ===");
        if (gpuDiagnostics.DetectedGpus.Count > 0)
        {
            foreach (var gpu in gpuDiagnostics.DetectedGpus)
            {
                Console.WriteLine($"  GPU: {gpu}");
            }
        }
        else
        {
            Console.WriteLine("  No GPUs detected");
        }
        
        // Check available backends
        Console.WriteLine();
        Console.WriteLine("=== Available Backends ===");
        var backends = backendService.GetAvailableBackends();
        foreach (var backend in backends)
        {
            var status = backend.IsAvailable ? "[OK]" : "[--]";
            var path = backend.IsAvailable ? $" ({backend.ExecutablePath})" : "";
            Console.WriteLine($"  {status} {IBackendSelectionService.GetDisplayName(backend.Backend)}{path}");
        }
        
        // Determine effective backend
        var settings = settingsService.Settings;
        var requestedMode = settings.EnableHardwareAcceleration 
            ? settings.TranscriptionBackend 
            : TranscriptionBackendMode.CpuOnly;
        
        var selectedBackend = backendService.DetermineBackend(requestedMode);
        Console.WriteLine();
        Console.WriteLine($"Requested backend: {IBackendSelectionService.GetDisplayName(requestedMode)}");
        Console.WriteLine($"Selected backend: {IBackendSelectionService.GetDisplayName(selectedBackend)}");
        
        if (backendService.FellBackToCpu)
        {
            Console.WriteLine($"[WARN] Fell back to CPU because requested backend was not available");
        }

        var whisperPath = backendService.ActiveWhisperPath;
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

