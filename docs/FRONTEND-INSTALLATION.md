# VoxTether Frontend Installation Guide

This guide explains how to install and run the VoxTether Electron frontend client.

## Overview

The VoxTether frontend is an Electron desktop application that provides:
- System tray integration
- Toggle recording via global hotkey
- Settings management
- Connection to the backend server

## Prerequisites

- Windows 10/11 (64-bit)
- VoxTether backend server running (see [BACKEND-SETUP.md](BACKEND-SETUP.md))

---

## Installation Options

### Option 1: Windows Installer (Recommended)

1. Download `VoxTether-Client-x.x.x-Setup.exe` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Run the installer
3. Follow the setup wizard
4. Launch VoxTether from the Start Menu

### Option 2: Portable ZIP

1. Download `VoxTether-Client-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract to a folder of your choice
3. Run `VoxTether.exe`

### Option 3: From Source (Development)

See below for detailed instructions.

---

## Building from Source

### Requirements

| Software | Version | Download |
|----------|---------|----------|
| Node.js | 20.x+ | [nodejs.org](https://nodejs.org/) |
| npm | 10.x+ | Included with Node.js |
| Git | Latest | [git-scm.com](https://git-scm.com/) |

### Verify Installation

```powershell
node --version   # Should be 20.x+
npm --version    # Should be 10.x+
git --version
```

### Clone Repository

```powershell
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether
```

### Install Dependencies

```powershell
cd src/frontend-electron
npm install
```

### Run in Development Mode

```powershell
npm start
```

The application will launch with DevTools enabled.

### Run with Debug Mode

```powershell
npm run dev
```

### Build for Production

```powershell
# Build unpacked application
npm run pack

# Build installer and portable
npm run build
```

Build outputs:
- `dist/win-unpacked/` - Unpacked application
- `dist/VoxTether Setup x.x.x.exe` - Windows installer (NSIS)
- `dist/VoxTether-x.x.x-win.zip` - Portable ZIP

---

## Configuration

### First Run

1. Start the VoxTether backend server first
2. Launch the frontend application
3. The app will appear in the system tray
4. Right-click the tray icon → "Settings" to configure

### Settings

All settings are stored in `%APPDATA%\VoxTether\settings.json`.

| Setting | Description | Default |
|---------|-------------|---------|
| `windowToggleHotkey` | Show/hide settings window | `Ctrl+Shift+V` |
| `toggleRecordingHotkey` | Start/stop recording | `Ctrl+Shift+R` |
| `modelName` | Model to use for transcription | `small` |
| `language` | Transcription language | `auto` |
| `outputMode` | How to output text | `ClipboardAndPaste` |
| `showNotifications` | Show transcription notifications | `true` |
| `showRecordingIndicator` | Visual indicator while recording | `true` |
| `theme` | UI theme | `system` |
| `backendHost` | Backend server host | `127.0.0.1` |
| `backendPort` | Backend server port | `5678` |

### Backend Connection

By default, the frontend connects to `http://127.0.0.1:5678`.

To connect to a remote backend server:
1. Open Settings
2. Configure the backend host and port
3. Save settings
4. Restart the application

---

## Usage

### System Tray

The application runs in the system tray. Right-click the tray icon for options:

- **Settings** - Open settings window
- **Test Microphone** - Record a 2-second test
- **Open Models Folder** - Open the models directory
- **Open Logs** - Open the logs directory
- **About** - Show version information
- **Exit** - Close the application

### Toggle Recording

1. Press the hotkey (default: `Ctrl+Shift+R`) to start recording
2. Speak into your microphone
3. Press the hotkey again to stop recording
4. The transcribed text is automatically typed/pasted

### Changing Hotkey

1. Open Settings (right-click tray → Settings)
2. Click "Capture" next to the hotkey field
3. Press your desired key combination
4. Click "Save Settings"

### Selecting a Model

1. Open Settings → Models tab
2. Click "Load Model" on a downloaded model
3. The model will be loaded on the backend

**Note:** Models are downloaded using the backend CLI. See [BACKEND-SETUP.md](BACKEND-SETUP.md).

---

## Troubleshooting

### Application doesn't start

1. Check if another instance is running (check system tray)
2. Delete `%APPDATA%\VoxTether\settings.json` and restart
3. Run from command line to see error messages:
   ```powershell
   cd "path\to\VoxTether"
   .\VoxTether.exe
   ```

### Can't connect to backend

1. Verify the backend is running:
   ```powershell
   curl http://127.0.0.1:5678/api/health
   ```
2. Check firewall settings
3. Verify backend host/port in settings

### Hotkey not working

1. Check if another application is using the same hotkey
2. Try a different key combination
3. Run as Administrator (for some elevated applications)

### Recording doesn't work

1. Check Windows Sound settings → Recording
2. Ensure microphone is set as default device
3. Grant microphone permissions in Windows Settings

### No models available

Models are managed via the backend CLI:

```bash
cd src/backend
python cli.py list           # List available models
python cli.py download small # Download the 'small' model
```

---

## Updating

### Installer Version

1. Download the new installer
2. Run the installer (will update existing installation)

### Portable Version

1. Download the new ZIP
2. Extract to a new folder
3. Copy `settings.json` from old installation

### From Source

```powershell
cd VoxTether
git pull
cd src/frontend-electron
npm install
npm start
```

---

## Uninstalling

### Installer Version

1. Windows Settings → Apps → VoxTether → Uninstall

### Portable Version

1. Delete the application folder
2. (Optional) Delete `%APPDATA%\VoxTether` to remove settings

---

## Development

### Project Structure

```
src/frontend-electron/
├── src/
│   ├── main.js          # Electron main process
│   ├── preload.js       # Secure IPC bridge
│   └── renderer/        # UI files
│       ├── index.html   # Main HTML
│       ├── styles.css   # Styles
│       └── renderer.js  # UI logic
├── package.json         # Dependencies
└── eslint.config.js     # Linting config
```

### Linting

```powershell
npm run lint
```

### Building

```powershell
# Development build (unpacked)
npm run pack

# Production build (installer + portable)
npm run build
```

---

## See Also

- [FRONTEND-FEATURES.md](FRONTEND-FEATURES.md) - Complete feature documentation
- [FRONTEND-TESTING.md](FRONTEND-TESTING.md) - Testing guide
- [BACKEND-SETUP.md](BACKEND-SETUP.md) - Backend server setup
- [BACKEND-API.md](BACKEND-API.md) - API documentation
- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
