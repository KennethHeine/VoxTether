# VoxTether Installation Guide

This guide covers installation and setup for VoxTether, a voice dictation application for Windows.

## System Requirements

- **OS**: Windows 10/11 (64-bit)
- **RAM**: 4 GB minimum, 8 GB recommended
- **Disk**: 500 MB for application + 75 MB - 3 GB per model
- **GPU** (optional): NVIDIA GPU with CUDA 12 support for acceleration

---

## Installation Options

### Option A: Windows Installer (Recommended)

1. Download `VoxTether-x.x.x-Setup.exe` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Run the installer and follow the wizard
3. Launch VoxTether from the Start Menu or Desktop shortcut
4. On first launch, you'll be prompted to download a speech recognition model

### Option B: Portable ZIP

1. Download `VoxTether-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract the ZIP file to a folder of your choice (e.g., `C:\Tools\VoxTether`)
3. Run `VoxTether.exe`
4. On first launch, you'll be prompted to download a speech recognition model

### Option C: From Source (Development)

```powershell
# Clone the repository
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether

# --- Backend (Python) ---
cd src/backend
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt

# Run backend server
python -m uvicorn main:app --port 5678

# --- Frontend (Electron) --- (in a new terminal)
cd src/frontend-electron
npm install
npm start
```

---

## GPU Acceleration Setup (Optional)

For NVIDIA GPU acceleration:

```powershell
# Install CUDA libraries (in the backend virtual environment)
cd src/backend
.\venv\Scripts\Activate.ps1
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Or install the full [CUDA Toolkit 12](https://developer.nvidia.com/cuda-downloads) from NVIDIA.

**Verify GPU detection:**
The About page in Settings will show your GPU status and device information.

---

## First Run Setup

On first launch, VoxTether will:

1. **Start Backend**: The Python transcription backend starts automatically
2. **GPU Detection**: VoxTether detects your hardware capabilities
3. **Model Download**: Choose and download a speech recognition model

### Recommended Models

| Model | Size | Price | Quality | Speed | Best For |
|-------|------|-------|---------|-------|----------|
| tiny | ~75 MB | Free (local) | Basic | Very Fast | Quick notes, testing |
| base | ~142 MB | Free (local) | Good | Fast | General use |
| **small** | ~466 MB | Free (local) | Better | Moderate | **Recommended for most users** |
| medium | ~1.5 GB | Free (local) | Great | Slow | When accuracy matters |
| large-v3 | ~3 GB | Free (local) | Best | Very Slow | Maximum accuracy |
| large-v3-turbo | ~1.6 GB | Free (local) | Excellent | Fast | Best speed/accuracy balance |
| distil-large-v3 | ~1.1 GB | Free (local) | Excellent | Fast | Fast high-quality transcription |

---

## Usage

### Default Hotkey

**Ctrl + Shift + Space** (configurable in Settings)

**How to use:**
1. Press and **hold** the hotkey
2. Speak into your microphone
3. **Release** the hotkey to transcribe
4. The text is automatically inserted at your cursor

### Changing the Hotkey

1. Right-click the VoxTether tray icon
2. Select **Settings...**
3. Go to the **General** tab
4. Click "Capture" and press your new key combination
5. Click **Save Settings**

### System Tray Menu

Right-click the tray icon to access:

| Menu Item | Description |
|-----------|-------------|
| Settings... | Open the settings window |
| Test Microphone | Record a 2-second test and show transcription |
| Open Models Folder | Access downloaded models |
| Open Logs Folder | Access log files for troubleshooting |
| About | Show version and system info |
| Exit | Close VoxTether |

---

## Configuration

Settings are stored in:
- `%APPDATA%\VoxTether\settings.json`

### Settings Reference

```json
{
  "Hotkey": "Ctrl+Shift+Space",
  "ModelName": "small",
  "Language": "auto",
  "OutputMode": "ClipboardAndPaste",
  "ShowNotifications": true,
  "ShowRecordingIndicator": true,
  "AudioDeviceId": -1,
  "ClipboardDelayMs": 50,
  "BackendPort": 5678,
  "StartMinimized": true,
  "StartWithWindows": false,
  "Theme": "System"
}
```

**Output modes:**
- `Clipboard` - Copy text to clipboard only
- `ClipboardAndPaste` - Copy and paste at cursor (default)
- `SimulateTyping` - Type text character by character

---

## Troubleshooting

### Audio / Microphone Issues

**No audio recording:**
1. Check Windows Sound settings → Recording
2. Ensure your microphone is set as the default device
3. Run **Test Microphone** from the tray menu
4. Try selecting a specific device in Settings → Audio

**Audio quality issues:**
1. Move closer to the microphone
2. Reduce background noise
3. Check microphone gain levels in Windows settings

### Backend Issues

**Backend not starting:**
1. Check if port 5678 is available
2. Look at the logs in `%APPDATA%\VoxTether\logs\`
3. Try restarting the application

**Slow transcription:**
1. Try a smaller model (tiny or base)
2. Enable GPU acceleration if you have an NVIDIA GPU
3. Close resource-intensive applications

### Hotkey Issues

**Hotkey not responding:**
1. Check if another application uses the same hotkey
2. Try a different key combination
3. Some games/full-screen apps capture keyboard input globally

**Need to dictate into elevated apps:**
Run VoxTether as Administrator (right-click → Run as administrator)

### GPU / Performance Issues

**GPU not detected:**
1. Install CUDA packages: See GPU Acceleration section above
2. Update NVIDIA drivers
3. Check the About page for device status

### Text Insertion Issues

**Text not appearing:**
1. Some applications don't support clipboard paste
2. Password fields are skipped for security
3. Try running VoxTether as Administrator
4. Try changing output mode in Settings

### Antivirus False Positives

VoxTether uses low-level keyboard hooks for hotkey detection. Some antivirus software may flag this.

**Solution:**
1. Add the VoxTether installation folder to your antivirus exclusions
2. Verify the download hash matches the release

---

## Updating

### Checking for Updates

1. Right-click the tray icon → About
2. Click "View Releases" to check GitHub

### Upgrading

**Using Installer:**
1. Download the new installer
2. Run it - it will upgrade the existing installation

**Using Portable ZIP:**
1. Download the new version
2. Extract to the same folder (overwrites old version)

Your settings and downloaded models are preserved in `%APPDATA%\VoxTether`.

---

## Uninstalling

**If installed with the installer:**
1. Open Windows Settings → Apps → VoxTether → Uninstall

**If using portable version:**
1. Delete the folder containing `VoxTether.exe`

**To remove settings and models:**
```powershell
Remove-Item -Recurse "$env:APPDATA\VoxTether"
```

---

## Building from Source

### Prerequisites

- Windows 10/11 (64-bit)
- Python 3.13+
- Node.js 20.x+
- (Optional) Inno Setup 6 for creating installers

### Build Commands

```powershell
# Build both frontend and backend
cd build
.\build.ps1

# Build for release
.\build.ps1 -Release -Version "2.0.0"

# Build with installer
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

### Output

- `build/output/` - Built application files
- `build/installer/` - Windows installer (if created)
- `build/VoxTether-x.x.x-win-x64.zip` - Portable ZIP (if release)

---

## Additional Resources

- [README](../README.md) - Project overview
- [Architecture](ARCHITECTURE.md) - Technical architecture
- [GitHub Releases](https://github.com/KennethHeine/VoxTether/releases) - Download builds
- [Issues](https://github.com/KennethHeine/VoxTether/issues) - Report bugs
