# VoxTether Architecture

This document describes the client-server architecture of VoxTether, a push-to-talk dictation application.

## Overview

VoxTether uses a **client-server architecture** with complete separation of frontend and backend:

- **Client (Frontend)**: Electron 40.x - Desktop application with UI and system tray
- **Server (Backend)**: Python FastAPI - Speech-to-text transcription service

The backend runs as a standalone Python server. It does NOT require PyInstaller or any executable bundling - just Python with the required packages.

| Component | Technology | Deployment |
|-----------|------------|------------|
| **Client** | Electron 40.x | Windows desktop application |
| **Server** | Python / FastAPI | Python script on any machine |
| **Transcription** | faster-whisper (CTranslate2) | Runs on server |
| **GPU Support** | CUDA 12 (native) | Server-side only |

---

## System Architecture

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          CLIENT (User's Windows PC)                          │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────┐                               │
│   │         VoxTether.exe (Electron)         │                               │
│   ├─────────────────────────────────────────┤                               │
│   │  • System Tray Icon                      │                               │
│   │  • Global Hotkey Detection               │                               │
│   │  • Settings UI (HTML/CSS/JS)             │                               │
│   │  • Text Injection (Clipboard)            │                               │
│   │  • Audio Recording (Web Audio API)       │                               │
│   └─────────────────┬───────────────────────┘                               │
│                     │                                                        │
└─────────────────────┼────────────────────────────────────────────────────────┘
                      │
                      │ HTTP REST API
                      │ (localhost:5678 or network)
                      ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                         SERVER (Same machine or remote)                      │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────┐                               │
│   │      Python Backend (FastAPI + Uvicorn)  │                               │
│   ├─────────────────────────────────────────┤                               │
│   │  • REST API for transcription            │                               │
│   │  • faster-whisper integration            │                               │
│   │  • Model management (HuggingFace)        │                               │
│   │  • CUDA/CPU device management            │                               │
│   └─────────────────┬───────────────────────┘                               │
│                     │                                                        │
│                     ▼                                                        │
│   ┌─────────────────────────────────────────┐                               │
│   │           GPU (CUDA) / CPU               │                               │
│   │     (faster-whisper processing)          │                               │
│   └─────────────────────────────────────────┘                               │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Deployment Options

### Option 1: Same Machine (localhost)
Both client and server run on the same Windows PC. The server binds to `127.0.0.1:5678`.

### Option 2: Network Deployment
The server runs on a dedicated machine (with GPU), and multiple clients connect over the network. The server binds to `0.0.0.0:5678`.

---

## Component Responsibilities

### Client (Electron)

| Component | Location | Purpose |
|-----------|----------|---------|
| `main.js` | `src/frontend-electron/src/` | Electron main process, window management |
| `preload.js` | `src/frontend-electron/src/` | Secure IPC bridge to renderer |
| `renderer/` | `src/frontend-electron/src/` | UI (HTML/CSS/JS) |
| System Tray | `main.js` | Tray icon and context menu |
| Backend Client | `main.js` | HTTP client for backend API |
| Settings Service | `main.js` | Load/save user preferences |

### Server (Python / FastAPI)

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
│   ├── frontend-electron/           # Electron Frontend
│   │   ├── src/
│   │   │   ├── main.js              # Electron main process
│   │   │   ├── preload.js           # Secure IPC bridge
│   │   │   └── renderer/            # UI files
│   │   │       ├── index.html       # Main HTML
│   │   │       ├── styles.css       # Styles
│   │   │       └── renderer.js      # UI logic
│   │   ├── assets/                  # Icons and resources
│   │   └── package.json             # Dependencies and build config
│   │
│   ├── backend/                     # Python Backend
│   │   ├── api/
│   │   │   ├── __init__.py
│   │   │   ├── health.py
│   │   │   ├── transcribe.py
│   │   │   └── models.py
│   │   ├── services/
│   │   │   ├── __init__.py
│   │   │   ├── transcriber.py
│   │   │   └── model_manager.py
│   │   ├── main.py                  # FastAPI entry point
│   │   ├── config.py                # Configuration
│   │   └── requirements.txt
│   │
│   └── (legacy Python code)         # Original Python implementation
│       ├── main.py
│       ├── tray.py
│       └── ...
│
├── installer/
│   └── VoxTether.iss                # Inno Setup script
│
├── build/
│   └── build.ps1                    # Build script
│
├── docs/
│   ├── ARCHITECTURE.md              # This document
│   ├── INSTALLATION.md              # Installation guide
│   └── CHANGELOG.md                 # Version history
│
├── .github/workflows/
│   ├── ci.yml                       # CI pipeline
│   └── release.yml                  # Release pipeline
│
└── README.md
```

---

## Frontend Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| Electron | 40.x | Desktop framework |
| Node.js | 20.x | Runtime |
| electron-builder | 26.x | Build and packaging |
| HTML/CSS/JS | - | UI implementation |

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

```javascript
// main.js - Backend process management
async function startBackend() {
    backendProcess = spawn(backendPath, [], {
        stdio: isDebug ? 'inherit' : 'ignore',
        detached: false
    });
    
    await waitForBackend(30000);  // Wait up to 30 seconds
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
cd src/frontend-electron
npm install
npm start
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
│   (Python)   │    │  (Electron)  │    │   (pytest)   │
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
