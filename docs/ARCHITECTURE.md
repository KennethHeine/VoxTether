# VoxTether Architecture

This document describes the client-server architecture of VoxTether, a voice dictation application.

## Overview

VoxTether uses a **client-server architecture** with complete separation of frontend and backend:

- **Client (Frontend)**: Electron 40.x - Desktop application with UI and system tray (this repo)
- **Server (Backend)**: Python FastAPI - Speech-to-text transcription service ([VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend))

| Component | Technology | Repository |
|-----------|------------|------------|
| **Client** | Electron 40.x | [VoxTether](https://github.com/KennethHeine/VoxTether) (this repo) |
| **Server** | Python / FastAPI | [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) |
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
│                 SERVER (https://github.com/KennethHeine/VoxTether-backend)   │
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

### Client (Electron) - This Repository

| Component | Location | Purpose |
|-----------|----------|---------|
| `main/index.js` | `src/frontend-electron/src/` | Electron main process, window management |
| `preload.js` | `src/frontend-electron/src/` | Secure IPC bridge to renderer |
| `renderer/` | `src/frontend-electron/src/` | UI (HTML/CSS/JS) |
| System Tray | `main/` | Tray icon and context menu |
| Backend Client | `main/` | HTTP client for backend API |
| Settings Service | `main/` | Load/save user preferences |

### Server (Python / FastAPI) - [Separate Repository](https://github.com/KennethHeine/VoxTether-backend)

See the [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) repository for backend component details.

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

See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for full API documentation.

---

## Project Structure

```
VoxTether/                              # This repository (frontend)
├── src/
│   └── frontend-electron/              # Electron Frontend
│       ├── src/
│       │   ├── main/                   # Electron main process
│       │   ├── preload.js              # Secure IPC bridge
│       │   └── renderer/              # UI files
│       ├── assets/                     # Icons and resources
│       └── package.json               # Dependencies and build config
│
├── installer/
│   └── VoxTether.iss                   # Inno Setup script
│
├── build/
│   └── build.ps1                       # Build script
│
├── docs/                               # Documentation
│
├── .github/workflows/
│   ├── ci-frontend.yml                 # Frontend CI pipeline
│   └── release-frontend.yml           # Frontend release
│
└── README.md

VoxTether-backend/                      # Separate repository
├── api/                                # REST API endpoints
├── services/                           # Business logic
├── main.py                             # FastAPI entry point
└── requirements.txt                    # Python dependencies
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

## Data Storage

| Data | Location |
|------|----------|
| Settings | `%APPDATA%\VoxTether\settings.json` |
| Models | `%APPDATA%\VoxTether\models\` |
| Logs | `%APPDATA%\VoxTether\logs\` |

---

## Security

- Backend binds to `127.0.0.1` only (localhost)
- No authentication required (local-only communication)
- Temporary audio files deleted after transcription
- No telemetry or network calls (except HuggingFace downloads)

---

## See Also

- [Installation Guide](INSTALLATION.md) - Setup instructions
- [README](../README.md) - Project overview
- [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) - Backend repository
