# Running VoxTether Locally (Development)

This guide explains how to run VoxTether from source for development purposes.

## Architecture Overview

VoxTether uses a client-server architecture:
- **Client (Frontend)**: Electron desktop application (this repo)
- **Server (Backend)**: Python FastAPI service ([VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend))

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| Node.js | 20.x+ | Frontend client |
| npm | 10.x+ | Package management |
| Git | Latest | Source control |

**Check installations:**
```powershell
node --version     # Should be 20.x+
npm --version      # Should be 10.x+
git --version
```

### Hardware Requirements

**Client (Frontend):**
| Requirement | Minimum |
|------------|---------|
| OS | Windows 10 (64-bit) |
| RAM | 2 GB |

---

## Step 1: Get the Source Code

```powershell
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether
```

---

## Step 2: Set Up the Backend Server

The backend is in a separate repository. See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for setup instructions.

---

## Step 3: Set Up the Frontend

```powershell
# Navigate to frontend
cd src/frontend-electron

# Install dependencies
npm install
```

---

## Step 4: Run the Frontend

```powershell
# Run the Electron application
npm start

# Or with debug mode (opens DevTools)
npm run dev
```

VoxTether will start and appear in your system tray.

---

## Using VoxTether

### Default Hotkey

**Ctrl + Shift + Space**

Press and hold the hotkey to record. Release to transcribe and insert text.

### System Tray Menu

Right-click the VoxTether tray icon to access:

| Menu Item | Description |
|-----------|-------------|
| Settings... | Open settings window |
| Test Microphone | Record and transcribe a 2-second test |
| Open Models Folder | Access downloaded models |
| Open Logs Folder | Access log files |
| About | Show version and system info |
| Exit | Close VoxTether |

---

## Development Workflow

### Running Tests

```powershell
# Frontend E2E tests
cd src/frontend-electron
npm test

# Frontend linting
npm run lint
```

---

## Building for Release

```powershell
cd build
.\build.ps1 -Release -Version "2.0.0"

# Build with installer
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

---

## Project Structure

```
VoxTether/
├── src/
│   └── frontend-electron/ # Electron frontend
│       ├── src/
│       │   ├── main/      # Main process
│       │   ├── preload.js # IPC bridge
│       │   └── renderer/  # UI files
│       └── package.json
├── build/                 # Build scripts
├── installer/             # Inno Setup script
└── docs/                  # Documentation
```

> **Backend**: See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend)

---

## Troubleshooting

### Frontend can't connect to backend

1. Ensure the [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) is running
2. Check the backend URL in Settings

### "node: command not found"

Install Node.js 20.x from [nodejs.org](https://nodejs.org/).

### Hotkey not working

1. Another app may be using the same hotkey
2. Try running as Administrator
3. Check Windows Focus Assist settings

### No audio recording

1. Check Windows Sound settings → Recording
2. Ensure microphone is set as default device
3. Use "Test Microphone" from tray menu

---

## See Also

- [README](../README.md) - Project overview
- [Installation Guide](INSTALLATION.md) - End-user installation
- [Architecture](ARCHITECTURE.md) - Technical architecture
- [Changelog](CHANGELOG.md) - Version history
- [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) - Backend repository
