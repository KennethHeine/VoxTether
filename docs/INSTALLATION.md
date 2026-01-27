# VoxTether Installation Guide

This guide covers installation and setup for VoxTether, a push-to-talk dictation application for Windows.

## System Requirements

- **OS**: Windows 10/11 (64-bit)
- **Runtime**: .NET 8.0 (bundled in self-contained builds)
- **GPU** (optional): NVIDIA GPU with CUDA 11.8 support
- **RAM**: 4 GB minimum, 8 GB recommended
- **Disk**: 100 MB for application + 75 MB - 3 GB per model

---

## Installation Options

### Option A: Windows Installer (Easiest)

1. Download `VoxTether-Setup-x.x.x.exe` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Run the installer
   - No admin rights required (installs to user profile by default)
   - Optional: Choose "Install for all users" if you have admin rights
3. Launch VoxTether from the Start Menu or desktop shortcut
4. On first launch, you'll be prompted to download a speech recognition model

### Option B: Portable Version

1. Download `VoxTether-x.x.x-win-x64-portable.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract to a folder of your choice (e.g., `C:\Tools\VoxTether`)
3. Run `VoxTether.exe`
4. On first launch, you'll be prompted to download a speech recognition model

### Option C: Build from Source

```powershell
# Clone the repository
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether

# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Run tests
dotnet test

# Publish self-contained executable
dotnet publish src/VoxTether/VoxTether.csproj -c Release -r win-x64 --self-contained
```

The published application will be in `src/VoxTether/bin/Release/net8.0-windows/win-x64/publish/`.

---

## GPU Acceleration Setup (Optional)

VoxTether supports NVIDIA GPU acceleration for faster transcription:

1. Download the CUDA backend from Settings → Performance → Backend Management
2. Install [CUDA Toolkit 11.8](https://developer.nvidia.com/cuda-11-8-0-download-archive) from NVIDIA
3. Restart VoxTether

See [CUDA Troubleshooting Guide](cuda-troubleshooting.md) for detailed help.

---

## First Run Setup

On first launch, VoxTether will:

1. **GPU Detection**: VoxTether detects your hardware
2. **Model Download**: Choose and download a speech recognition model
3. **Hotkey Configuration**: The default hotkey is shown

### Recommended Models

| Model | Size | Quality | Speed | Best For |
|-------|------|---------|-------|----------|
| ggml-tiny.bin | ~75 MB | Basic | Very Fast | Quick notes, testing |
| ggml-base.bin | ~142 MB | Good | Fast | General use |
| **ggml-small.bin** | ~466 MB | Better | Moderate | **Recommended for most users** |
| ggml-medium.bin | ~1.5 GB | Great | Slow | When accuracy matters |
| ggml-large-v3.bin | ~3 GB | Best | Very Slow | Maximum accuracy |

---

## Usage

### Default Hotkey

**Ctrl + Alt + Space** (configurable in Settings)

**How to use:**
1. Press and **hold** the hotkey
2. Speak into your microphone
3. **Release** the hotkey to transcribe
4. The text is automatically inserted at your cursor

### Changing the Hotkey

1. Right-click the VoxTether tray icon
2. Select **Settings...**
3. Click the hotkey field and press your new key combination
4. Click **Save**

### System Tray Menu

Right-click the tray icon to access:

| Menu Item | Description |
|-----------|-------------|
| Settings... | Configure hotkey, model, and options |
| Test Microphone | Record a 2-second test and show transcription |
| Open Models Folder | Access downloaded models |
| Open Logs | Access log files for troubleshooting |
| Check for Updates... | Check for new versions |
| About | Show version and configuration info |
| Exit | Close VoxTether |

### Command Line Options

```powershell
# Run healthcheck
VoxTether.exe --healthcheck
```

---

## Configuration

Settings are stored in:
- `%APPDATA%\VoxTether\settings.json`

### Settings Structure

```json
{
  "Hotkey": "Ctrl + Alt + Space",
  "ModelName": "ggml-base.bin",
  "TranscriptionBackend": "Auto",
  "EnableHardwareAcceleration": true,
  "ShowNotifications": true,
  "ShowRecordingIndicator": true,
  "CopyToClipboard": true,
  "FallbackToTyping": true,
  "ClipboardDelayMs": 100,
  "Language": "auto"
}
```

---

## Troubleshooting

### Audio / Microphone Issues

**No audio recording:**
1. Check Windows Sound settings → Recording
2. Ensure your microphone is set as the default device
3. Run **Test Microphone** from the tray menu

**Audio quality issues:**
1. Move closer to the microphone
2. Reduce background noise
3. Check microphone gain levels in Windows settings

### Hotkey Issues

**Hotkey not responding:**
1. Check if another application uses the same hotkey
2. Try a different key combination
3. Some games/full-screen apps capture keyboard input globally

**Need to dictate into elevated apps:**
Run VoxTether as Administrator (right-click → Run as administrator)

### GPU / Performance Issues

**GPU not working:**
1. Download CUDA backend in Settings → Performance
2. Install [CUDA Toolkit 11.8](https://developer.nvidia.com/cuda-11-8-0-download-archive)
3. Check [CUDA Troubleshooting Guide](cuda-troubleshooting.md)

**Slow transcription:**
1. Try a smaller model (ggml-tiny.bin or ggml-base.bin)
2. Enable GPU acceleration
3. Close resource-intensive applications

### Text Insertion Issues

**Text not appearing:**
1. Some applications don't support clipboard paste
2. Password fields are skipped for security
3. Try running VoxTether as Administrator

### Antivirus False Positives

VoxTether uses low-level keyboard hooks for hotkey detection. Some antivirus software may flag this.

**Solution:**
1. Add VoxTether to your antivirus exclusions
2. Verify the download hash matches the release

---

## Updating

### Checking for Updates

1. Right-click the tray icon
2. Select **Check for Updates...**
3. If a new version is available, download from the releases page

### Upgrading

**Installer version:**
1. Download the new installer
2. Run it - it automatically upgrades the existing installation
3. Settings, models, and logs are preserved

**Portable version:**
1. Download the new version
2. Extract to the same folder
3. Your settings are preserved in `%APPDATA%\VoxTether`

---

## Uninstalling

### Installer Version

Use **Settings → Apps → VoxTether → Uninstall** or run the uninstaller from the Start Menu.

### Portable Version

Delete the folder containing `VoxTether.exe`.

To remove settings and models:
```powershell
Remove-Item -Recurse "$env:APPDATA\VoxTether"
```

---

## Developer Installation

For contributing to VoxTether development:

```powershell
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run specific test category
dotnet test --filter "Category=Unit"
```

---

## Additional Resources

- [README](../README.md) - Project overview
- [CUDA Troubleshooting](cuda-troubleshooting.md) - GPU issues
- [Backend Download System](backend-download-system.md) - How backends work
- [GitHub Releases](https://github.com/KennethHeine/VoxTether/releases) - Download builds
- [Issues](https://github.com/KennethHeine/VoxTether/issues) - Report bugs
