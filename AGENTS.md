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

# Linting
cd src/frontend-electron
npm run lint

# E2E tests (requires xvfb on Linux CI)
cd src/frontend-electron
npm test
```

## Architecture

```
VoxTether/
├── src/
│   └── frontend-electron/           # Electron Frontend
│       ├── src/
│       │   ├── main/                 # Electron main process (Node.js, CommonJS)
│       │   │   ├── index.js          # App lifecycle & orchestration
│       │   │   ├── ipc-handlers.js   # All IPC handler registrations
│       │   │   ├── backend-client.js # HTTP client for backend API
│       │   │   ├── recording.js      # Recording state machine
│       │   │   ├── settings-manager.js # Settings persistence
│       │   │   ├── hotkeys.js        # Global hotkey registration
│       │   │   ├── overlay.js        # Recording indicator overlay
│       │   │   ├── transcription-provider.js # Multi-provider transcription
│       │   │   ├── tray.js           # System tray icon & menu
│       │   │   ├── updater.js        # Auto-update logic
│       │   │   └── window.js         # Main window management
│       │   ├── shared/
│       │   │   └── constants.js      # ⚠️ Single source of truth for all IPC
│       │   │                         #    channel names, events, and config
│       │   ├── preload.js            # Secure IPC bridge (uses constants.js)
│       │   ├── overlay/              # Recording overlay UI
│       │   └── renderer/             # UI (ES Modules)
│       │       ├── renderer.js       # Entry point (imports modules/index.js)
│       │       ├── index.html        # Main HTML
│       │       ├── styles.css        # Global styles
│       │       └── modules/          # Feature modules (ES Modules)
│       │           ├── index.js      # Module orchestrator & health monitoring
│       │           ├── state.js      # Centralized state management
│       │           ├── settings.js   # Settings UI logic
│       │           ├── recording/    # Recording subsystem
│       │           │   ├── index.js
│       │           │   ├── audio-processing.js
│       │           │   ├── media-recorder.js
│       │           │   ├── preview.js
│       │           │   └── transcription.js
│       │           ├── models.js     # Model management UI
│       │           ├── transcribe.js # File transcription UI
│       │           ├── audio.js      # Audio device management
│       │           ├── audio-constants.js # Audio processing constants
│       │           ├── mictest.js    # Microphone test UI
│       │           ├── history.js    # Transcription history
│       │           ├── statistics.js # Usage statistics
│       │           ├── hotkey.js     # Hotkey capture UI
│       │           ├── navigation.js # Page navigation
│       │           ├── notifications.js # Toast notifications
│       │           ├── status.js     # Status indicator
│       │           ├── theme.js      # Theme management
│       │           ├── updater.js    # Auto-update UI
│       │           ├── about.js      # About page
│       │           └── utils.js      # Shared utilities
│       ├── tests/                    # Playwright E2E tests
│       ├── eslint.config.js          # ESLint configuration
│       └── package.json
│
├── build/                            # Build scripts (PowerShell)
├── assets/                           # Application assets (icons)
├── docs/                             # Documentation
└── installer/                        # Installer scripts
```

> **Backend**: https://github.com/KennethHeine/VoxTether-backend

## Key Conventions

### IPC Channel Management

**All IPC channel names are defined in `src/shared/constants.js`** and used consistently across three files:

1. **`shared/constants.js`** — Defines all `IPC_*` and `EVENT_*` constants
2. **`preload.js`** — Imports and uses constants for the renderer ↔ main bridge
3. **`main/ipc-handlers.js`** — Imports and uses constants for handler registration

When adding a new IPC channel:
1. Add the constant to `shared/constants.js`
2. Add the handler in `main/ipc-handlers.js`
3. Add the bridge method in `preload.js`
4. The renderer calls via `window.voxtether.<methodName>()`

### Module System

- **Main process** (`src/main/`, `src/preload.js`): CommonJS (`require`/`module.exports`)
- **Renderer** (`src/renderer/`): ES Modules (`import`/`export`)
- **Shared** (`src/shared/constants.js`): ES Module syntax (`export`), but compatible with CommonJS `require()` via Node.js ESM interop

### State Management

Renderer state is centralized in `renderer/modules/state.js`:
- Provides getter/setter functions for all UI state
- Supports `subscribe(key, callback)` for reactive state changes
- Recording and mic test state return direct references (live objects)
- History and statistics state return copies

### Transcription Providers

Three transcription backends are supported, routed by `main/transcription-provider.js`:
- **local**: faster-whisper via the Python backend
- **openai**: OpenAI Whisper API (cloud)
- **azure**: Azure Speech Services (cloud)

The provider is configured via `settings.transcriptionProvider`.

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

- Frontend E2E: `cd src/frontend-electron && npm test` (Playwright, 44 tests)
- Linting: `cd src/frontend-electron && npm run lint` (ESLint 10)

On Linux CI, tests require xvfb: `xvfb-run --auto-servernum npm test`

## CI/CD

CI/CD workflows are defined in `.github/workflows/`:
- `ci-frontend.yml` — Frontend CI (linting, build, Playwright E2E tests)
- `release-frontend.yml` — Frontend release (Windows installer + portable ZIP)
- `copilot-setup-steps.yml` — GitHub Copilot setup
- `dependabot-auto-merge.yml` — Auto-merge Dependabot PRs

Runs on pull requests and pushes to main branch (path-filtered to `src/frontend-electron/**`).

## Platform

- Windows only (requires Windows-specific libraries for keyboard hooks)

## Building for Release

```bash
cd build
.\build.ps1 -Release -Version "2.0.0"
```
