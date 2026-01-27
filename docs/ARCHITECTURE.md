# VoxTether Architecture

This document describes the architecture of VoxTether's two implementations and how they relate to each other.

## Overview

VoxTether is a push-to-talk dictation application that provides offline speech-to-text transcription. The project has two implementations:

| Version | Technology Stack | Status | GPU Support |
|---------|-----------------|--------|-------------|
| **Python** | Python 3.10+, faster-whisper, tkinter | Active Development | CUDA 12 (native) |
| **.NET** | C#/.NET 8, WPF, whisper.cpp | Maintenance Mode | CUDA 11.8 (external) |

Both versions share the same core workflow but use different technologies for each component.

---

## High-Level Architecture

Both versions follow the same architectural pattern:

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
│  │   Service    │  │  Recorder   │  │   Engine   │  │  Injector  │ │
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

Both versions implement the same push-to-talk workflow:

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

## Python Version Architecture

The Python version uses a modular design with standalone classes for each component.

### Project Structure

```
voxtether-python/
├── src/
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
├── requirements.txt         # Dependencies
└── build.py                 # PyInstaller build script
```

### Component Details

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

### Transcription Engine

The Python version uses **faster-whisper** which wraps CTranslate2:

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

### Dependencies

```
faster-whisper    → Speech-to-text engine
sounddevice       → Audio recording (PortAudio bindings)
soundfile         → WAV file handling
pystray           → System tray (Windows/macOS/Linux)
keyboard          → Global hotkey hooks
pyperclip         → Cross-platform clipboard
Pillow            → Tray icon image handling
huggingface_hub   → Model downloads
```

---

## .NET Version Architecture

The .NET version uses a layered architecture with dependency injection and interfaces.

### Project Structure

```
src/
├── VoxTether/                    # WPF Application Layer
│   ├── App.xaml.cs               # Application startup, DI container
│   ├── VoxTetherController.cs    # Main orchestration controller
│   ├── TrayIconManager.cs        # System tray management
│   ├── SettingsWindow.xaml       # Settings dialog (WPF)
│   ├── ModelSetupWindow.xaml     # First-run model setup
│   └── RecordingOverlayWindow.xaml # Visual recording indicator
│
├── VoxTether.Core/               # Core Abstractions & Services
│   ├── Interfaces/
│   │   ├── IAudioRecorder.cs     # Audio recording contract
│   │   ├── ITranscriptionEngine.cs # Transcription contract
│   │   ├── ITextInjector.cs      # Text output contract
│   │   ├── IHotkeyService.cs     # Hotkey detection contract
│   │   └── IBackendSelectionService.cs # GPU backend selection
│   ├── Models/
│   │   ├── VoxTetherSettings.cs  # Settings data model
│   │   └── TranscriptionBackendMode.cs # Backend enum
│   └── Services/
│       ├── SettingsService.cs    # Settings persistence
│       └── GitHubUpdateService.cs # Update checking
│
├── VoxTether.Infrastructure/     # Platform Implementations
│   ├── NAudioRecorder.cs         # NAudio-based recording
│   ├── ClipboardTextInjector.cs  # Clipboard + SendKeys
│   └── LowLevelHookHotkeyService.cs # Win32 keyboard hooks
│
└── VoxTether.Transcription/      # Transcription Implementations
    ├── WhisperCppEngine.cs       # whisper.cpp process wrapper
    ├── BackendSelectionService.cs # CUDA/CPU backend detection
    └── BackendDownloadService.cs  # Backend download management
```

### Component Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│                      VoxTether (WPF Application)                   │
│  ┌──────────────┐  ┌─────────────────┐  ┌───────────────────────┐ │
│  │ App.xaml.cs  │  │VoxTetherController│ │  TrayIconManager     │ │
│  │ (DI Setup)   │  │  (Orchestrator)  │  │  (System Tray)       │ │
│  └──────┬───────┘  └────────┬─────────┘  └───────────────────────┘ │
│         │                   │                                      │
│         ▼                   │                                      │
│  ServiceCollection ─────────┴──────────────────────────────────────│
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│                    VoxTether.Core (Interfaces)                     │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────┐  ┌────────────┐│
│  │IAudioRecorder│  │IHotkeyService│  │ITranscript.│  │ITextInjector││
│  │              │  │              │  │   Engine   │  │            ││
│  └──────────────┘  └─────────────┘  └────────────┘  └────────────┘│
└────────────────────────────────────────────────────────────────────┘
        │                    │                │               │
        ▼                    ▼                ▼               ▼
┌────────────────────────────────────────────────────────────────────┐
│               VoxTether.Infrastructure & Transcription              │
│  ┌──────────────┐  ┌───────────────┐  ┌────────────┐  ┌──────────┐│
│  │NAudioRecorder│  │LowLevelHook   │  │WhisperCpp  │  │Clipboard ││
│  │              │  │HotkeyService  │  │Engine      │  │TextInject││
│  └──────────────┘  └───────────────┘  └────────────┘  └──────────┘│
└────────────────────────────────────────────────────────────────────┘
```

### Transcription Engine

The .NET version uses **whisper.cpp** as an external process:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    WhisperCppEngine                                 │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐    ┌─────────────────┐    ┌───────────────────┐   │
│  │ .NET App    │───▶│  Process.Start  │───▶│  whisper.cpp      │   │
│  │ (C#)        │    │  (main.exe)     │    │  (native binary)  │   │
│  └─────────────┘    └─────────────────┘    └───────────────────┘   │
│                              │                                      │
│                              ▼                                      │
│                     ┌─────────────────┐                            │
│                     │ Backend Binary  │                            │
│                     │ (CPU or CUDA)   │                            │
│                     └─────────────────┘                            │
└─────────────────────────────────────────────────────────────────────┘
```

