# VoxTether Python

Push-to-talk dictation for Windows 10/11. Fully offline, using faster-whisper for GPU-accelerated speech-to-text.

## Why Python + faster-whisper?

This is a complete rewrite of VoxTether from C#/.NET to Python, addressing GPU compatibility issues with the original whisper.cpp implementation.

| Aspect | Original (C# + whisper.cpp) | Python (faster-whisper) |
|--------|---------------------------|-------------------------|
| GPU Support | ❌ Crashes on RTX 40-series | ✅ Native CUDA 12 support |
| New Models | Manual GGML conversion | Direct HuggingFace support |
| Performance | N/A (GPU broken) | 4x faster than OpenAI Whisper |
| Installation | Complex CUDA DLL matching | `pip install faster-whisper` |

## Features

- **Push-to-talk recording**: Press and hold a global hotkey to record, release to transcribe
- **GPU acceleration**: Native CUDA support with automatic fallback to CPU
- **Fully offline**: Uses faster-whisper for local speech-to-text, no internet required after model download
- **Text insertion**: Automatically types transcribed text at your cursor position
- **System tray**: Runs quietly in the background
- **Model management**: Download models on-demand from HuggingFace

## Requirements

- Windows 10/11 (64-bit)
- Python 3.10 or later
- NVIDIA GPU with CUDA 12 support (optional, for GPU acceleration)

## Installation

### From Source

```bash
# Clone the repository
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether/voxtether-python

# Create virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# For development
pip install -r requirements-dev.txt
```

### GPU Acceleration (Optional)

For GPU acceleration with NVIDIA GPUs:

```bash
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Or install CUDA 12 Toolkit from NVIDIA.

## Usage

### Running the Application

```bash
# From the voxtether-python directory
python -m src.main

# Or with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck
```

### Default Hotkey

**Ctrl + Shift + Space** (configurable in Settings)

Press and hold the hotkey to record your voice. Release to stop recording and transcribe.

### First Run

On first launch, VoxTether will:
1. Detect your GPU hardware
2. Prompt you to download a speech recognition model
3. Show the default hotkey configuration

### System Tray Menu

Right-click the VoxTether tray icon to access:

- **Settings...** - Configure hotkey, model, and options
- **Test Microphone** - Record a 2-second test and show the transcription
- **Open Models Folder** - Access downloaded models
- **Open Logs** - Access log files for troubleshooting
- **Check for Updates...** - Check for new versions on GitHub
- **About** - Show version and configuration info
- **Exit** - Close VoxTether

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

## Development

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

### Project Structure

```
voxtether-python/
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
├── tests/
│   ├── test_settings.py
│   ├── test_model_manager.py
│   ├── test_recorder.py
│   └── test_transcriber.py
├── assets/
│   └── icon.ico
├── requirements.txt
├── requirements-dev.txt
├── pyproject.toml
└── build.py
```

## Troubleshooting

### GPU Not Detected

1. Ensure NVIDIA drivers are up to date
2. Install CUDA 12 packages: `pip install nvidia-cublas-cu12 nvidia-cudnn-cu12`
3. Run healthcheck: `python -m src.main --healthcheck`

### Audio Not Recording

1. Check Windows Sound settings → Recording
2. Ensure microphone is set as default device
3. Test with Settings → Test Microphone

### Hotkey Not Working

1. Check if another application is using the same hotkey
2. Try a different key combination in Settings
3. Run as Administrator if targeting elevated apps

## Performance Targets

| Metric | Target |
|--------|--------|
| Startup time | < 3 seconds |
| Recording latency | < 100ms |
| Transcription (8s audio, small model, GPU) | < 1 second |
| Transcription (8s audio, small model, CPU) | < 8 seconds |
| Idle memory usage | < 100 MB |
| Active memory usage (with model) | < 1 GB |

## License

MIT License - see [LICENSE](../LICENSE) for details.

## Credits

- [faster-whisper](https://github.com/SYSTRAN/faster-whisper) - Fast Whisper transcription
- [CTranslate2](https://github.com/OpenNMT/CTranslate2) - Efficient inference engine
- [pystray](https://github.com/moses-palmer/pystray) - System tray support
