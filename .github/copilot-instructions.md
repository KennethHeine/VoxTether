# Copilot Instructions for VoxTether

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. This repository contains the Electron frontend client. The Python FastAPI backend is maintained separately at https://github.com/KennethHeine/VoxTether-backend.

**Key characteristics:**
- Windows-only desktop application
- Electron frontend (this repo)
- Backend is in a separate repository
- MIT License

## Build and Test Commands

**Run frontend commands from `src/frontend-electron/`.**

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

### Building for Release

```bash
cd build
.\build.ps1 -Release -Version "2.0.0"
```

## Project Architecture

```
VoxTether/
├── src/
│   └── frontend-electron/       # Electron Frontend
│       ├── src/
│       │   ├── main/             # Electron main process
│       │   ├── preload.js        # Secure IPC bridge
│       │   └── renderer/         # UI (HTML/CSS/JS)
│       ├── tests/               # Playwright E2E tests
│       └── package.json
│
├── build/                       # Build scripts
├── assets/                      # Application assets (icons)
├── docs/                        # Documentation
└── installer/                   # Installer scripts
```

> **Backend**: The Python FastAPI backend is at https://github.com/KennethHeine/VoxTether-backend

## Key Components

### Frontend (Electron)
| Component | File | Purpose |
|-----------|------|---------|
| **main/index.js** | `src/frontend-electron/src/main/index.js` | Electron main process entry point |
| **preload.js** | `src/frontend-electron/src/preload.js` | Secure IPC bridge |
| **renderer/** | `src/frontend-electron/src/renderer/` | UI components |
| **tests/** | `src/frontend-electron/tests/` | Playwright E2E tests |

## CI/CD Pipeline

CI/CD workflows are defined in `.github/workflows/`:

### Frontend CI (`.github/workflows/ci-frontend.yml`)
- Runs on PRs/pushes to `main` that modify `src/frontend-electron/**`
- Tests: Linting (ESLint), Electron build, Playwright E2E tests

### Release Workflow
- `release-frontend.yml` - Frontend release (creates Windows installer and portable ZIP)

## Configuration Files

| File | Purpose |
|------|---------|
| `src/frontend-electron/package.json` | Frontend dependencies |
| `.github/workflows/ci-frontend.yml` | Frontend CI pipeline |

## Dependency Management

### Frontend (Node.js)
Dependencies are declared in `src/frontend-electron/package.json`:
- **electron**: Desktop framework
- **electron-builder**: Build/packaging

## Testing

- **Frontend**: Playwright E2E tests (`npm test`)
- **Linting**: `cd src/frontend-electron && npm run lint`

## Troubleshooting

### Frontend can't connect to backend
Ensure the backend is running (see https://github.com/KennethHeine/VoxTether-backend).

## Trust These Instructions

These instructions have been validated against the actual repository. If a command or path mentioned here fails, verify the current state of the repository as it may have changed. Only search the codebase if information here appears outdated or incomplete.
