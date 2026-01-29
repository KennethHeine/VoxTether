# Copilot Instructions for VoxTether

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It uses a client-server architecture with an Electron frontend and Python FastAPI backend using faster-whisper for fully offline speech-to-text.

**Key characteristics:**
- Windows-only desktop application
- Electron frontend + Python FastAPI backend
- Uses faster-whisper for transcription (native CUDA 12 support)
- MIT License

## Build and Test Commands

**Run backend commands from `src/backend/` directory, frontend commands from `src/frontend-electron/`.**

### Backend Setup

```bash
# Navigate to backend
cd src/backend

# Create virtual environment (first time only)
python -m venv venv
.\venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt

# Install dev dependencies (for linting)
pip install -r ../../requirements-dev.txt

# Run backend server
python -m uvicorn main:app --host 127.0.0.1 --port 5678
```

### Frontend Setup

```bash
# Navigate to frontend
cd src/frontend-electron

# Install dependencies
npm install

# Run frontend (development)
npm start

# Run linting
npm run lint

# Run E2E tests
npm test
```

### Running Linting

```bash
# Backend linting
ruff check src/backend/

# Frontend linting
cd src/frontend-electron && npm run lint
```

### Important Notes

- **Windows only**: The application uses Windows-specific features for keyboard hooks and system tray.
- **Linting**: Use ruff for backend, ESLint for frontend.
- **Testing**: Backend server health check, Playwright E2E for frontend.
- **GPU optional**: CUDA 12 support is optional; the app falls back to CPU mode.

### Building for Release

```bash
# Build both frontend and backend
cd build
.\build.ps1 -Release -Version "2.0.0"
```

## Project Architecture

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
| Component | File | Purpose |
|-----------|------|---------|
| **main.py** | `src/backend/main.py` | FastAPI application entry point |
| **cli.py** | `src/backend/cli.py` | CLI tool for model management and server control |
| **config.py** | `src/backend/config.py` | Configuration settings (pydantic-settings) |
| **api/health.py** | `src/backend/api/health.py` | Health check endpoint |
| **api/models.py** | `src/backend/api/models.py` | Model management endpoints (list, download, delete, load) |
| **api/transcribe.py** | `src/backend/api/transcribe.py` | Transcription endpoint |
| **services/transcriber.py** | `src/backend/services/transcriber.py` | faster-whisper integration |
| **services/model_manager.py** | `src/backend/services/model_manager.py` | Model download/management |

### Frontend (Electron)
| Component | File | Purpose |
|-----------|------|---------|
| **main.js** | `src/frontend-electron/src/main.js` | Electron main process |
| **preload.js** | `src/frontend-electron/src/preload.js` | Secure IPC bridge |
| **renderer/** | `src/frontend-electron/src/renderer/` | UI components |
| **tests/** | `src/frontend-electron/tests/` | Playwright E2E tests |

## CI/CD Pipeline

CI/CD workflows are defined in `.github/workflows/`:

### Backend CI (`.github/workflows/ci-backend.yml`)
- Runs on PRs/pushes to `main` that modify `src/backend/**`
- Tests: Linting (ruff), server startup test

### Frontend CI (`.github/workflows/ci-frontend.yml`)
- Runs on PRs/pushes to `main` that modify `src/frontend-electron/**`
- Tests: Linting (ESLint), Electron build, Playwright E2E tests

### Release Workflows
- `release-backend.yml` - Backend release
- `release-frontend.yml` - Frontend release (creates Windows installer and portable ZIP)

## Code Style Guidelines

- **Python**: Follow PEP 8 style guidelines
- **Type hints**: Use type hints where appropriate
- **Linting**: Use ruff for Python, ESLint for JavaScript
- **Formatting**: Use black for Python formatting (optional)

## Configuration Files

| File | Purpose |
|------|---------|
| `src/backend/requirements.txt` | Backend Python dependencies |
| `src/backend/config.py` | Backend configuration settings |
| `requirements-dev.txt` | Development dependencies |
| `src/frontend-electron/package.json` | Frontend dependencies |
| `.github/workflows/ci-backend.yml` | Backend CI pipeline |
| `.github/workflows/ci-frontend.yml` | Frontend CI pipeline |

## Dependency Management

### Backend (Python)
Dependencies are declared in `src/backend/requirements.txt`:
- **fastapi**: Web framework
- **uvicorn**: ASGI server
- **faster-whisper**: Speech-to-text engine
- **pydantic**: Data validation
- **huggingface-hub**: Model downloads

### Frontend (Node.js)
Dependencies are declared in `src/frontend-electron/package.json`:
- **electron**: Desktop framework
- **electron-builder**: Build/packaging

## Testing

- **Backend**: Health check endpoint test (CI tests server startup)
- **Frontend**: Playwright E2E tests (`npm test`)
- **Linting**: `ruff check src/backend/` and `npm run lint`

## Troubleshooting

### Backend won't start
Make sure you're in `src/backend/` and have installed requirements.txt.

### CUDA not available
Install CUDA packages: `pip install nvidia-cublas-cu12 nvidia-cudnn-cu12`

### Frontend can't connect to backend
Ensure backend is running on port 5678.

## Trust These Instructions

These instructions have been validated against the actual repository. If a command or path mentioned here fails, verify the current state of the repository as it may have changed. Only search the codebase if information here appears outdated or incomplete.
