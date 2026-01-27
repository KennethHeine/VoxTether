# AGENTS.md

This file provides context and instructions to help AI coding agents work effectively on the VoxTether project.

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It is fully offline, using faster-whisper for local speech-to-text transcription. The project is built with Python 3.10+.

## Setup Commands

```bash
# Create virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Install dev dependencies
pip install -r requirements-dev.txt

# Run the application
python -m src.main

# Run with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck
```

## Architecture

```
VoxTether/
├── src/
│   ├── main.py              # Entry point, VoxTetherApp
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
├── assets/                  # Application assets (icons)
├── docs/                    # Documentation
├── requirements.txt         # Runtime dependencies
├── requirements-dev.txt     # Development dependencies
├── pyproject.toml          # Project configuration
└── build.py                # PyInstaller build script
```

## Key Components

- `VoxTetherApp` - Main application controller that orchestrates all components
- `TrayManager` - System tray icon with context menu (pystray)
- `HotkeyListener` - Global push-to-talk hotkey detection (keyboard library)
- `AudioRecorder` - Records microphone to 16kHz mono WAV (sounddevice, soundfile)
- `Transcriber` - GPU/CPU speech-to-text (faster-whisper)
- `TextInjector` - Clipboard paste or keyboard simulation (pyperclip, keyboard)
- `SettingsService` - Load/save user preferences (JSON)
- `ModelManager` - Download/manage Whisper models (huggingface_hub)

## Code Style

- Python 3.10+
- Follow PEP 8 style guidelines
- Use type hints where appropriate
- Use ruff for linting

## Testing

- Unit tests are located in `tests/`
- Run tests with: `pytest`
- Run with coverage: `pytest --cov=src --cov-report=html`
- Tests use pytest framework

## CI/CD

- CI workflow is defined in `.github/workflows/ci.yml`
- Runs on pull requests to main branch
- Runs linting (ruff) and tests (pytest)

## Platform

- Windows only (requires Windows-specific libraries for keyboard hooks)
- Targets Python 3.10+
- Uses faster-whisper for transcription (native CUDA 12 support)
- Uses sounddevice for audio recording

## Building Executable

```bash
# Build single .exe with PyInstaller
python build.py

# Build with debug console
python build.py --debug
```
