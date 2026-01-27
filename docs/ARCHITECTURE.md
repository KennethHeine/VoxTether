# VoxTether Architecture

This document describes the architecture of VoxTether, a push-to-talk dictation application for Windows.

## Overview

VoxTether is built with C# and .NET 8.0, using WPF for the UI and whisper.cpp for speech-to-text transcription.

| Aspect | Details |
|--------|---------|
| **Platform** | Windows 10/11 (64-bit) |
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Transcription** | whisper.cpp (external process) |
| **GPU Support** | CUDA 11.8 (optional) |

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

VoxTether implements a push-to-talk workflow:

1. **Hotkey Press** → Start recording audio from microphone
2. **Hotkey Release** → Stop recording, create WAV file
3. **Transcription** → Send audio to whisper.cpp
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

tests/
└── VoxTether.Core.Tests/         # Unit tests (xUnit)
```

---

## Component Diagram

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

---

## Transcription Engine

VoxTether uses **whisper.cpp** as an external process:

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

---

## Dependency Injection

VoxTether uses Microsoft.Extensions.DependencyInjection:

```csharp
// In App.xaml.cs ConfigureServices()
services.AddSingleton<SettingsService>();
services.AddSingleton<IAudioRecorder, NAudioRecorder>();
services.AddSingleton<IHotkeyService, LowLevelHookHotkeyService>();
services.AddSingleton<ITranscriptionEngine, WhisperCppEngine>();
services.AddSingleton<ITextInjector, ClipboardTextInjector>();
services.AddSingleton<VoxTetherController>();
```

---

## Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IAudioRecorder` | Start/stop recording, get devices |
| `IHotkeyService` | Register hotkeys, press/release events |
| `ITranscriptionEngine` | Async transcription with options |
| `ITextInjector` | Output text via clipboard or typing |
| `IBackendSelectionService` | Select CPU/CUDA backend |
| `IBackendDownloadService` | Download GPU backends |
| `IUpdateService` | Check for application updates |
| `ITextPostProcessor` | Post-processing hook (V2 extension point) |

---

## Audio Recording

| Aspect | Details |
|--------|---------|
| **Library** | NAudio |
| **Format** | 16kHz mono WAV |
| **Device Selection** | Device ID |

---

## Text Injection

| Aspect | Details |
|--------|---------|
| **Clipboard** | System.Windows.Clipboard |
| **Typing** | SendKeys / InputSimulator |
| **Modes** | clipboard / paste / type |

---

## Data Flow

### Settings Storage

Settings are stored in:
- `%APPDATA%\VoxTether\settings.json`

**Settings structure:**
```json
{
  "Hotkey": "Ctrl + Alt + Space",
  "ModelName": "ggml-base.bin",
  "TranscriptionBackend": "Auto",
  "EnableHardwareAcceleration": true
}
```

### Model Storage

| Location | Format |
|----------|--------|
| `%APPDATA%\VoxTether\models\` | GGML .bin |

---

## Extensibility

Extend VoxTether by:
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

## See Also

- [Installation Guide](INSTALLATION.md) - Setup instructions
- [CUDA Troubleshooting](cuda-troubleshooting.md) - GPU acceleration issues
- [Backend Download System](backend-download-system.md) - Backend management
