# VoxTether Hybrid Architecture Plan

## Overview

This document outlines the plan to restructure VoxTether into a hybrid application with:
- **Frontend**: .NET 8.0 WinUI 3 application for Windows-native UI
- **Backend**: Python FastAPI server for speech-to-text transcription using faster-whisper

This architecture combines the best of both worlds:
- **Native Windows UI**: WinUI 3 provides modern Fluent Design, Windows 11 integration, native dark/light themes, and excellent system tray management
- **Python Transcription**: faster-whisper with CUDA 12 support, extensive model ecosystem, and active community

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        User's Windows PC                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────┐                               │
│  │         VoxTether.exe (Frontend)         │                               │
│  │       .NET 8.0 WinUI 3 Application       │                               │
│  ├─────────────────────────────────────────┤                               │
│  │  • System Tray Icon                      │                               │
│  │  • Global Hotkey Detection               │                               │
│  │  • Audio Recording (NAudio)              │                               │
│  │  • Settings UI (XAML + Fluent Design)    │                               │
│  │  • Recording Overlay                     │                               │
│  │  • Text Injection (Clipboard/SendKeys)   │                               │
│  └─────────────────┬───────────────────────┘                               │
│                    │                                                        │
│                    │ HTTP + WebSocket                                       │
│                    │ (localhost:5678)                                       │
│                    ▼                                                        │
│  ┌─────────────────────────────────────────┐                               │
│  │      vox-backend.exe (Backend)          │                               │
│  │      Python + FastAPI + PyInstaller     │                               │
│  ├─────────────────────────────────────────┤                               │
│  │  • REST API for transcription           │                               │
│  │  • WebSocket for audio streaming        │                               │
│  │  • faster-whisper integration           │                               │
│  │  • Model management                     │                               │
│  │  • CUDA/CPU device management           │                               │
│  └─────────────────┬───────────────────────┘                               │
│                    │                                                        │
│                    ▼                                                        │
│  ┌─────────────────────────────────────────┐                               │
│  │           GPU / CPU                      │                               │
│  │     (faster-whisper processing)          │                               │
│  └─────────────────────────────────────────┘                               │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### Frontend (.NET WPF)

| Component | Purpose |
|-----------|---------|
| `VoxTether.exe` | Main application entry point |
| `TrayIconManager` | System tray icon with context menu |
| `HotkeyService` | Global keyboard hook for push-to-talk |
| `AudioRecorder` | Record audio using NAudio |
| `BackendClient` | HTTP/WebSocket client for backend communication |
| `SettingsWindow` | XAML-based settings UI |
| `RecordingOverlay` | Visual feedback during recording |
| `TextInjector` | Paste text via clipboard or SendKeys |

### Backend (Python FastAPI)

| Component | Purpose |
|-----------|---------|
| `main.py` | FastAPI application entry point |
| `transcriber.py` | faster-whisper integration |
| `model_manager.py` | Model download and management |
| `api/transcribe.py` | REST endpoints for transcription |
| `api/stream.py` | WebSocket for real-time audio streaming |
| `api/models.py` | Model management endpoints |
| `api/health.py` | Health check and status endpoints |

## API Design

### REST Endpoints

```
POST /api/transcribe
  - Accept: audio/wav (raw bytes) or multipart/form-data
  - Body: WAV audio file
  - Query: language=auto, translate=false
  - Response: { "text": "...", "language": "en", "duration": 1.23 }

GET /api/models
  - Response: [{ "name": "small", "downloaded": true, "size_mb": 466 }]

POST /api/models/{name}/download
  - Response: Server-Sent Events with progress updates

DELETE /api/models/{name}
  - Response: { "success": true }

GET /api/devices
  - Response: { "cuda_available": true, "device_name": "RTX 3080", "cuda_version": "12.1" }

GET /api/health
  - Response: { "status": "ok", "model_loaded": true, "device": "cuda" }

POST /api/settings
  - Body: { "device": "cuda", "compute_type": "float16", "language": "auto" }
  - Response: { "success": true }
```

### WebSocket Endpoint

```
WS /api/stream
  - Client sends: Raw audio chunks (16kHz, 16-bit, mono)
  - Server sends: { "type": "partial", "text": "Hello..." }
  - Server sends: { "type": "final", "text": "Hello world", "duration": 0.8 }
```

## Directory Structure

