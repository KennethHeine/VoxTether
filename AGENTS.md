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

## Dependency Management

### Frontend (npm)

**Important**: Always keep `package-lock.json` in sync with `package.json` to prevent CI/CD failures.

When updating frontend dependencies in `src/frontend-electron/`:

1. **Never modify `package-lock.json` manually**
2. **Always use npm commands to update dependencies**:
   ```bash
   cd src/frontend-electron
   
   # Install new dependencies
   npm install <package-name>
   
   # Update existing dependencies
   npm update
   
   # Update specific package
   npm install <package-name>@latest
   ```

3. **After updating `package.json`**, regenerate the lock file:
   ```bash
   npm install
   ```

4. **Always commit both files together**:
   ```bash
   git add package.json package-lock.json
   git commit -m "Update frontend dependencies"
   ```

**CI/CD Note**: The workflows use `npm ci` which requires exact sync between `package.json` and `package-lock.json`. If these files are out of sync, the build will fail.

### Backend (pip)

When updating Python dependencies in `src/backend/`:

1. Update `requirements.txt` with the new dependency
2. Install in your virtual environment:
   ```bash
   cd src/backend
   .\venv\Scripts\Activate.ps1  # Windows
   pip install -r requirements.txt
   ```

3. For development dependencies, update `requirements-dev.txt` in the project root

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
