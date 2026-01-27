using CommunityToolkit.Mvvm.ComponentModel;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.ViewModels;

/// <summary>
/// ViewModel for audio settings.
/// </summary>
public partial class AudioSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedDeviceId = -1;

    [ObservableProperty]
    private int _clipboardDelayMs = 50;

    [ObservableProperty]
    private int _audioLevel = 0;

    public List<AudioDevice> AudioDevices { get; }

    public AudioSettingsViewModel(VoxTetherSettings settings, IAudioRecorder audioRecorder)
    {
        SelectedDeviceId = settings.AudioDeviceId;
        ClipboardDelayMs = settings.ClipboardDelayMs;

        // Get available audio devices
        AudioDevices = new List<AudioDevice>
        {
            new AudioDevice(-1, "(Default Device)")
        };

        var devices = audioRecorder.GetAvailableDevices();
        foreach (var (id, name) in devices)
        {
            AudioDevices.Add(new AudioDevice(id, name));
        }
    }

    public void ApplyTo(VoxTetherSettings settings)
    {
        settings.AudioDeviceId = SelectedDeviceId;
        settings.ClipboardDelayMs = ClipboardDelayMs;
    }
}

/// <summary>
/// Represents an audio device.
/// </summary>
public record AudioDevice(int Id, string Name);
