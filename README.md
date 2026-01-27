# VoxTether

Push-to-talk dictation for Windows 10/11. Fully offline, no cloud, no telemetry.

## Two Versions Available

VoxTether is available in two implementations. **For most users, we recommend the Python version.**

| Version | GPU Support | Status | Best For |
|---------|-------------|--------|----------|
| **[Python](voxtether-python/)** | ✅ Native CUDA 12 | **Active Development** | RTX 40-series, modern NVIDIA GPUs |
| [.NET](src/) | ⚠️ CUDA 11.8 only | Maintenance Mode | Older GPUs, CPU-only systems |

> 📖 **See [Installation Guide](docs/INSTALLATION.md) for detailed setup instructions.**

### Why Two Versions?

The original .NET version uses whisper.cpp, which has compatibility issues with NVIDIA RTX 40-series GPUs. The Python version uses faster-whisper with native CUDA 12 support, solving these GPU issues and providing:

- **4x faster** transcription than OpenAI Whisper
- **Direct HuggingFace model support** - no manual conversion needed
- **Simpler installation** - `pip install faster-whisper` handles CUDA

## Features

- **Push-to-talk recording**: Press and hold a global hotkey to record, release to transcribe
- **Fully offline**: Uses whisper.cpp/.NET or faster-whisper/Python for local speech-to-text
- **Text insertion**: Automatically types the transcribed text at your cursor position
- **System tray**: Runs quietly in the background
- **Privacy-first**: No network calls, no telemetry, all processing is local

## Quick Install

### Python Version (Recommended)

