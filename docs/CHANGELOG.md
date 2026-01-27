# Changelog

All notable changes to VoxTether will be documented in this file.

## [2.0.0] - 2026-01-27

### 🚀 Major Changes

#### Complete Client-Server Architecture
The application is now fully split into separate client and server components:

- **Client (Electron)**: Desktop application for Windows with UI and system tray
- **Server (Python FastAPI)**: Backend transcription service that runs on localhost or a network server

**Benefits:**
- **Flexible Deployment**: Run the server on a powerful machine with GPU, clients on any Windows PC
- **No Bundled Executables**: Backend runs as pure Python - no PyInstaller needed
- **Easier Updates**: Update client and server independently
- **Network Support**: Multiple clients can share one backend server

#### Frontend Rewrite: Electron.js
The entire frontend has been rewritten from WinUI 3 (.NET) to Electron.js for a more modern, maintainable, and potentially cross-platform experience.

**Why Electron?**
- **Modern Web Technologies**: Uses HTML, CSS, and JavaScript for the UI
- **Easier to Maintain**: Single codebase, faster development cycle
- **Better Tooling**: npm ecosystem, hot reload during development
- **Future-Ready**: Potential for cross-platform support (macOS, Linux)

### ✨ New Features

#### Modern Settings UI
- Clean, Windows 11 Fluent-inspired design
- Dark and light theme support (follows system preference)
- Responsive layout with sidebar navigation
- Four settings pages: General, Audio, Models, About
- Backend server connection status indicator

#### General Settings
- **Push-to-Talk Hotkey**: Customizable key combination capture
- **Language Selection**: Choose from 11+ languages or auto-detect
- **Output Mode**: Clipboard only, Clipboard + Paste, or Simulate Typing
- **Notifications**: Toggle transcription notifications
- **Recording Indicator**: Visual feedback while recording
- **Start with Windows**: Auto-launch option
- **Start Minimized**: Launch directly to system tray
- **Theme**: System, Light, or Dark mode
- **Backend Host/Port**: Configure server connection

#### Audio Settings
- **Input Device Selection**: Choose your microphone
- **Clipboard Delay**: Fine-tune paste timing (0-1000ms)
- **Microphone Test**: Quick 2-second recording test

#### Model Management
- **Visual Model Cards**: See model details at a glance
- **GPU/CPU Detection**: Automatic hardware detection display
- **Download Progress**: Real-time download progress with speed
- **Model Loading**: Load models with one click
- **Model Deletion**: Remove unwanted models

#### System Tray
- Status indicator (Ready/Recording)
- Quick access to Settings
- Test Microphone option
- Open Models/Logs folders
- About dialog
- Exit option

### 🔧 Technical Improvements

#### Electron 40.x
- Latest stable Electron version (January 2026)
- Chromium-based rendering for consistent UI
- Node.js integration for system-level features

#### Secure Architecture
- Context isolation enabled
- Preload script for secure IPC communication
- No direct Node.js access from renderer
- XSS-safe DOM manipulation (no innerHTML with user data)

#### Build System
- electron-builder for packaging
- NSIS installer for Windows
- Portable build option
- Updated CI/CD workflow

### 🗑️ Removed

- **WinUI 3 Frontend**: Removed entirely in favor of Electron
- **.NET 8.0 Dependency**: No longer required for the frontend
- **NAudio**: Audio recording now handled differently
- **H.NotifyIcon**: System tray now via Electron native APIs

### 📦 Dependencies

#### Frontend (New)
| Package | Version | Purpose |
|---------|---------|---------|
| electron | 40.0.0 | Desktop framework |
| electron-builder | 26.5.0 | Build and packaging |
| eslint | 9.18.0 | Code linting |

#### Backend (Unchanged)
| Package | Version | Purpose |
|---------|---------|---------|
| FastAPI | 0.109+ | REST API framework |
| faster-whisper | 1.0+ | Speech-to-text |
| uvicorn | 0.27+ | ASGI server |

### 🔄 Migration Guide

If you're upgrading from v1.x (WinUI 3):

1. **Settings**: Your settings are stored in `%APPDATA%\VoxTether\settings.json` and will be preserved
2. **Models**: Downloaded models remain in `%APPDATA%\VoxTether\models\`
3. **Uninstall**: You can uninstall the old .NET version after installing the new Electron version

### 📋 System Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| OS | Windows 10 64-bit | Windows 11 |
| RAM | 4 GB | 8 GB+ |
| GPU | - | NVIDIA with CUDA 12 |
| Storage | 500 MB | 5 GB (for large models) |

### 🐛 Known Issues

- Global hotkey detection may require running as Administrator for some applications
- First model download may take several minutes depending on connection speed

---

## [1.0.0] - Previous Version

Initial release with WinUI 3 (.NET 8.0) frontend.

---

## Version History

| Version | Date | Frontend | Notes |
|---------|------|----------|-------|
| 2.0.0 | 2026-01-27 | Electron 40.x | Complete frontend rewrite |
| 1.0.0 | - | WinUI 3 (.NET 8.0) | Initial release |
