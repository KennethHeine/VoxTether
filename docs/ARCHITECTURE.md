# VoxTether Architecture

This document describes the hybrid architecture of VoxTether, a push-to-talk dictation application for Windows.

## Overview

VoxTether uses a hybrid architecture combining:

- **Frontend**: WinUI 3 (.NET 8.0) - Modern Windows UI with Fluent Design
- **Backend**: Python FastAPI - Speech-to-text transcription using faster-whisper

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Frontend** | WinUI 3 / .NET 8.0 | Windows UI, system tray, hotkeys, audio recording |
| **Backend** | Python / FastAPI | Transcription engine, model management |
| **Transcription** | faster-whisper (CTranslate2) | GPU/CPU speech-to-text |
| **GPU Support** | CUDA 12 (native) | Hardware acceleration |

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        User's Windows PC                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────┐                               │
│  │         VoxTether.exe (Frontend)         │                               │
│  │       .NET 8.0 WinUI 3 Application       │                               │
│  ├─────────────────────────────────────────┤                               │
│  │  • System Tray Icon (H.NotifyIcon)       │                               │
│  │  • Global Hotkey Detection               │                               │
│  │  • Audio Recording (NAudio)              │                               │
│  │  • Settings UI (XAML + Fluent Design)    │                               │
│  │  • Text Injection (Clipboard/SendKeys)   │                               │
│  └─────────────────┬───────────────────────┘                               │
│                    │                                                        │
│                    │ HTTP REST API                                          │
│                    │ (localhost:5678)                                       │
│                    ▼                                                        │
│  ┌─────────────────────────────────────────┐                               │
│  │      vox-backend.exe (Backend)          │                               │
│  │      Python + FastAPI + PyInstaller     │                               │
│  ├─────────────────────────────────────────┤                               │
│  │  • REST API for transcription           │                               │
│  │  • faster-whisper integration           │                               │
│  │  • Model management (HuggingFace)       │                               │
│  │  • CUDA/CPU device management           │                               │
│  └─────────────────┬───────────────────────┘                               │
│                    │                                                        │
│                    ▼                                                        │
│  ┌─────────────────────────────────────────┐                               │
│  │           GPU (CUDA) / CPU               │                               │
│  │     (faster-whisper processing)          │                               │
│  └─────────────────────────────────────────┘                               │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### Frontend (WinUI 3 / .NET 8.0)

| Component | Location | Purpose |
|-----------|----------|---------|
| `VoxTether.exe` | `src/frontend/VoxTether/` | Main WinUI 3 application |
| `TrayIconManager` | `Services/TrayIconManager.cs` | System tray icon and menu |
| `VoxTetherController` | `Services/VoxTetherController.cs` | Orchestrates recording workflow |
| `BackendClient` | `Services/BackendClient.cs` | HTTP client for backend API |
| `BackendProcessManager` | `Services/BackendProcessManager.cs` | Starts/stops backend process |
| `NAudioRecorder` | `VoxTether.Infrastructure/` | Audio recording using NAudio |
| `LowLevelHookHotkeyService` | `VoxTether.Infrastructure/` | Global keyboard hooks |
| `ClipboardTextInjector` | `VoxTether.Infrastructure/` | Text injection via clipboard |
| `SettingsService` | `Services/SettingsService.cs` | User settings management |

### Backend (Python / FastAPI)

| Component | Location | Purpose |
|-----------|----------|---------|
| `main.py` | `src/backend/` | FastAPI application entry point |
| `TranscriberService` | `services/transcriber.py` | faster-whisper integration |
| `ModelManager` | `services/model_manager.py` | Model download and management |
| `health.py` | `api/health.py` | Health check endpoints |
| `transcribe.py` | `api/transcribe.py` | Transcription endpoints |
| `models.py` | `api/models.py` | Model management endpoints |

---

## REST API

The frontend communicates with the backend via HTTP REST API on `localhost:5678`.

### Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/health` | Health check and status |
| `GET` | `/api/devices` | Get GPU/CPU device info |
| `POST` | `/api/transcribe` | Transcribe audio file |
| `GET` | `/api/models` | List available models |
| `POST` | `/api/models/{name}/download` | Download a model (SSE) |
| `POST` | `/api/models/{name}/load` | Load a model |
| `DELETE` | `/api/models/{name}` | Delete a model |
| `POST` | `/api/settings` | Update transcription settings |

### Example: Transcription Request

```http
POST /api/transcribe
Content-Type: multipart/form-data

file: <audio.wav>
language: auto
translate: false
```

**Response:**
```json
{
  "text": "Hello, this is a test.",
  "language": "en",
  "duration": 0.82,
  "success": true
}
```

---

## Core Workflow

```
User holds hotkey              User releases hotkey
       │                              │
       ▼                              ▼
┌─────────────┐               ┌──────────────┐
│ Start Audio │               │ Stop Audio   │
│  Recording  │──────────────▶│  Recording   │
│  (NAudio)   │               │  (NAudio)    │
└─────────────┘               └──────┬───────┘
                                     │
                                     ▼
                              ┌──────────────┐
                              │ Save to WAV  │
                              │    File      │
                              └──────┬───────┘
                                     │
                                     ▼
                              ┌──────────────┐
                              │  HTTP POST   │
                              │  to Backend  │
                              │ /api/transcribe
                              └──────┬───────┘
                                     │
                                     ▼
                              ┌──────────────┐
                              │ Backend runs │
                              │faster-whisper│
                              └──────┬───────┘
                                     │
                                     ▼
                              ┌──────────────┐
                              │ Return JSON  │
                              │  with text   │
                              └──────┬───────┘
                                     │
                                     ▼
                              ┌──────────────┐
                              │ Inject Text  │
                              │ (Clipboard + │
                              │  Ctrl+V)     │
                              └──────────────┘
```

---

## Project Structure

```
VoxTether/
├── src/
│   ├── frontend/                     # WinUI 3 Frontend
│   │   ├── VoxTether/                # Main WinUI 3 project
│   │   │   ├── App.xaml              # Application entry
│   │   │   ├── MainWindow.xaml       # Settings window
│   │   │   ├── Views/                # Settings pages
│   │   │   │   ├── GeneralSettingsPage.xaml
│   │   │   │   ├── AudioSettingsPage.xaml
│   │   │   │   ├── ModelsPage.xaml
│   │   │   │   └── AboutPage.xaml
│   │   │   ├── ViewModels/           # MVVM view models
│   │   │   ├── Services/             # Application services
│   │   │   │   ├── BackendClient.cs
│   │   │   │   ├── BackendProcessManager.cs
│   │   │   │   ├── SettingsService.cs
│   │   │   │   ├── TrayIconManager.cs
│   │   │   │   └── VoxTetherController.cs
│   │   │   └── Assets/               # Icons and resources
│   │   ├── VoxTether.Core/           # Interfaces and models
│   │   │   ├── Interfaces/
│   │   │   │   ├── IAudioRecorder.cs
│   │   │   │   ├── IBackendClient.cs
│   │   │   │   ├── IHotkeyService.cs
│   │   │   │   └── ITextInjector.cs
│   │   │   └── Models/
│   │   │       └── VoxTetherSettings.cs
│   │   ├── VoxTether.Infrastructure/  # Platform implementations
│   │   │   ├── NAudioRecorder.cs
│   │   │   ├── ClipboardTextInjector.cs
│   │   │   └── LowLevelHookHotkeyService.cs
│   │   └── VoxTether.sln             # Solution file
│   │
│   ├── backend/                      # Python Backend
│   │   ├── api/
│   │   │   ├── __init__.py
│   │   │   ├── health.py
│   │   │   ├── transcribe.py
│   │   │   └── models.py
│   │   ├── services/
│   │   │   ├── __init__.py
│   │   │   ├── transcriber.py
│   │   │   └── model_manager.py
│   │   ├── main.py                   # FastAPI entry point
│   │   ├── config.py                 # Configuration
│   │   └── requirements.txt
│   │
│   └── (legacy Python code)          # Original Python implementation
│       ├── main.py
│       ├── tray.py
│       └── ...
│
├── installer/
│   └── VoxTether.iss                 # Inno Setup script
│
├── build/
│   └── build.ps1                     # Build script
│
├── docs/
│   ├── ARCHITECTURE.md               # This document
│   ├── INSTALLATION.md               # Installation guide
│   └── HYBRID-ARCHITECTURE-PLAN.md   # Original planning doc
│
├── .github/workflows/
│   ├── ci.yml                        # CI pipeline
│   └── release.yml                   # Release pipeline
│
└── README.md
```

