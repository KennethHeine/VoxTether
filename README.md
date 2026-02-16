# VoxTether

Voice dictation for Windows 10/11. Fully offline, no cloud, no telemetry.

## Features

- **Toggle recording**: Press a global hotkey to start/stop recording
- **Fully offline**: Uses faster-whisper for local speech-to-text, no internet required after model download
- **Modern UI**: Built with Electron for a clean, Windows 11 Fluent-inspired interface
- **Text insertion**: Automatically types transcribed text at your cursor position
- **System tray**: Runs quietly in the background
- **Model management**: Download models on-demand from HuggingFace
- **Privacy-first**: No network calls, no telemetry, all processing is local
- **Client-Server Architecture**: Flexible deployment with separate frontend and backend

## Architecture

VoxTether uses a client-server architecture:
- **Client (Frontend)**: Electron 40.x - Desktop application with UI and system tray (this repo)
- **Server (Backend)**: Python FastAPI - Speech-to-text transcription service ([VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend))

The backend runs as a separate Python server, which can be on the same machine (localhost) or on a different server on your network.

See [Architecture Documentation](docs/ARCHITECTURE.md) for details.

## Requirements

### Client (Electron App)
- Windows 10/11 (64-bit)

### Server (Python Backend)
- See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for backend requirements

## Installation

### Quick Start (Development)

**Terminal 1 - Start Backend Server:**

See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for backend setup instructions.

**Terminal 2 - Start Frontend Client:**
```powershell
cd src/frontend-electron
npm install
npm start
```

### Production Deployment

**Server Setup:**

See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for server setup.

**Client Setup (any Windows machine):**
1. Download the Electron client from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Configure the backend server address in Settings
3. Start using push-to-talk!

> 📖 **For detailed installation options and troubleshooting, see [Installation Guide](docs/INSTALLATION.md).**

## Usage

### Default Hotkey

**Ctrl + Shift + R** (configurable in Settings)

Press the hotkey to start recording. Press it again to stop recording and transcribe.

### Changing the Hotkey

1. Right-click the tray icon
2. Select "Settings..."
3. Click the hotkey field and press your desired key combination
4. Click Save

### System Tray Menu

Right-click the VoxTether tray icon to access:

- **Settings...** - Configure hotkey, model, and options
- **Test Microphone** - Record a 2-second test and show the transcription
- **Open Models Folder** - Access downloaded models
- **Open Logs** - Access log files for troubleshooting
- **About** - Show version and configuration info
- **Exit** - Close VoxTether

### First Run

On first launch, VoxTether will:
1. Detect your GPU hardware
2. Prompt you to download a speech recognition model
3. Show the default hotkey configuration

## Configuration

Settings are stored in `%APPDATA%\VoxTether\settings.json`

### Available Settings

```json
{
  "windowToggleHotkey": "Ctrl+Shift+V",
  "toggleRecordingHotkey": "Ctrl+Shift+R",
  "modelName": "small",
  "language": "auto",
  "outputMode": "ClipboardAndPaste",
  "showNotifications": true,
  "showRecordingIndicator": true,
  "startMinimized": true,
  "startWithWindows": false,
  "theme": "system"
}
```

### Language Codes

Use "auto" for automatic detection, or specify a language code:
- `en` - English
- `es` - Spanish
- `fr` - French
- `de` - German
- `it` - Italian
- `pt` - Portuguese
- `nl` - Dutch
- `ru` - Russian
- `zh` - Chinese
- `ja` - Japanese
- `ko` - Korean

## Troubleshooting

### Audio Not Recording

1. Check Windows Sound settings → Recording
2. Ensure microphone is set as default device
3. Test with Settings → Test Microphone

### Hotkey Not Working

1. Check if another application is using the same hotkey
2. Try a different key combination in Settings
3. Run as Administrator if targeting elevated apps

### Frontend can't connect to backend

1. Ensure the [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) is running
2. Check the backend URL in Settings (default: localhost:5678)

### Antivirus False Positives

VoxTether uses low-level keyboard hooks for global hotkey detection. Some antivirus software may flag this as suspicious.

To resolve:
1. Add VoxTether to your antivirus exclusions
2. Verify the download hash matches the release

## Development

### Project Structure

```
VoxTether/
├── src/
│   └── frontend-electron/       # Electron Frontend
│       ├── src/
│       │   ├── main/             # Electron main process
│       │   ├── preload.js        # Secure IPC bridge
│       │   └── renderer/         # UI (HTML/CSS/JS)
│       └── package.json
│
├── build/                       # Build scripts
├── docs/                        # Documentation
└── assets/                      # Application assets
```

> **Backend**: The backend is maintained in a separate repository: [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend)

### Running Tests

```bash
# Electron frontend E2E tests
cd src/frontend-electron
npm test

# Frontend linting
npm run lint
```

### Building for Release

```powershell
# Build frontend
cd build
.\build.ps1 -Release -Version "2.0.0"

# Build with Windows installer
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

This creates:
- `build/output/` - Application files
- `build/VoxTether-2.0.0-win-x64.zip` - Portable ZIP
- `build/installer/VoxTether-2.0.0-Setup.exe` - Windows installer (if `-CreateInstaller`)

### CI/CD

The project uses GitHub Actions for continuous integration and release:

- **Frontend CI** (`.github/workflows/ci-frontend.yml`): Linting, build, and Playwright E2E tests on frontend changes
- **Frontend Release** (`.github/workflows/release-frontend.yml`): Creates Windows installer and portable ZIP

## Privacy

- **No network calls**: All processing is done locally (except optional update checks and model downloads)
- **No telemetry**: No usage data is collected
- **No cloud**: Your voice recordings never leave your computer
- Recordings are temporary and deleted after transcription

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) - Python FastAPI backend
- [faster-whisper](https://github.com/SYSTRAN/faster-whisper) - Fast Whisper transcription
- [CTranslate2](https://github.com/OpenNMT/CTranslate2) - Efficient inference engine
- [Electron](https://www.electronjs.org/) - Cross-platform desktop framework
