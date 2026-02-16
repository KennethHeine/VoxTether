# VoxTether Installation Guide

This guide covers installation and setup for VoxTether, a voice dictation application for Windows.

## System Requirements

- **OS**: Windows 10/11 (64-bit)
- **RAM**: 4 GB minimum, 8 GB recommended
- **Disk**: 500 MB for application

## Backend Server

VoxTether requires the backend server to be running. See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for backend setup.

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

# Frontend (Electron)
cd src/frontend-electron
npm install
npm start
```

> **Note**: The backend must be running separately. See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend).

---

## First Run Setup

On first launch, VoxTether will:

1. **Connect to Backend**: Connects to the transcription backend server
2. **GPU Detection**: VoxTether detects your hardware capabilities
3. **Model Download**: Choose and download a speech recognition model

---

## Usage

### Default Hotkey

**Ctrl + Shift + R** (configurable in Settings)

**How to use:**
1. Press the hotkey to **start** recording
2. Speak into your microphone
3. Press the hotkey again to **stop** recording and transcribe
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
  "windowToggleHotkey": "Ctrl+Shift+V",
  "toggleRecordingHotkey": "Ctrl+Shift+R",
  "modelName": "small",
  "language": "auto",
  "outputMode": "ClipboardAndPaste",
  "showNotifications": true,
  "showRecordingIndicator": true,
  "audioDeviceId": -1,
  "clipboardDelayMs": 50,
  "backendPort": 5678,
  "startMinimized": true,
  "startWithWindows": false,
  "theme": "system"
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

**Backend not connecting:**
1. Ensure the [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) is running
2. Check the backend URL in Settings (default: localhost:5678)
3. Look at the logs in `%APPDATA%\VoxTether\logs\`

### Hotkey Issues

**Hotkey not responding:**
1. Check if another application uses the same hotkey
2. Try a different key combination
3. Some games/full-screen apps capture keyboard input globally

**Need to dictate into elevated apps:**
Run VoxTether as Administrator (right-click → Run as administrator)

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
- Node.js 20.x+
- (Optional) Inno Setup 6 for creating installers

### Build Commands

```powershell
cd build
.\build.ps1 -Release -Version "2.0.0"

# Build with installer
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

---

## Additional Resources

- [README](../README.md) - Project overview
- [Architecture](ARCHITECTURE.md) - Technical architecture
- [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) - Backend repository
- [GitHub Releases](https://github.com/KennethHeine/VoxTether/releases) - Download builds
- [Issues](https://github.com/KennethHeine/VoxTether/issues) - Report bugs