**Backend selection:**
- CPU backend bundled, always available
- CUDA backend downloadable (~60MB)
- Requires CUDA 11.8 DLLs for GPU acceleration

### Dependency Injection

The .NET version uses Microsoft.Extensions.DependencyInjection:

```csharp
// In App.xaml.cs ConfigureServices()
services.AddSingleton<SettingsService>();
services.AddSingleton<IAudioRecorder, NAudioRecorder>();
services.AddSingleton<IHotkeyService, LowLevelHookHotkeyService>();
services.AddSingleton<ITranscriptionEngine, WhisperCppEngine>();
services.AddSingleton<ITextInjector, ClipboardTextInjector>();
services.AddSingleton<VoxTetherController>();
```

### Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IAudioRecorder` | Start/stop recording, get devices |
| `IHotkeyService` | Register hotkeys, press/release events |
| `ITranscriptionEngine` | Async transcription with options |
| `ITextInjector` | Output text via clipboard or typing |
| `IBackendSelectionService` | Select CPU/CUDA backend |
| `IBackendDownloadService` | Download GPU backends |
| `IUpdateService` | Check for application updates |

---

## Comparison: Python vs .NET

### Architecture Patterns

| Aspect | Python | .NET |
|--------|--------|------|
| **Pattern** | Simple module pattern | Interface-based DI |
| **UI Framework** | pystray + tkinter | WPF (XAML) |
| **Dependency Management** | Direct instantiation | Microsoft.Extensions.DI |
| **Configuration** | dataclass + JSON | POCO model + JSON |
| **Logging** | Python logging | Microsoft.Extensions.Logging |
| **Testing** | pytest | xUnit |

### Transcription Approach

| Aspect | Python (faster-whisper) | .NET (whisper.cpp) |
|--------|------------------------|-------------------|
| **Integration** | Native Python library | External process |
| **Model Format** | HuggingFace (auto-download) | GGML (.bin files) |
| **GPU Library** | CTranslate2 (CUDA 12) | whisper.cpp (CUDA 11.8) |
| **Fallback** | Automatic to CPU | Requires backend switch |
| **RTX 40-series** | ✅ Works | ❌ Compatibility issues |

### Audio Recording

| Aspect | Python | .NET |
|--------|--------|------|
| **Library** | sounddevice (PortAudio) | NAudio |
| **Format** | 16kHz mono WAV | 16kHz mono WAV |
| **Device Selection** | Device index | Device ID |

### Text Injection

| Aspect | Python | .NET |
|--------|--------|------|
| **Clipboard** | pyperclip | System.Windows.Clipboard |
| **Typing** | keyboard.write() | SendKeys / InputSimulator |
| **Modes** | clipboard / focused_app | clipboard / paste / type |

---

## Data Flow

### Settings Storage

Both versions store settings in the same location:
- `%APPDATA%\VoxTether\settings.json`

**Python settings structure:**
```json
{
  "hotkey": "ctrl+shift+space",
  "model_name": "small",
  "device": "auto",
  "compute_type": "auto"
}
```

**.NET settings structure:**
```json
{
  "Hotkey": "Ctrl + Alt + Space",
  "ModelName": "ggml-base.bin",
  "TranscriptionBackend": "Auto",
  "EnableHardwareAcceleration": true
}
```

### Model Storage

| Version | Model Location | Format |
|---------|---------------|--------|
| Python | `%APPDATA%\VoxTether\models\` + HuggingFace cache | HuggingFace CT2 |
| .NET | `%APPDATA%\VoxTether\models\` | GGML .bin |

---

## Extensibility

### Python Version

Extend by:
1. Subclassing existing classes
2. Adding new modules to `src/`
3. Modifying `VoxTetherApp` initialization

### .NET Version

Extend by:
1. Implementing interfaces in `VoxTether.Core`
2. Registering implementations in DI container
3. Adding new projects to the solution

**Example: Custom Post-Processor**
```csharp
// Implement interface
public class CustomPostProcessor : ITextPostProcessor
{
    public Task<string> ProcessAsync(string text, CancellationToken ct)
    {
        // Custom processing (e.g., LLM integration)
        return Task.FromResult(text.Trim());
    }
}

// Register in DI
services.AddSingleton<ITextPostProcessor, CustomPostProcessor>();
```

---

## Version Interoperability

The two versions are **independent implementations** and do not communicate or share runtime state. However, they share:

1. **Settings location** - Same `%APPDATA%\VoxTether\` folder
2. **User models folder** - Models can be shared (with format conversion)
3. **Log location** - `%APPDATA%\VoxTether\logs\`

**Note:** Model formats are not compatible:
- Python uses HuggingFace CTranslate2 format
- .NET uses GGML format (.bin files)

---

## Future Architecture Considerations

Potential improvements (not implemented):

1. **Shared Settings Format** - Unified settings schema for both versions
2. **Model Conversion** - Automatic GGML ↔ CT2 conversion
3. **Plugin System** - Loadable plugins for post-processing
4. **IPC** - Inter-process communication for shared state
5. **Web UI** - Electron or web-based UI for cross-platform consistency

---

## See Also

- [Installation Guide](INSTALLATION.md) - Setup instructions for both versions
- [CUDA Troubleshooting](cuda-troubleshooting.md) - GPU acceleration issues
- [Backend Download System](backend-download-system.md) - .NET backend management
