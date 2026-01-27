using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VoxTether.Core.Interfaces;
using VoxTether.Services;
using VoxTether.ViewModels;

namespace VoxTether.Views;

/// <summary>
/// Models management page.
/// </summary>
public sealed partial class ModelsPage : Page
{
    public ModelsViewModel ViewModel { get; }
    
    private readonly IBackendClient _backendClient;
    private readonly SettingsService _settingsService;

    public ModelsPage()
    {
        _backendClient = App.Services.GetRequiredService<IBackendClient>();
        _settingsService = App.Services.GetRequiredService<SettingsService>();
        
        ViewModel = new ModelsViewModel(_settingsService.Settings);
        
        this.InitializeComponent();
        
        // Load models on page load
        Loaded += ModelsPage_Loaded;
    }

    private async void ModelsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingText.Text = "Loading models...";
        
        try
        {
            var models = await _backendClient.GetModelsAsync();
            ViewModel.UpdateModels(models, _settingsService.Settings.ModelName);
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error Loading Models",
                Content = $"Could not load models from backend: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void ModelAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string modelName)
        {
            var modelVm = ViewModel.Models.FirstOrDefault(m => m.Name == modelName);
            if (modelVm == null) return;
            
            if (modelVm.Downloaded)
            {
                // Load the model
                await LoadModelAsync(modelName);
            }
            else
            {
                // Download the model
                await DownloadModelAsync(modelName, modelVm);
            }
        }
    }

    private async Task LoadModelAsync(string modelName)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingText.Text = $"Loading {modelName}...";
        
        try
        {
            var success = await _backendClient.LoadModelAsync(modelName);
            
            if (success)
            {
                // Update settings
                _settingsService.Settings.ModelName = modelName;
                _settingsService.Save();
                
                // Refresh model list
                await RefreshModelsAsync();
                
                var dialog = new ContentDialog
                {
                    Title = "Model Loaded",
                    Content = $"The {modelName} model is now active.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                throw new Exception("Backend returned failure");
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error Loading Model",
                Content = $"Could not load model: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async Task DownloadModelAsync(string modelName, ModelItemViewModel modelVm)
    {
        modelVm.IsDownloading = true;
        modelVm.IsActionEnabled = false;
        
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    modelVm.DownloadProgress = p.Progress;
                });
            });
            
            await _backendClient.DownloadModelAsync(modelName, progress);
            
            // Refresh after download
            await RefreshModelsAsync();
            
            var dialog = new ContentDialog
            {
                Title = "Download Complete",
                Content = $"The {modelName} model has been downloaded.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Download Failed",
                Content = $"Could not download model: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            modelVm.IsDownloading = false;
            modelVm.IsActionEnabled = true;
        }
    }
}
