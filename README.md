# VoxTether

Push-to-talk dictation for Windows 10/11. Fully offline, no cloud, no telemetry.

## Features

- **Push-to-talk recording**: Press and hold a global hotkey to record, release to transcribe
- **GPU acceleration**: Native CUDA 12 support with automatic fallback to CPU
- **Fully offline**: Uses faster-whisper for local speech-to-text, no internet required after model download
- **Text insertion**: Automatically types transcribed text at your cursor position
- **System tray**: Runs quietly in the background
- **Model management**: Download models on-demand from HuggingFace
- **Privacy-first**: No network calls, no telemetry, all processing is local

## Requirements

- Windows 10/11 (64-bit)
- Python 3.10 or later
- NVIDIA GPU with CUDA 12 support (optional, for GPU acceleration)

## Installation

### Pre-built Executable (Easiest)

1. Download `VoxTether-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract and run `VoxTether.exe`
3. Follow the first-run setup to download a model

### From Source

```bash
# Clone the repository
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether

# Create virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run the application
python -m src.main
```

### GPU Acceleration (Optional)

For GPU acceleration with NVIDIA GPUs:

```bash
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
- **Check for Updates...** - Check for new versions on GitHub
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
  "hotkey": "ctrl+shift+space",
  "model_name": "small",
  "language": "auto",
  "device": "auto",
  "compute_type": "auto",
  "show_notifications": true,
  "show_recording_indicator": true,
  "output_mode": "clipboard"
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
│   ├── main.py              # Entry point
│   ├── tray.py              # System tray management
│   ├── hotkey.py            # Global hotkey listener
│   ├── recorder.py          # Audio recording
│   ├── transcriber.py       # faster-whisper integration
│   ├── injector.py          # Text injection
│   ├── settings.py          # Settings management
│   ├── model_manager.py     # Model download/management
│   └── ui/
│       ├── settings_window.py
│       └── model_setup.py
├── tests/                   # Unit tests
├── assets/
│   └── icon.ico
├── docs/                    # Documentation
├── requirements.txt
├── requirements-dev.txt
├── pyproject.toml
└── build.py
```

### Running Tests

```bash
# Install dev dependencies
pip install -r requirements-dev.txt

# Run tests
pytest

# Run with coverage
pytest --cov=src --cov-report=html
```

### Building Executable

```bash
# Install PyInstaller
pip install pyinstaller

# Build single .exe
python build.py

# Build with debug console
python build.py --debug
```

### Releases

To create a new release:

```bash
git tag v1.0.0
git push --tags
```

The GitHub Actions workflow will automatically:
1. Run tests
2. Build the executable with PyInstaller
3. Create a portable ZIP
4. Publish to GitHub Releases

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
- [pystray](https://github.com/moses-palmer/pystray) - System tray support
- [sounddevice](https://python-sounddevice.readthedocs.io/) - Audio recording