```
VoxTether/
├── src/
│   ├── frontend/                     # .NET WinUI 3 Frontend
│   │   ├── VoxTether/                # Main WinUI 3 project
│   │   │   ├── App.xaml
│   │   │   ├── App.xaml.cs
│   │   │   ├── MainWindow.xaml
│   │   │   ├── MainWindow.xaml.cs
│   │   │   ├── TrayIconManager.cs
│   │   │   ├── Views/
│   │   │   │   ├── SettingsPage.xaml
│   │   │   │   ├── ModelsPage.xaml
│   │   │   │   └── AboutPage.xaml
│   │   │   ├── ViewModels/
│   │   │   │   ├── SettingsViewModel.cs
│   │   │   │   └── ModelsViewModel.cs
│   │   │   ├── Services/
│   │   │   │   ├── BackendClient.cs
│   │   │   │   ├── BackendProcess.cs
│   │   │   │   └── ...
│   │   │   └── Assets/
│   │   ├── VoxTether.Core/           # Interfaces and models
│   │   │   ├── Interfaces/
│   │   │   └── Models/
│   │   ├── VoxTether.Infrastructure/ # Platform implementations
│   │   │   ├── NAudioRecorder.cs
│   │   │   ├── ClipboardTextInjector.cs
│   │   │   └── LowLevelHookHotkeyService.cs
│   │   └── VoxTether.sln             # Solution file
│   │
│   └── backend/                      # Python Backend
│       ├── api/
│       │   ├── __init__.py
│       │   ├── transcribe.py
│       │   ├── stream.py
│       │   ├── models.py
│       │   └── health.py
│       ├── services/
│       │   ├── __init__.py
│       │   ├── transcriber.py
│       │   ├── model_manager.py
│       │   └── settings.py
│       ├── main.py
│       ├── config.py
│       └── requirements.txt
│
├── tests/
│   ├── frontend/                     # .NET unit tests
│   │   └── VoxTether.Core.Tests/
│   └── backend/                      # Python tests
│       └── test_*.py
│
├── build/
│   ├── build-frontend.ps1            # Build .NET frontend
│   ├── build-backend.ps1             # Build Python backend with PyInstaller
│   └── package-release.ps1           # Create release package
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── INSTALLATION.md
│   └── HYBRID-ARCHITECTURE-PLAN.md   # This document
│
├── assets/
│   └── icon.ico
│
├── README.md
└── LICENSE
```

## Communication Protocol

### Frontend → Backend Communication

1. **Startup Sequence**:
   ```
   Frontend starts → Launches backend process → Waits for health check → Ready
   ```

2. **Transcription Flow**:
   ```
   User presses hotkey
   → Frontend starts recording with NAudio
   → User releases hotkey
   → Frontend stops recording
   → Frontend sends WAV to POST /api/transcribe
   → Backend returns transcribed text
   → Frontend injects text at cursor
   ```

3. **Streaming Flow (Optional Enhancement)**:
   ```
   User presses hotkey
   → Frontend opens WebSocket connection
   → Frontend streams audio chunks in real-time
   → Backend sends partial transcription updates
   → User releases hotkey
   → Backend sends final transcription
   → Frontend injects text at cursor
   ```

### Process Management

The frontend is responsible for:
1. **Starting the backend**: On application startup
2. **Health monitoring**: Periodic health checks via `/api/health`
3. **Graceful shutdown**: Terminate backend process on exit
4. **Auto-restart**: Restart backend if it crashes

```csharp
public class BackendProcess : IDisposable
{
    private Process? _process;
    
    public async Task StartAsync()
    {
        var backendPath = Path.Combine(AppContext.BaseDirectory, "backend", "vox-backend.exe");
        _process = Process.Start(new ProcessStartInfo
        {
            FileName = backendPath,
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        
        // Wait for backend to be ready
        await WaitForHealthyAsync(timeout: TimeSpan.FromSeconds(30));
    }
    
    public void Stop()
    {
        _process?.Kill(entireProcessTree: true);
    }
}
```

## Build and Release Strategy

### Development Workflow

```powershell
# Install backend dependencies
cd src/backend
pip install -r requirements.txt

# Run backend locally (Python)
python -m uvicorn main:app --reload --port 5678

# Build frontend (in another terminal)
cd src/frontend
dotnet build

# Run frontend
cd src/frontend/VoxTether
dotnet run
```

