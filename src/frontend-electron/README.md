# VoxTether Electron Frontend

This is the Electron.js-based frontend for VoxTether, a push-to-talk dictation application for Windows.

## Features

- **Modern UI**: Clean, Windows 11 Fluent-inspired interface
- **Cross-platform capable**: Built with Electron (currently Windows-focused)
- **System Tray**: Runs quietly in the background
- **Settings Management**: Full configuration UI for hotkeys, models, and preferences
- **Model Management**: Download, load, and manage speech recognition models

## Prerequisites

- Node.js 20.x or higher
- npm 10.x or higher
- Python backend running (for transcription)

## Development

### Install Dependencies

```bash
cd src/frontend-electron
npm install
```

### Run in Development Mode

```bash
npm start

# With debug logging
npm run dev
```

### Build for Production

```bash
# Create unpacked build
npm run pack

# Create distributable (installer + portable)
npm run build
```

## Project Structure

```
frontend-electron/
├── package.json           # Project configuration
├── src/
│   ├── main.js           # Electron main process
│   ├── preload.js        # Preload script for secure IPC
│   └── renderer/
│       ├── index.html    # Main UI HTML
│       ├── styles.css    # Styles (Fluent Design inspired)
│       └── renderer.js   # UI logic and event handling
├── assets/
│   └── icon.ico          # Application icon (optional)
└── dist/                 # Build output (gitignored)
```

## Architecture

The Electron frontend communicates with the Python backend via HTTP REST API:

```
┌─────────────────────────────┐
│  Electron App (Frontend)    │
├─────────────────────────────┤
│  Main Process               │
│  - Window management        │
│  - System tray              │
│  - Backend process mgmt     │
│  - Global hotkeys           │
├─────────────────────────────┤
│  Renderer Process           │
│  - Settings UI              │
│  - Model management         │
│  - Theme handling           │
└──────────┬──────────────────┘
           │ HTTP (localhost:5678)
           ▼
┌─────────────────────────────┐
│  Python Backend             │
│  - FastAPI server           │
│  - faster-whisper           │
│  - Model management         │
└─────────────────────────────┘
```

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| Electron | 40.x | Desktop framework |
| Node.js | 20.x | Runtime |
| electron-builder | 25.x | Build and packaging |

## API Endpoints Used

The frontend communicates with the backend at `http://127.0.0.1:5678`:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/health` | GET | Health check |
| `/api/devices` | GET | Get GPU/CPU info |
| `/api/models` | GET | List available models |
| `/api/models/{name}/download` | POST | Download a model (SSE) |
| `/api/models/{name}/load` | POST | Load a model |
| `/api/models/{name}` | DELETE | Delete a model |
| `/api/transcribe` | POST | Transcribe audio file |

## Configuration

Settings are stored in the user's app data directory:
- Windows: `%APPDATA%\VoxTether\settings.json`

### Settings Structure

```json
{
  "hotkey": "Ctrl+Shift+Space",
  "modelName": "small",
  "language": "auto",
  "outputMode": "ClipboardAndPaste",
  "showNotifications": true,
  "showRecordingIndicator": true,
  "audioDeviceId": -1,
  "clipboardDelayMs": 50,
  "startMinimized": true,
  "startWithWindows": false,
  "theme": "system"
}
```

## Building for Release

The build process uses electron-builder:

```bash
# Build Windows installer and portable
npm run build
```

Output:
- `dist/VoxTether Setup.exe` - NSIS installer
- `dist/VoxTether.exe` - Portable executable

## License

MIT License - see [LICENSE](../../LICENSE) for details.