---

## Frontend Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime |
| WinUI 3 | 1.5 | UI Framework |
| Windows App SDK | 1.5 | Windows integration |
| NAudio | 2.2 | Audio recording |
| H.NotifyIcon | 2.1 | System tray |
| CommunityToolkit.Mvvm | 8.2 | MVVM framework |

---

## Backend Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| Python | 3.11+ | Runtime |
| FastAPI | 0.109+ | Web framework |
| faster-whisper | 1.0+ | Transcription |
| uvicorn | 0.27+ | ASGI server |
| huggingface-hub | 0.20+ | Model downloads |

---

## Process Management

The frontend manages the backend process lifecycle:

1. **Startup**: Frontend starts → Launches `backend/vox-backend.exe` → Waits for health check
2. **Runtime**: Frontend sends HTTP requests to backend
3. **Shutdown**: Frontend terminates → Kills backend process

```csharp
// BackendProcessManager.cs
public async Task StartAsync()
{
    _process = Process.Start(new ProcessStartInfo
    {
        FileName = "backend/vox-backend.exe",
        CreateNoWindow = true,
        UseShellExecute = false,
    });
    
    await WaitForHealthyAsync(timeout: TimeSpan.FromSeconds(30));
}
```

---

## Data Storage

| Data | Location |
|------|----------|
| Settings | `%APPDATA%\VoxTether\settings.json` |
| Models | `%APPDATA%\VoxTether\models\` |
| Logs | `%APPDATA%\VoxTether\logs\` |

### Settings Structure

```json
{
  "Hotkey": "Ctrl+Shift+Space",
  "ModelName": "small",
  "Language": "auto",
  "OutputMode": "ClipboardAndPaste",
  "ShowNotifications": true,
  "ShowRecordingIndicator": true,
  "AudioDeviceId": -1,
  "ClipboardDelayMs": 50,
  "BackendPort": 5678,
  "StartMinimized": true,
  "StartWithWindows": false,
  "Theme": "System"
}
```

---

## Build and Release

### Local Development

```powershell
# Build backend
cd src/backend
pip install -r requirements.txt
python -m uvicorn main:app --port 5678

# Build frontend (new terminal)
cd src/frontend
dotnet build
dotnet run --project VoxTether
```

### Release Build

```powershell
# Build everything + create installer
cd build
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

### CI/CD Pipeline

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ build-backend│    │build-frontend│    │  test-python │
│   (Python)   │    │  (WinUI 3)   │    │   (pytest)   │
└──────┬───────┘    └──────┬───────┘    └──────┬───────┘
       │                   │                   │
       └───────────────────┴───────────────────┘
                           │
                           ▼
                  ┌──────────────────┐
                  │  build-complete  │
                  │ (verify + upload)│
                  └──────────────────┘
```

---

## Security

- Backend binds to `127.0.0.1` only (localhost)
- No authentication required (local-only communication)
- Temporary audio files deleted after transcription
- No telemetry or network calls (except HuggingFace downloads)

---

## Performance Targets

| Metric | Target |
|--------|--------|
| Frontend startup | < 1 second |
| Backend startup | < 5 seconds |
| Model loading | < 30 seconds |
| Recording latency | < 100ms |
| Transcription (8s audio, GPU) | < 1 second |
| Transcription (8s audio, CPU) | < 8 seconds |
| Idle memory (frontend + backend) | < 150 MB |
| Active memory (with model) | < 1.5 GB |

---

## See Also

- [Installation Guide](INSTALLATION.md) - Setup instructions
- [README](../README.md) - Project overview
- [Hybrid Architecture Plan](HYBRID-ARCHITECTURE-PLAN.md) - Original design document
