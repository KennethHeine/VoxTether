using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VoxTether.Core.Interfaces;
using VoxTether.Services;
using VoxTether.ViewModels;

namespace VoxTether.Views;

/// <summary>
/// Audio settings page.
/// </summary>
public sealed partial class AudioSettingsPage : Page
{
    public AudioSettingsViewModel ViewModel { get; }
    
    private readonly IAudioRecorder _audioRecorder;
    private readonly IBackendClient _backendClient;
    private readonly SettingsService _settingsService;

    public AudioSettingsPage()
    {
        _settingsService = App.Services.GetRequiredService<SettingsService>();
        _audioRecorder = App.Services.GetRequiredService<IAudioRecorder>();
        _backendClient = App.Services.GetRequiredService<IBackendClient>();
        
        ViewModel = new AudioSettingsViewModel(_settingsService.Settings, _audioRecorder);
        
        this.InitializeComponent();
        
        // Subscribe to audio level changes
        _audioRecorder.AudioLevelChanged += OnAudioLevelChanged;
    }

    private void OnAudioLevelChanged(object? sender, int level)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.AudioLevel = level;
        });
    }

    private async void TestMicrophone_Click(object sender, RoutedEventArgs e)
    {
        TestMicButton.IsEnabled = false;
        TestProgress.IsActive = true;
        TestResultCard.Visibility = Visibility.Collapsed;
        
        try
        {
            // Record for 2 seconds
            var tempPath = Path.Combine(Path.GetTempPath(), $"voxtether_test_{Guid.NewGuid()}.wav");
            _audioRecorder.SelectedDeviceId = ViewModel.SelectedDeviceId;
            _audioRecorder.StartRecording(tempPath);
            
            await Task.Delay(2000);
            
            _audioRecorder.StopRecording();
            
            // Transcribe
            var result = await _backendClient.TranscribeAsync(tempPath);
            
            // Show result
            TestResultCard.Visibility = Visibility.Visible;
            if (result.Success && !string.IsNullOrEmpty(result.Text))
            {
                TestResultText.Text = $"Heard: \"{result.Text}\"";
            }
            else if (result.Success)
            {
                TestResultText.Text = "Recording worked, but no speech was detected.";
            }
            else
            {
                TestResultText.Text = $"Error: {result.Error}";
            }
            
            // Clean up temp file
            try { File.Delete(tempPath); } catch { }
        }
        catch (Exception ex)
        {
            TestResultCard.Visibility = Visibility.Visible;
            TestResultText.Text = $"Test failed: {ex.Message}";
        }
        finally
        {
            TestMicButton.IsEnabled = true;
            TestProgress.IsActive = false;
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // Apply settings
        ViewModel.ApplyTo(_settingsService.Settings);
        _settingsService.Save();
        
        // Update audio recorder
        _audioRecorder.SelectedDeviceId = ViewModel.SelectedDeviceId;
        
        // Show confirmation
        var dialog = new ContentDialog
        {
            Title = "Settings Saved",
            Content = "Your audio settings have been saved.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        
        await dialog.ShowAsync();
    }

    ~AudioSettingsPage()
    {
        _audioRecorder.AudioLevelChanged -= OnAudioLevelChanged;
    }
}
