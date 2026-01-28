# AGENTS.md

This file provides context and instructions to help AI coding agents work effectively on the VoxTether project.

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It uses a client-server architecture with an Electron frontend and Python FastAPI backend using faster-whisper for local speech-to-text transcription.

## Setup Commands

```bash
# Backend setup
cd src/backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt

# Install dev dependencies (for testing/linting)
pip install -r ../../requirements-dev.txt

# Run backend server
python -m uvicorn main:app --host 127.0.0.1 --port 5678

# Frontend setup (in a new terminal)
cd src/frontend-electron
npm install
npm start
```

## Architecture

```
VoxTether/
├── src/
│   ├── backend/                 # Python Backend (FastAPI)
│   │   ├── api/                 # REST API endpoints
│   │   ├── services/            # Business logic
│   │   ├── main.py              # FastAPI entry point
│   │   └── requirements.txt     # Python dependencies
│   │
│   ├── frontend-electron/       # Electron Frontend
│   │   ├── src/
│   │   │   ├── main.js          # Electron main process
│   │   │   ├── preload.js       # Secure IPC bridge
│   │   │   └── renderer/        # UI (HTML/CSS/JS)
│   │   └── package.json
│   │
│   └── (legacy Python UI)       # Original Python implementation
│       ├── main.py
│       ├── tray.py
│       └── ...
│
├── tests/                       # Unit tests
├── assets/                      # Application assets (icons)
├── docs/                        # Documentation
├── requirements-dev.txt         # Development dependencies
├── pyproject.toml               # Project configuration
└── build.py                     # PyInstaller build script
```

## Key Components

### Backend (FastAPI)
- `main.py` - FastAPI application entry point
- `api/` - REST API endpoints for transcription, health, models
- `services/` - Business logic (transcriber, model manager)

### Frontend (Electron)
- `main.js` - Electron main process
- `preload.js` - Secure IPC bridge
- `renderer/` - UI components (HTML/CSS/JS)

## Code Style

- Python 3.13+
- Follow PEP 8 style guidelines
- Use type hints where appropriate
- Use ruff for linting

## Testing

- Backend: Run backend server and test with curl or frontend
- Frontend: `cd src/frontend-electron && npm test` (Playwright E2E tests)
- Linting: `ruff check src/backend/`

## CI/CD

- CI workflow is defined in `.github/workflows/ci.yml`
- Runs on pull requests to main branch
- Tests backend (FastAPI server start)
- Builds frontend (Electron)
- Runs E2E tests (Playwright)

## Platform

- Windows only (requires Windows-specific libraries for keyboard hooks)
- Targets Python 3.13+
- Uses faster-whisper for transcription (native CUDA 12 support)

## Building for Release

```bash
# Build both frontend and backend
cd build
.\build.ps1 -Release -Version "2.0.0"
```
