# VoxTether Installation Guide

This guide covers installation and setup for VoxTether, a push-to-talk dictation application for Windows.

## System Requirements

- **OS**: Windows 10/11 (64-bit)
- **Python**: 3.10 or later (for source installation)
- **GPU** (optional): NVIDIA GPU with CUDA 12 support for acceleration
- **RAM**: 4 GB minimum, 8 GB recommended
- **Disk**: 500 MB for application + 75 MB - 3 GB per model

---

## Installation Options

### Option A: Pre-built Executable (Easiest)

1. Download the latest `VoxTether-x.x.x-win-x64.zip` from [Releases](https://github.com/KennethHeine/VoxTether/releases)
2. Extract the ZIP file to a folder of your choice (e.g., `C:\Tools\VoxTether`)
3. Run `VoxTether.exe`
4. On first launch, you'll be prompted to download a speech recognition model

### Option B: From Source

```powershell
# Clone the repository
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether

# Create a virtual environment (recommended)
python -m venv venv
.\venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt

# Run the application
python -m src.main
```

---

## GPU Acceleration Setup (Optional)

For NVIDIA GPU acceleration:

```powershell
# Install CUDA libraries
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Or install the full [CUDA Toolkit 12](https://developer.nvidia.com/cuda-downloads) from NVIDIA.

**Verify GPU detection:**
```powershell
python -m src.main --healthcheck
```

Look for `✓ CUDA available` in the output.

---

## Building an Executable

To create a standalone executable:

```powershell
# Install dev dependencies (includes PyInstaller)
pip install -r requirements-dev.txt

# Build the executable
python build.py

# The executable will be in dist/VoxTether.exe
```

---

## First Run Setup

On first launch, VoxTether will:

1. **GPU Detection**: VoxTether detects your hardware
2. **Model Download**: Choose and download a speech recognition model
3. **Hotkey Configuration**: The default hotkey is shown

### Recommended Models

| Model | Size | Quality | Speed | Best For |
|-------|------|---------|-------|----------|
| tiny | ~75 MB | Basic | Very Fast | Quick notes, testing |
| base | ~142 MB | Good | Fast | General use |
| **small** | ~466 MB | Better | Moderate | **Recommended for most users** |
| medium | ~1.5 GB | Great | Slow | When accuracy matters |
| large-v3 | ~3 GB | Best | Very Slow | Maximum accuracy |
| large-v3-turbo | ~1.6 GB | Excellent | Fast | Best speed/accuracy balance |
| distil-large-v3 | ~1.1 GB | Excellent | Fast | Fast high-quality transcription |

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
# Run with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck

# Show version
python -m src.main --version
```

---

## Configuration

Settings are stored in:
- `%APPDATA%\VoxTether\settings.json`

### Settings Reference

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

**Device options:**
- `auto` - Automatically use GPU if available
- `cuda` - Force NVIDIA GPU
- `cpu` - Force CPU only

**Compute type options:**
- `auto` - Optimal for device (float16 on GPU, int8 on CPU)
- `float16` - Half precision (faster on GPU)
- `int8` - Integer quantization (faster on CPU)
- `float32` - Full precision (highest accuracy)

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

**GPU not detected:**
1. Install CUDA packages: `pip install nvidia-cublas-cu12 nvidia-cudnn-cu12`
2. Update NVIDIA drivers
3. Run healthcheck: `python -m src.main --healthcheck`

**Slow transcription:**
1. Try a smaller model (tiny or base)
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

1. Download the new version
2. Extract to the same folder (overwrites old version)
3. Your settings are preserved in `%APPDATA%\VoxTether`

---

## Uninstalling

Delete the folder containing `VoxTether.exe`.

To remove settings and models:
```powershell
Remove-Item -Recurse "$env:APPDATA\VoxTether"
```

---

## Developer Installation

For contributing to VoxTether development:

```powershell
# Clone and set up
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether

# Create virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1

# Install all dependencies including dev tools
pip install -r requirements-dev.txt

# Run linting
ruff check src/ tests/

# Run tests
pytest tests/ -v

# Run tests with coverage
pytest tests/ --cov=src --cov-report=html
```

---

## Additional Resources

- [README](../README.md) - Project overview
- [Architecture](ARCHITECTURE.md) - Technical architecture
- [GitHub Releases](https://github.com/KennethHeine/VoxTether/releases) - Download builds
- [Issues](https://github.com/KennethHeine/VoxTether/issues) - Report bugs
