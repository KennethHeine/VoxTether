# VoxTether

Push-to-talk dictation for Windows 10/11. Fully offline, no cloud, no telemetry.

## Features

- **Push-to-talk recording**: Press and hold a global hotkey to record, release to transcribe
- **GPU acceleration**: Native CUDA 12 support with automatic fallback to CPU
- **Fully offline**: Uses faster-whisper for local speech-to-text, no internet required after model download
- **Modern UI**: Built with Electron for a clean, Windows 11 Fluent-inspired interface
- **Text insertion**: Automatically types transcribed text at your cursor position
- **System tray**: Runs quietly in the background
- **Model management**: Download models on-demand from HuggingFace
- **Privacy-first**: No network calls, no telemetry, all processing is local

## Architecture

VoxTether uses a hybrid architecture:
- **Frontend**: Electron 40.x - Modern JavaScript/HTML/CSS UI with system tray integration
- **Backend**: Python FastAPI - Speech-to-text transcription using faster-whisper

See [Architecture Documentation](docs/ARCHITECTURE.md) for details.

## Requirements

- Windows 10/11 (64-bit)
- NVIDIA GPU with CUDA 12 support (optional, for GPU acceleration)

## Installation

### Windows Installer (Recommended)

1. Download `VoxTether-x.x.x-Setup.exe` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Run the installer and follow the wizard
3. Launch VoxTether from the Start Menu
4. On first launch, download a speech recognition model

### Portable ZIP

1. Download `VoxTether-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract and run `VoxTether.exe`
3. Follow the first-run setup to download a model

### From Source (Development)

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

### GPU Acceleration (Optional)

For GPU acceleration with NVIDIA GPUs:

```powershell
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Or install CUDA 12 Toolkit from NVIDIA.

> 📖 **For detailed installation options, GPU setup, and troubleshooting, see [Installation Guide](docs/INSTALLATION.md).**

## Usage

### Default Hotkey

**Ctrl + Shift + Space** (configurable in Settings)

Press and hold the hotkey to record your voice. Release the hotkey to stop recording and transcribe.

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

## Available Models

| Model | Size | Quality | Speed | Recommended For |
|-------|------|---------|-------|-----------------|
| tiny | ~75 MB | Basic | Very Fast | Quick notes, low-resource systems |
| base | ~142 MB | Good | Fast | General use |
| small | ~466 MB | Better | Moderate | **Recommended for most users** |
| medium | ~1.5 GB | Great | Slow | When accuracy is important |
| large-v3 | ~3 GB | Best | Very Slow | When accuracy is critical |
| large-v3-turbo | ~1.6 GB | Excellent | Fast | Best balance of speed and accuracy |
| distil-large-v3 | ~1.1 GB | Excellent | Fast | Fast high-quality transcription |

## Configuration

Settings are stored in `%APPDATA%\VoxTether\settings.json`

### Available Settings

```json
{
  "hotkey": "Ctrl+Shift+Space",
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

### Device Options

- `auto` - Automatically detect and use GPU if available
- `cuda` - Force NVIDIA GPU usage
- `cpu` - Force CPU usage

### Compute Type Options

- `auto` - Use optimal precision for device (float16 for GPU, int8 for CPU)
- `float16` - Half precision (faster on GPU)
- `int8` - Integer quantization (faster on CPU)
- `float32` - Full precision (highest accuracy)

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

### GPU Not Detected

1. Ensure NVIDIA drivers are up to date
2. Install CUDA packages: `pip install nvidia-cublas-cu12 nvidia-cudnn-cu12`
3. Run healthcheck: `python -m src.main --healthcheck`

### Audio Not Recording

1. Check Windows Sound settings → Recording
2. Ensure microphone is set as default device
3. Test with Settings → Test Microphone

### Hotkey Not Working

1. Check if another application is using the same hotkey
2. Try a different key combination in Settings
3. Run as Administrator if targeting elevated apps

### Antivirus False Positives

VoxTether uses low-level keyboard hooks for global hotkey detection. Some antivirus software may flag this as suspicious.

To resolve:
1. Add VoxTether to your antivirus exclusions
2. Verify the download hash matches the release

## Command Line

```bash
# Run with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck

# Show version
python -m src.main --version
```

## Performance Targets

| Metric | Target |
|--------|--------|
| Startup time | < 3 seconds |
| Recording latency | < 100ms |
| Transcription (8s audio, small model, GPU) | < 1 second |
| Transcription (8s audio, small model, CPU) | < 8 seconds |
| Idle memory usage | < 100 MB |
| Active memory usage (with model) | < 1 GB |

## Development

### Project Structure

```
VoxTether/
├── src/
│   ├── frontend-electron/       # Electron Frontend
│   │   ├── src/
│   │   │   ├── main.js          # Electron main process
│   │   │   ├── preload.js       # Secure IPC bridge
│   │   │   └── renderer/        # UI (HTML/CSS/JS)
│   │   └── package.json
│   │
│   ├── backend/                 # Python Backend (FastAPI)
│   │   ├── api/                 # REST API endpoints
│   │   ├── services/            # Business logic
│   │   ├── main.py              # FastAPI entry point
│   │   └── requirements.txt     # Python dependencies
│   │
│   └── (legacy Python UI)       # Original Python implementation
│       ├── main.py
│       ├── tray.py
│       └── ...
│
├── build/                       # Build scripts
├── tests/                       # Unit tests
├── docs/                        # Documentation
└── assets/                      # Application assets
```

### Running Tests

```bash
# Python backend tests
cd src/backend
pip install pytest
pytest

# Electron frontend (lint only)
cd src/frontend-electron
npm run lint
```

### Building for Release

```powershell
# Build both frontend and backend
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

- **CI Pipeline** (`.github/workflows/ci.yml`): Builds frontend, backend, and runs tests on every PR
- **Release Pipeline** (`.github/workflows/release.yml`): Creates Windows installer and portable ZIP for releases

### Releases

To create a new release:

1. Go to Actions → Release workflow
2. Click "Run workflow"
3. Enter the version number (e.g., `2.0.0`)
4. The workflow builds everything and creates a GitHub Release with installer and portable ZIP

## Privacy

- **No network calls**: All processing is done locally (except optional update checks and model downloads)
- **No telemetry**: No usage data is collected
- **No cloud**: Your voice recordings never leave your computer
- Recordings are temporary and deleted after transcription

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [faster-whisper](https://github.com/SYSTRAN/faster-whisper) - Fast Whisper transcription
- [CTranslate2](https://github.com/OpenNMT/CTranslate2) - Efficient inference engine
- [Electron](https://www.electronjs.org/) - Cross-platform desktop framework