### Release Build

```powershell
# 1. Build Python backend as standalone executable
cd src/backend
pyinstaller --onefile --name vox-backend main.py

# 2. Build .NET frontend
cd src/frontend
dotnet publish -c Release -r win-x64 --self-contained

# 3. Package together
# Copy dist/vox-backend.exe to publish/backend/
# Create VoxTether-x.x.x-win-x64.zip
```

### Release Package Structure

```
VoxTether-x.x.x-win-x64/
├── VoxTether.exe                 # .NET frontend
├── VoxTether.dll
├── *.dll                         # .NET dependencies
├── backend/
│   └── vox-backend.exe           # Python backend (single file)
├── models/                       # Pre-downloaded models (optional)
├── README.txt
└── LICENSE.txt
```

## Migration Plan

### Phase 1: Backend API Server (Week 1)

1. Create `src/backend/` directory structure
2. Port transcriber.py, model_manager.py, settings.py from current Python code
3. Implement FastAPI endpoints:
   - POST /api/transcribe
   - GET /api/models
   - POST /api/models/{name}/download
   - GET /api/devices
   - GET /api/health
4. Add PyInstaller build script
5. Write tests for API endpoints

### Phase 2: Frontend WPF Application (Week 2)

1. Create `src/frontend/` directory structure
2. Port from old .NET branch:
   - VoxTether.Core (interfaces, models)
   - VoxTether.Infrastructure (NAudioRecorder, ClipboardTextInjector, HotkeyService)
   - VoxTether WPF (App, TrayIconManager, SettingsWindow, RecordingOverlay)
3. Implement BackendClient for API communication
4. Implement BackendProcess for process management
5. Update UI to show backend status and controls
6. Remove whisper.cpp integration (replaced by Python backend)

### Phase 3: Integration and Polish (Week 3)

1. End-to-end testing
2. Error handling and recovery
3. Progress indicators for model downloads
4. Logging and diagnostics
5. Performance optimization
6. Build automation and CI/CD

### Phase 4: Release (Week 4)

1. Documentation updates
2. Release workflow updates
3. Beta testing
4. Final release

## Configuration

### Backend Configuration (config.py)

```python
import os
from pydantic import BaseSettings

class Settings(BaseSettings):
    host: str = "127.0.0.1"
    port: int = 5678
    models_path: str = os.path.join(os.environ.get("APPDATA", ""), "VoxTether", "models")
    device: str = "auto"
    compute_type: str = "auto"
    default_model: str = "small"
    
    class Config:
        env_prefix = "VOXTETHER_"
```

### Frontend Configuration

Settings stored in `%APPDATA%\VoxTether\settings.json`:

```json
{
  "hotkey": "Ctrl+Shift+Space",
  "language": "auto",
  "model_name": "small",
  "output_mode": "clipboard",
  "show_notifications": true,
  "show_recording_indicator": true,
  "backend_port": 5678
}
```

## Error Handling

### Backend Unavailable

If the frontend cannot connect to the backend:
1. Show "Backend Starting..." in tray tooltip
2. Attempt to restart backend process
3. If restart fails, show error notification
4. Provide "Restart Backend" option in tray menu

### Transcription Errors

If transcription fails:
1. Show error notification with message
2. Log error details for troubleshooting
3. Continue operating for next recording

### Model Not Found

If the selected model is not downloaded:
1. Show model setup dialog
2. Offer to download the model
3. Show progress during download

## Security Considerations

1. **Local Only**: Backend binds to `127.0.0.1` only
2. **No Authentication**: Since it's local-only, no auth needed
3. **Temp Files**: Audio files are temporary and deleted after transcription
4. **No Network**: Backend only downloads from HuggingFace (user-initiated)

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

## Future Enhancements

1. **Real-time Streaming**: WebSocket-based streaming for live transcription
2. **Multiple Languages UI**: Localized frontend UI
3. **Plugin System**: Custom post-processors for text
4. **Separate Backend Distribution**: Allow backend updates independent of frontend
5. **Linux/Mac Backend**: Platform-agnostic backend (Python) with platform-specific frontends

## Conclusion

This hybrid architecture provides:
- ✅ Native Windows experience with WPF
- ✅ Reliable transcription with faster-whisper
- ✅ Clean separation of concerns
- ✅ Easy maintenance and updates
- ✅ Future flexibility for platform expansion