1. Download `VoxTether-Python-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract and run `VoxTether.exe`
3. Follow the first-run setup to download a model

**From source:**
```bash
cd voxtether-python
pip install -r requirements.txt
python -m src.main
```

### .NET Version (Legacy)

1. Download `VoxTether-Setup-x.x.x.exe` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Run the installer (no admin required)
3. Launch from Start Menu

**From source:**
```bash
dotnet build --configuration Release
dotnet publish src/VoxTether/VoxTether.csproj -c Release -r win-x64 --self-contained
```

> 📖 **For detailed installation options, GPU setup, and troubleshooting, see [Installation Guide](docs/INSTALLATION.md).**

## Usage

### Default Hotkey

**Ctrl + Alt + Space** (configurable in Settings)

Press and hold the hotkey to record your voice. Release the hotkey to stop recording and transcribe.

### Changing the Hotkey

1. Right-click the tray icon
2. Select "Settings..."
3. Click the hotkey field and press your desired key combination
4. Click Save

### System Tray Menu

Right-click the VoxTether tray icon to access:

- **Settings...** - Configure hotkey, model, and options
- **Start with Windows** - Toggle auto-start on login
- **Open Models Folder** - Access user models directory
- **Open Logs** - Access log files for troubleshooting
- **Test Microphone** - Record a 2-second test and show the transcription
- **Check for Updates...** - Check for new versions on GitHub
- **About** - Show version and configuration info
- **Exit** - Close VoxTether

## Model Management

VoxTether comes with a default speech recognition model (ggml-base.bin). You can add additional models for better accuracy or different languages.

### Adding Custom Models

1. Download a whisper.cpp compatible model (.bin file) from:
   - [Hugging Face - ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp)
   
2. Place the model file in:
   - User models folder: `%APPDATA%\VoxTether\models\`
   - Or use "Open Models Folder" from the tray menu

3. Select the model in Settings

### Recommended Models

| Model | Size | Quality | Speed |
|-------|------|---------|-------|
| ggml-tiny.bin | ~75 MB | Basic | Very Fast |
| ggml-base.bin | ~142 MB | Good | Fast |
| ggml-small.bin | ~466 MB | Better | Moderate |
| ggml-medium.bin | ~1.5 GB | Great | Slow |
| ggml-large-v3.bin | ~3 GB | Best | Very Slow |

## GPU Acceleration

VoxTether supports GPU acceleration for faster transcription. The application ships with a CPU backend by default and offers on-demand download of the NVIDIA CUDA backend.

### Available Backends

- **CPU Only** (included) - Works on any system, no additional downloads required
- **NVIDIA CUDA** (downloadable) - For NVIDIA graphics cards (fastest for NVIDIA GPUs)

### First-Run Experience

On first launch, VoxTether will:
1. Detect your GPU hardware
2. Recommend CUDA backend if an NVIDIA GPU is detected
3. Offer to download the CUDA backend

You can skip this and use CPU-only mode, or download backends later from Settings.

### Downloading Backends

1. Open Settings from the tray menu
2. Navigate to the **Performance** tab
3. Scroll to **Backend Management**
4. Click **Download** next to the CUDA backend

Download sizes:
- CUDA: ~60 MB

### Managing Backends

In Settings → Performance → Backend Management, you can:
- Download the CUDA backend
- Remove installed backends to free disk space
- View backend status and requirements

For more details, see [Backend Download System Documentation](docs/backend-download-system.md).

## Configuration

Settings are stored in `%APPDATA%\VoxTether\settings.json`

### Available Settings

```json
{
  "hotkey": "Ctrl + Alt + Space",
  "modelPath": null,
  "modelName": "ggml-base.bin",
  "language": "auto",
  "startWithWindows": false,
  "showNotifications": true,
  "showRecordingIndicator": true,
  "copyToClipboard": true,
  "fallbackToTyping": true,
  "clipboardDelayMs": 100,
  "enableHardwareAcceleration": true,
  "transcriptionBackend": "Auto"
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

### GPU Acceleration Not Working

1. Ensure you have downloaded the CUDA backend for your NVIDIA GPU
2. **CUDA 11.8 Required**: The CUDA backend requires CUDA Toolkit 11.8 to be installed. Download from [NVIDIA CUDA 11.8 Archive](https://developer.nvidia.com/cuda-11-8-0-download-archive)
3. Check that GPU drivers are up to date
4. Check logs for backend-specific errors (look for "missing DLL" messages)
5. See [CUDA Troubleshooting Guide](docs/cuda-troubleshooting.md) for detailed help
6. Fall back to CPU mode if GPU acceleration issues persist

### Microphone Not Working

1. Ensure your microphone is plugged in and set as the default recording device
2. Check Windows Sound settings → Recording
3. Run "Test Microphone" from the tray menu to verify

### Hotkey Not Working

1. Check if another application is using the same hotkey
2. Try a different key combination in Settings
3. Some applications (games, full-screen apps) may capture keyboard input

### Cannot Paste into Application

VoxTether uses clipboard paste (Ctrl+V) to insert text. If this doesn't work:

1. **Elevated applications**: If the target app runs as Administrator, VoxTether also needs to run as Administrator
2. **Password fields**: VoxTether skips injection into detected password fields for security
3. **Custom input fields**: Some applications use non-standard text input that may not accept paste

### Antivirus False Positives

VoxTether uses low-level keyboard hooks for global hotkey detection. Some antivirus software may flag this as suspicious.

To resolve:
1. Add VoxTether to your antivirus exclusions
2. Verify the download hash matches the release

### Performance Issues

If transcription is slow:
1. Try a smaller model (ggml-tiny.bin or ggml-base.bin)
2. Close resource-intensive applications
3. Ensure you have adequate CPU/RAM available

## Command Line

VoxTether supports a healthcheck command for troubleshooting:

```bash
VoxTether.exe --healthcheck
```

This will verify:
- Recording device availability
- Whisper binary presence
- Model file availability

## Updates

VoxTether includes built-in update checking:

1. Right-click the tray icon
2. Select "Check for Updates..."
3. If a new version is available, you'll be prompted to open the download page
4. Download the latest installer or portable version
5. Install over the existing installation (settings are preserved)

**Note**: Update checking requires an internet connection to reach GitHub.

### Upgrading via Installer

The VoxTether installer automatically handles upgrades:

- **No admin required**: Installs to user profile by default, no elevation needed
- **Detects existing installation**: The installer checks if VoxTether is already installed
- **Version notification**: Shows the currently installed version and the new version
- **Automatic app closure**: Closes VoxTether if it's running before upgrading
- **Settings preserved**: Your settings, models, and logs are preserved during upgrade
- **Same location**: Uses the same installation directory as the previous version

## Privacy

- **No network calls**: All processing is done locally (except optional update checks)
- **No telemetry**: No usage data is collected
- **No cloud**: Your voice recordings never leave your computer
- Recordings are temporary and deleted after transcription

## Development

> 📖 **See [Architecture Guide](docs/ARCHITECTURE.md) for detailed documentation on how both versions work.**

### .NET Architecture

```
src/
├── VoxTether/                 # WPF application
├── VoxTether.Core/            # Interfaces and core services
├── VoxTether.Infrastructure/  # NAudio recorder, hotkey hook, text injector
└── VoxTether.Transcription/   # whisper.cpp engine wrapper

tests/
└── VoxTether.Core.Tests/      # Unit tests
```

### Python Architecture

```
voxtether-python/
├── src/
│   ├── main.py                # Entry point, VoxTetherApp
│   ├── recorder.py            # Audio recording
│   ├── transcriber.py         # faster-whisper integration
│   ├── hotkey.py              # Global hotkey listener
│   ├── injector.py            # Text injection
│   └── ui/                    # Settings and setup windows
└── tests/                     # Unit tests
```

### Key Interfaces (.NET)

- `IAudioRecorder` - Audio recording to WAV
- `ITranscriptionEngine` - Speech-to-text transcription
- `ITextInjector` - Text insertion into focused applications
- `IHotkeyService` - Global hotkey detection
- `ITextPostProcessor` - Post-processing hook (V2 extension point)

## Releases

To create a new release:

```bash
git tag v1.0.0
git push --tags
```

The GitHub Actions workflow will automatically:
1. Build and test the application
2. Create a portable ZIP
3. Build the installer
4. Publish to GitHub Releases

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

### .NET Version
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) - Fast C++ implementation of OpenAI's Whisper
- [NAudio](https://github.com/naudio/NAudio) - .NET audio library

### Python Version
- [faster-whisper](https://github.com/SYSTRAN/faster-whisper) - Fast Whisper transcription with CTranslate2
- [CTranslate2](https://github.com/OpenNMT/CTranslate2) - Efficient inference engine
- [pystray](https://github.com/moses-palmer/pystray) - System tray support
- [sounddevice](https://python-sounddevice.readthedocs.io/) - Audio recording
