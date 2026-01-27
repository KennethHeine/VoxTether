using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VoxTether.Core.Interfaces;
using VoxTether.Services;

namespace VoxTether.Views;

/// <summary>
/// About page showing application information.
/// </summary>
public sealed partial class AboutPage : Page
{
    private readonly IBackendClient _backendClient;
    private readonly SettingsService _settingsService;

    public AboutPage()
    {
        _backendClient = App.Services.GetRequiredService<IBackendClient>();
        _settingsService = App.Services.GetRequiredService<SettingsService>();
        
        this.InitializeComponent();
        
        // Set version
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "2.0.0";
        VersionText.Text = $"Version {version}";
        
        // Set current model
        CurrentModelText.Text = _settingsService.Settings.ModelName;
        
        // Load backend status
        Loaded += AboutPage_Loaded;
    }

    private async void AboutPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshBackendStatusAsync();
    }

    private async Task RefreshBackendStatusAsync()
    {
        try
        {
            var isHealthy = await _backendClient.IsHealthyAsync();
            
            if (isHealthy)
            {
                StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                BackendStatusText.Text = "Running";
                
                // Get device info
                var deviceInfo = await _backendClient.GetDeviceInfoAsync();
                if (deviceInfo.CudaAvailable)
                {
                    DeviceText.Text = $"CUDA ({deviceInfo.DeviceName ?? "GPU"})";
                }
                else
                {
                    DeviceText.Text = "CPU";
                }
                
                // Get current model from backend
                var models = await _backendClient.GetModelsAsync();
                // Note: The backend should return which model is currently loaded
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                BackendStatusText.Text = "Not responding";
                DeviceText.Text = "Unknown";
            }
        }
        catch (Exception)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            BackendStatusText.Text = "Error connecting";
            DeviceText.Text = "Unknown";
        }
    }

    private void OpenModelsFolder_Click(object sender, RoutedEventArgs e)
    {
        var modelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoxTether",
            "models"
        );
        
        Directory.CreateDirectory(modelsPath);
        Process.Start("explorer.exe", modelsPath);
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoxTether",
            "logs"
        );
        
        Directory.CreateDirectory(logsPath);
        Process.Start("explorer.exe", logsPath);
    }
}
