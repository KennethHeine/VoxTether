# AGENTS.md

This file provides context and instructions to help AI coding agents work effectively on the VoxTether project.

## Project Overview

VoxTether is a voice dictation application for Windows 10/11. It uses a client-server architecture with an Electron frontend and Python FastAPI backend using faster-whisper for local speech-to-text transcription.

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
│   │   │   ├── health.py        # Health check endpoint
│   │   │   ├── models.py        # Model management endpoints
│   │   │   └── transcribe.py    # Transcription endpoint
│   │   ├── services/            # Business logic
│   │   │   ├── model_manager.py # Model download/management
│   │   │   └── transcriber.py   # faster-whisper integration
│   │   ├── main.py              # FastAPI entry point
│   │   ├── cli.py               # CLI for model management
│   │   ├── config.py            # Configuration settings
│   │   └── requirements.txt     # Python dependencies
│   │
│   └── frontend-electron/       # Electron Frontend
│       ├── src/
│       │   ├── main.js          # Electron main process
│       │   ├── preload.js       # Secure IPC bridge
│       │   └── renderer/        # UI (HTML/CSS/JS)
│       ├── tests/               # Playwright E2E tests
│       └── package.json
│
├── build/                       # Build scripts
├── assets/                      # Application assets (icons)
├── docs/                        # Documentation
├── installer/                   # Installer scripts
├── tests/                       # Backend test scripts
└── requirements-dev.txt         # Development dependencies
```

## Key Components

### Backend (FastAPI)
- `main.py` - FastAPI application entry point
- `cli.py` - CLI tool for model management and server control
- `config.py` - Configuration settings (pydantic-settings)
- `api/health.py` - Health check endpoint
- `api/models.py` - Model management endpoints (list, download, delete, load)
- `api/transcribe.py` - Transcription endpoint
- `services/transcriber.py` - faster-whisper integration
- `services/model_manager.py` - Model download and management

### Frontend (Electron)
- `main.js` - Electron main process
- `preload.js` - Secure IPC bridge
- `renderer/` - UI components (HTML/CSS/JS)
- `tests/` - Playwright E2E tests

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

CI/CD workflows are defined in `.github/workflows/`:
- `ci-backend.yml` - Backend CI (linting, server start test)
- `ci-frontend.yml` - Frontend CI (linting, build, Playwright E2E tests)
- `release-backend.yml` - Backend release workflow
- `release-frontend.yml` - Frontend release workflow
- `copilot-setup-steps.yml` - GitHub Copilot setup

Runs on pull requests and pushes to main branch (path-filtered).

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
