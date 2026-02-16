# AGENTS.md

This file provides context and instructions to help AI coding agents work effectively on the VoxTether project.

## Project Overview

VoxTether is a voice dictation application for Windows 10/11. This repository contains the Electron frontend client. The Python FastAPI backend is maintained separately at https://github.com/KennethHeine/VoxTether-backend.

## Setup Commands

```bash
# Frontend setup
cd src/frontend-electron
npm install
npm start
```

## Architecture

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

> **Backend**: https://github.com/KennethHeine/VoxTether-backend

## Key Components

### Frontend (Electron)
- `main/index.js` - Electron main process entry point
- `preload.js` - Secure IPC bridge
- `renderer/` - UI components (HTML/CSS/JS)
- `tests/` - Playwright E2E tests

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

## Testing

- Frontend: `cd src/frontend-electron && npm test` (Playwright E2E tests)
- Linting: `cd src/frontend-electron && npm run lint`

## CI/CD

CI/CD workflows are defined in `.github/workflows/`:
- `ci-frontend.yml` - Frontend CI (linting, build, Playwright E2E tests)
- `release-frontend.yml` - Frontend release workflow
- `copilot-setup-steps.yml` - GitHub Copilot setup

Runs on pull requests and pushes to main branch (path-filtered).

## Platform

- Windows only (requires Windows-specific libraries for keyboard hooks)

## Building for Release

```bash
cd build
.\build.ps1 -Release -Version "2.0.0"
```
