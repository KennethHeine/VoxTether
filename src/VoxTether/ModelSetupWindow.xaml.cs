using System.Windows;
using VoxTether.Core.Models;
using VoxTether.Core.Services;
using MessageBox = System.Windows.MessageBox;

namespace VoxTether;

/// <summary>
/// View model for model versions in the setup UI.
/// </summary>
public class SetupModelVersionViewModel
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
/// View model for models in the setup UI.
/// </summary>
public class SetupModelInfoViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public List<SetupModelVersionViewModel> Versions { get; set; } = new();
}

/// <summary>
/// Window for first-time model setup.
/// </summary>
public partial class ModelSetupWindow : Window
{
    private readonly ModelDownloadService _downloadService;
    private bool _isDownloading;
    private bool _hasDownloadedModel;

    /// <summary>
    /// Gets whether a model was successfully downloaded.
    /// </summary>
    public bool ModelDownloaded => _hasDownloadedModel;

    public ModelSetupWindow()
    {
        InitializeComponent();
        _downloadService = new ModelDownloadService();
        _downloadService.DownloadProgressChanged += OnDownloadProgressChanged;
        _downloadService.StatusChanged += OnDownloadStatusChanged;
        LoadModelCatalog();
        
        Closed += (s, e) =>
        {
            _downloadService.DownloadProgressChanged -= OnDownloadProgressChanged;
            _downloadService.StatusChanged -= OnDownloadStatusChanged;
            _downloadService.Dispose();
        };
    }

    private void LoadModelCatalog()
    {
        var models = ModelCatalog.GetAvailableModels();
        var viewModels = models.Select(m => new SetupModelInfoViewModel
        {
            Name = m.Name,
            Description = m.Description,
            Quality = m.Quality,
            Speed = m.Speed,
            Versions = m.Versions.Select(v => new SetupModelVersionViewModel
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
        
        // Check if any model is already downloaded
        UpdateContinueButtonState();
    }

    private void UpdateContinueButtonState()
    {
        // Check if any model exists in user folder
        var hasModel = System.IO.Directory.Exists(SettingsService.UserModelsPath) &&
                       System.IO.Directory.GetFiles(SettingsService.UserModelsPath, "*.bin").Length > 0;
        
        _hasDownloadedModel = hasModel;
        ContinueButton.IsEnabled = hasModel;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            MessageBox.Show("A download is already in progress.", "Download In Progress", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is System.Windows.Controls.Button button && button.Tag is SetupModelVersionViewModel versionVm)
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
                        $"Model '{versionVm.FileName}' downloaded successfully!\n\nYou can now continue to use VoxTether.",
                        "Download Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Refresh the model list and enable continue button
                    LoadModelCatalog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
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
