# VoxTether Architecture

This document describes the architecture of VoxTether, a push-to-talk dictation application.

## Overview

VoxTether is a push-to-talk dictation application that provides offline speech-to-text transcription using faster-whisper.

| Technology | Description |
|-----------|-------------|
| **Language** | Python 3.10+ |
| **Transcription** | faster-whisper (CTranslate2) |
| **GPU Support** | CUDA 12 (native) |
| **UI** | pystray (system tray) + tkinter (dialogs) |

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         User Interaction                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────────────┐  │
│  │ System Tray │    │  Settings UI │    │  Model Setup Window   │  │
│  │   Manager   │    │    Window    │    │  (First Run)          │  │
│  └──────┬──────┘    └──────┬───────┘    └───────────┬───────────┘  │
│         │                  │                        │              │
├─────────┴──────────────────┴────────────────────────┴──────────────┤
│                         Main Controller                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────┐  ┌────────────┐ │
│  │   Hotkey     │  │    Audio    │  │Transcription│ │    Text    │ │
│  │   Listener   │  │  Recorder   │  │   Engine   │  │  Injector  │ │
│  └──────┬───────┘  └──────┬──────┘  └──────┬─────┘  └──────┬─────┘ │
│         │                 │                │               │       │
├─────────┴─────────────────┴────────────────┴───────────────┴───────┤
│                     Platform / Hardware Layer                       │
├─────────────────────────────────────────────────────────────────────┤
│  Windows Keyboard │  Microphone  │  GPU/CPU   │  Clipboard/Input   │
│       Hooks       │    Input     │   Compute  │      Simulation    │
└───────────────────┴──────────────┴────────────┴────────────────────┘
```

---

## Core Workflow

VoxTether implements a push-to-talk workflow:

1. **Hotkey Press** → Start recording audio from microphone
2. **Hotkey Release** → Stop recording, create WAV file
3. **Transcription** → Send audio to speech-to-text engine
4. **Text Injection** → Insert transcribed text at cursor position
5. **Cleanup** → Delete temporary audio file

```
User holds hotkey          User releases hotkey
       │                          │
       ▼                          ▼
┌─────────────┐             ┌──────────────┐
│ Start Audio │             │ Stop Audio   │
│  Recording  │───────────▶ │  Recording   │
└─────────────┘             └──────┬───────┘
                                   │
                                   ▼
                            ┌──────────────┐
                            │ Save to WAV  │
                            │    File      │
                            └──────┬───────┘
                                   │
                                   ▼
                            ┌──────────────┐
                            │  Transcribe  │
                            │    Audio     │
                            └──────┬───────┘
                                   │
                                   ▼
                            ┌──────────────┐
                            │ Inject Text  │
                            │ (Clipboard/  │
                            │  Typing)     │
                            └──────────────┘
```

---

## Project Structure

```
VoxTether/
├── src/
│   ├── __init__.py          # Package init, version
│   ├── main.py              # Entry point, VoxTetherApp class
│   ├── tray.py              # TrayManager - system tray icon/menu
│   ├── hotkey.py            # HotkeyListener - global hotkey detection
│   ├── recorder.py          # AudioRecorder - microphone to WAV
│   ├── transcriber.py       # Transcriber - faster-whisper integration
│   ├── injector.py          # TextInjector - clipboard/typing output
│   ├── settings.py          # Settings and SettingsService
│   ├── model_manager.py     # Model download and management
│   └── ui/
│       ├── settings_window.py   # Settings dialog (tkinter)
│       └── model_setup.py       # First-run model setup
├── tests/                   # Unit tests
├── assets/                  # Application assets
│   └── icon.ico
├── docs/                    # Documentation
├── requirements.txt         # Runtime dependencies
├── requirements-dev.txt     # Dev dependencies
├── pyproject.toml          # Project configuration
└── build.py                 # PyInstaller build script
```

---

## Component Details

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

---

## Transcription Engine

VoxTether uses **faster-whisper** which wraps CTranslate2:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     faster-whisper Library                          │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐    ┌─────────────────┐    ┌───────────────────┐   │
│  │   Whisper   │───▶│   CTranslate2   │───▶│  GPU (CUDA 12)    │   │
│  │   Model     │    │   Inference     │    │  or CPU (AVX2)    │   │
│  │  (from HF)  │    │   Engine        │    │                   │   │
│  └─────────────┘    └─────────────────┘    └───────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

**Key advantages:**
- Native CUDA 12 support via CTranslate2
- Direct HuggingFace model loading (no GGML conversion)
- Automatic GPU/CPU detection and fallback
- VAD (Voice Activity Detection) filtering
- 4-8x faster than original OpenAI Whisper

---

## Dependencies

```
faster-whisper    → Speech-to-text engine
sounddevice       → Audio recording (PortAudio bindings)
soundfile         → WAV file handling
pystray           → System tray (Windows/macOS/Linux)
keyboard          → Global hotkey hooks
pyperclip         → Cross-platform clipboard
Pillow            → Tray icon image handling
huggingface_hub   → Model downloads
numpy             → Numerical operations
tqdm              → Progress bars
```

---

## Data Flow

### Settings Storage

Settings are stored in:
- `%APPDATA%\VoxTether\settings.json`

**Settings structure:**
```json
{
  "hotkey": "ctrl+shift+space",
  "model_name": "small",
  "device": "auto",
  "compute_type": "auto",
  "language": "auto",
  "show_notifications": true,
  "show_recording_indicator": true,
  "output_mode": "clipboard",
  "first_run_completed": true
}
```

### Model Storage

| Location | Purpose |
|----------|---------|
| `%APPDATA%\VoxTether\models\` | User models directory |
| HuggingFace cache | Downloaded CTranslate2 models |

### Logs

Logs are stored in:
- `%APPDATA%\VoxTether\logs\voxtether.log`

---

## Extensibility

Extend VoxTether by:
1. Adding new modules to `src/`
2. Subclassing existing classes
3. Modifying `VoxTetherApp` initialization

**Example: Custom Post-Processor**
```python
class CustomPostProcessor:
    def process(self, text: str) -> str:
        # Custom processing (e.g., LLM integration)
        return text.strip()

# Use in transcribe_and_inject
processed_text = post_processor.process(result.text)
```

---

## Audio Recording

| Aspect | Details |
|--------|---------|
| **Library** | sounddevice (PortAudio) |
| **Format** | 16kHz mono WAV |
| **Device Selection** | Device index |
| **Temp Storage** | System temp directory |

---

## Text Injection

| Aspect | Details |
|--------|---------|
| **Clipboard** | pyperclip |
| **Typing** | keyboard.write() |
| **Modes** | clipboard / focused_app |

---

## See Also

- [Installation Guide](INSTALLATION.md) - Setup instructions
- [README](../README.md) - Project overview and quick start
