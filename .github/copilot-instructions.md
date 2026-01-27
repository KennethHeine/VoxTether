# Copilot Instructions for VoxTether

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It provides fully offline speech-to-text using faster-whisper. The project is built with Python 3.13+.

**Key characteristics:**
- Windows-only desktop application
- Uses faster-whisper for transcription (native CUDA 12 support)
- Uses sounddevice for audio recording
- Uses pystray for system tray
- Uses keyboard for global hotkeys
- MIT License

## Build and Test Commands

**Always run commands from the repository root directory.**

### Required Commands (in order)

```bash
# 1. Create virtual environment (first time only)
python -m venv venv
venv\Scripts\activate

# 2. Install dependencies
pip install -r requirements.txt

# 3. Install dev dependencies (for testing)
pip install -r requirements-dev.txt

# 4. Run tests
pytest

# 5. Run linting
ruff check src/ tests/
```

### Running the Application

```bash
# Run the application
python -m src.main

# Run with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck
```

### Important Notes

- **Windows only**: The application uses Windows-specific features for keyboard hooks and system tray.
- **Linting**: Use ruff for linting (`ruff check src/ tests/`).
- **Testing**: Use pytest for testing.
- **GPU optional**: CUDA 12 support is optional; the app falls back to CPU mode.

### Building Executable

```bash
# Build single .exe with PyInstaller
python build.py

# Build with debug console
python build.py --debug
```

## Project Architecture

```
VoxTether/
├── src/
│   ├── __init__.py           # Package init, version
│   ├── main.py               # Entry point, VoxTetherApp
│   ├── tray.py               # System tray management
│   ├── hotkey.py             # Global hotkey listener
│   ├── recorder.py           # Audio recording
│   ├── transcriber.py        # faster-whisper integration
│   ├── injector.py           # Text injection
│   ├── settings.py           # Settings management
│   ├── model_manager.py      # Model download/management
│   └── ui/
│       ├── settings_window.py  # Settings dialog (tkinter)
│       └── model_setup.py      # First-run model setup
├── tests/                    # Unit tests (pytest)
├── assets/
│   └── icon.ico             # Application icon
├── docs/                    # Documentation
├── requirements.txt         # Runtime dependencies
├── requirements-dev.txt     # Development dependencies
├── pyproject.toml          # Project configuration
└── build.py                # PyInstaller build script
```

## Key Components

| Component | File | Library | Purpose |
|-----------|------|---------|---------|
| **VoxTetherApp** | `main.py` | - | Main controller, orchestrates all components |
| **TrayManager** | `tray.py` | pystray | System tray icon with context menu |
| **HotkeyListener** | `hotkey.py` | keyboard | Global push-to-talk hotkey detection |
| **AudioRecorder** | `recorder.py` | sounddevice, soundfile | Records microphone to 16kHz mono WAV |
| **Transcriber** | `transcriber.py` | faster-whisper | GPU/CPU speech-to-text |
| **TextInjector** | `injector.py` | pyperclip, keyboard | Clipboard paste or keyboard simulation |
| **SettingsService** | `settings.py` | json | Load/save user preferences |
| **ModelManager** | `model_manager.py` | huggingface_hub | Download/manage Whisper models |

## CI/CD Pipeline

### Pull Request CI (`.github/workflows/ci.yml`)

Runs on every PR to `main`:
1. Checkout code
2. Setup Python 3.13
3. Install dependencies
4. Run linting (ruff)
5. Run tests (pytest)

### Release Workflow (`.github/workflows/release.yml`)

Manually triggered with version input. Builds, tests, creates portable ZIP with PyInstaller.

## Code Style Guidelines

- **Python**: Follow PEP 8 style guidelines
- **Type hints**: Use type hints where appropriate
- **Linting**: Use ruff for linting
- **Formatting**: Use black for formatting (optional)

## Configuration Files

| File | Purpose |
|------|---------|
| `pyproject.toml` | Project configuration, dependencies |
| `requirements.txt` | Runtime dependencies |
| `requirements-dev.txt` | Development dependencies |
| `.github/workflows/ci.yml` | CI pipeline |
| `.github/dependabot.yml` | Automated dependency updates |
| `.gitignore` | Git ignore patterns |
| `build.py` | PyInstaller build script |

## Dependency Management

Dependencies are declared in `requirements.txt` and `pyproject.toml`:
- **faster-whisper**: Speech-to-text engine
- **sounddevice**: Audio recording
- **soundfile**: WAV file handling
- **pystray**: System tray
- **keyboard**: Global hotkeys
- **pyperclip**: Clipboard access
- **Pillow**: Image handling for icons
- **huggingface-hub**: Model downloads

## Testing

- **Framework**: pytest
- **Location**: `tests/`
- **Run tests**: `pytest`
- **Coverage**: `pytest --cov=src --cov-report=html`
- Add tests for new functionality following existing patterns in the test directory.

## Troubleshooting

### Tests fail with import errors
Make sure you're in a virtual environment and have installed all dependencies.

### CUDA not available
Install CUDA packages: `pip install nvidia-cublas-cu12 nvidia-cudnn-cu12`

### Keyboard hooks not working
Run as Administrator or check if another application is blocking keyboard hooks.

## Trust These Instructions

These instructions have been validated against the actual repository. If a command or path mentioned here fails, verify the current state of the repository as it may have changed. Only search the codebase if information here appears outdated or incomplete.
