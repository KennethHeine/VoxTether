# VoxTether Frontend Features

This document provides a comprehensive overview of all features available in the VoxTether Electron frontend application.

## Overview

VoxTether is a voice dictation application for Windows that provides fully offline speech-to-text transcription. The Electron frontend communicates with a Python FastAPI backend running faster-whisper for transcription.

---

## Core Features

### 1. Recording

The primary feature of VoxTether - use a customizable hotkey to toggle recording, then transcribe your speech.

| Feature | Description |
|---------|-------------|
| **Global Hotkey** | Works system-wide, even when VoxTether is not focused |
| **Default Hotkey** | `Ctrl+Shift+R` (fully customizable) |
| **Real-time Status** | Visual indicator shows recording state |
| **Auto-transcription** | Automatically sends audio to backend when recording stops |
| **Test Recording** | Test recording directly from the settings UI without hotkeys |
| **Recording Level Meter** | Real-time audio level visualization during recording |

**How it works:**
1. Press the configured hotkey to start recording
2. Speak into your microphone
3. Press the hotkey again to stop recording
4. Audio is sent to the backend for transcription
5. Transcribed text is automatically pasted at cursor position (or shown in preview dialog if enabled)

### 2. Recording Indicator Overlay

A visual overlay bar appears at the top of the screen during recording and transcription.

| State | Description |
|-------|-------------|
| **Recording** | Red pulsing bar indicates active recording |
| **Transcribing** | Blue animated gradient indicates processing |
| **Hidden** | Overlay is hidden when idle |

The overlay can be enabled/disabled in General Settings.

### 3. Transcription Preview (Optional)

An optional preview dialog that appears after transcription, allowing you to edit the text before inserting.

| Feature | Description |
|---------|-------------|
| **Edit Before Insert** | Modify transcription before pasting |
| **Copy Only** | Copy to clipboard without pasting |
| **Insert** | Paste the edited text at cursor position |
| **Cancel** | Discard the transcription |

Enable "Show Transcription Preview" in General Settings to use this feature.

### 4. System Tray Integration

VoxTether runs quietly in the system tray for quick access.

| Feature | Description |
|---------|-------------|
| **Tray Icon** | Shows current status (Ready, Recording) |
| **Context Menu** | Right-click for quick actions |
| **Double-click** | Opens the Settings window |
| **Minimize to Tray** | Closing the window minimizes to tray instead of quitting |

**Tray Menu Options:**
- Status indicator (Ready/Recording)
- Settings... - Open settings window
- Test Microphone - Quick microphone test
- Open Models Folder - View downloaded models
- Open Logs - View application logs
- About VoxTether - Version information
- Exit - Close the application completely

### 5. Text Output Modes

Multiple ways to output transcribed text:

| Mode | Description |
|------|-------------|
| **Clipboard + Paste** | Copies to clipboard and simulates Ctrl+V (Recommended) |
| **Clipboard Only** | Copies to clipboard without pasting |
| **Simulate Typing** | Types out the text character by character |

### 6. Transcription History

Track and manage your transcription history.

| Feature | Description |
|---------|-------------|
| **History List** | View all past transcriptions with timestamps |
| **Search** | Filter history by text content |
| **Copy** | Copy any past transcription to clipboard |
| **Delete** | Remove individual history items |
| **Expand/Collapse** | Click items to see full transcription text |
| **Export** | Export entire history to a text file |
| **Clear All** | Delete all history items |

History is stored locally in the browser's localStorage.

### 7. Usage Statistics

Track your transcription usage over time.

| Statistic | Description |
|-----------|-------------|
| **Total Recordings** | Number of recordings made |
| **Total Duration** | Cumulative recording time |
| **Characters Transcribed** | Total characters transcribed |

Statistics can be reset from the About page.

### 8. Auto-Updater

Automatic update checking and installation.

| Feature | Description |
|---------|-------------|
| **Check for Updates** | Manual check from About page |
| **Update Notification** | Notification when new version is available |
| **Download Update** | Download update in background |
| **Progress Tracking** | View download progress percentage |
| **Restart & Install** | Install update and restart application |

---

## Settings Pages

### General Settings

Configure core application behavior:

| Setting | Description | Default |
|---------|-------------|---------|
| Window Toggle Hotkey | Key combination to show/hide settings window | `Ctrl+Shift+V` |
| Toggle Recording Hotkey | Key combination to start/stop recording | `Ctrl+Shift+R` |
| Test Recording | Button to test recording without hotkeys | - |
| Language | Language for speech recognition | Auto Detect |
| Output Mode | How transcribed text is inserted | Clipboard + Paste |
| Show Notifications | Display notifications after transcription | Enabled |
| Show Recording Indicator | Visual overlay indicator while recording | Enabled |
| Show Transcription Preview | Show edit dialog before inserting text | Disabled |
| Start with Windows | Launch VoxTether on Windows startup | Disabled |
| Start Minimized | Start in system tray | Enabled |
| Theme | Application color theme (System/Light/Dark) | System |

**Recording Output Options:**
| Setting | Description |
|---------|-------------|
| Recording Output Folder | Folder to save recordings and transcripts |
| Save Audio File | Save the WAV recording to output folder |
| Save Transcript File | Save the transcript TXT to output folder |

### Audio Settings

Configure audio input and behavior:

| Setting | Description | Default |
|---------|-------------|---------|
| Input Device | Select microphone device | Default Device |
| Clipboard Delay | Delay before pasting (milliseconds) | 50ms |

**Microphone Test Feature:**
- Real-time volume meter with peak indicator
- Live waveform visualization
- Device selection dropdown
- Tests without requiring backend connection

### Models Page

Manage speech recognition models:

| Feature | Description |
|---------|-------------|
| Active Model | Currently loaded model for transcription |
| Model List | View all downloaded models |
| Load Model | Switch between downloaded models |
| Device Info | Shows GPU/CPU detection status |

**Available Models:**
| Model | Size | Description |
|-------|------|-------------|
| Tiny | ~75 MB | Quick notes, low-resource systems |
| Base | ~142 MB | General use |
| Small | ~466 MB | Recommended for most users |
| Medium | ~1.5 GB | When accuracy is important |
| Large v3 | ~3 GB | When accuracy is critical |
| Large v3 Turbo | ~1.6 GB | Best balance of speed and accuracy |
| Distil Large v3 | ~1.1 GB | Fast high-quality transcription |

### History Page

View and manage past transcriptions:

| Feature | Description |
|---------|-------------|
| History List | Chronological list of all transcriptions |
| Search | Filter transcriptions by text content |
| Copy | Copy individual transcriptions to clipboard |
| Delete | Remove individual items from history |
| Export | Save all history to a text file |
| Clear All | Remove all history items |

### About Page

Application information and utilities:

| Info | Description |
|------|-------------|
| Version | Current application version |
| Platform | Windows/macOS/Linux |
| Electron Version | Electron framework version |
| Data Path | User data directory (clickable) |
| Models Path | Downloaded models directory (clickable) |
| Links | GitHub, Documentation, Releases |

**Usage Statistics:**
| Statistic | Description |
|-----------|-------------|
| Total Recordings | Number of recordings made |
| Total Duration | Cumulative recording time |
| Characters Transcribed | Total characters transcribed |

**Update Section:**
| Feature | Description |
|---------|-------------|
| Check for Updates | Manual update check button |
| Update Status | Shows when update is available |
| Download/Install | Download and install updates |

### Transcribe Page

Transcribe audio files (not just live recordings):

| Feature | Description |
|---------|-------------|
| File Selection | Browse and select audio files |
| Language Selection | Choose transcription language |
| Output Folder | Where to save transcripts |
| Save Options | Save transcript and/or copy audio |
| Result Display | View transcription with copy/save options |

**Supported Audio Formats:**
- WAV, MP3, M4A, FLAC, OGG, WMA, AAC, WebM

---

## User Interface

### Design System

VoxTether uses a Windows 11 Fluent Design-inspired interface:

| Element | Description |
|---------|-------------|
| **Navigation** | Sidebar with icon and text labels |
| **Cards** | Rounded corners, subtle shadows |
| **Colors** | Blue accent (#0078d4), neutral grays |
| **Typography** | Segoe UI Variable font family |
| **Animations** | Smooth transitions (0.15s-0.2s) |

### Theme Support

| Theme | Description |
|-------|-------------|
| **System** | Follows Windows dark/light mode preference |
| **Light** | Light gray backgrounds, dark text |
| **Dark** | Dark backgrounds, light text |

### Responsive Design

- Sidebar collapses to icons on narrow windows (<700px)
- Content area adapts to available width
- Models displayed in responsive grid

---

## Technical Features

### Audio Processing

| Feature | Description |
|---------|-------------|
| Recording Format | WebM/Opus (browser-native) |
| Conversion | Automatic conversion to 16kHz mono WAV |
| Processing | Client-side audio processing via Web Audio API |

### Security

| Feature | Description |
|---------|-------------|
| Context Isolation | Renderer process isolated from Node.js |
| Node Integration | Disabled for security |
| IPC Bridge | Secure preload script exposes limited API |
| CSP | Content Security Policy prevents XSS |
| Local Only | Backend binds to localhost only |

### Performance

| Feature | Description |
|---------|-------------|
| Single Instance | Only one app instance allowed |
| Lazy Loading | Pages load on-demand |
| Cached Elements | DOM elements cached for animation performance |
| Efficient Updates | Uses DOM manipulation instead of innerHTML |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+R` | Start/stop recording (configurable) |
| `Ctrl+Shift+V` | Show/hide settings window (configurable) |
| Double-click tray | Open settings window |

---

## Data Storage

All data is stored locally in the user's AppData folder:

| Location | Contents |
|----------|----------|
| `%APPDATA%\VoxTether\settings.json` | User preferences |
| `%APPDATA%\VoxTether\models\` | Downloaded speech models |
| `%APPDATA%\VoxTether\logs\` | Application logs |
| `%APPDATA%\VoxTether\temp\` | Temporary recordings |

**Browser LocalStorage:**
| Key | Contents |
|-----|----------|
| `voxtether_history` | Transcription history |
| `voxtether_stats` | Usage statistics |

---

## Notifications

VoxTether provides toast notifications for user feedback:

| Type | Description |
|------|-------------|
| **Success** | Green notification for successful operations |
| **Error** | Red notification for errors |
| **Info** | Blue notification for information |

Notifications appear briefly and can be disabled in General Settings.

---

## Backend Communication

The frontend communicates with the Python backend via HTTP REST API:

| Endpoint | Purpose |
|----------|---------|
| `GET /api/health` | Check backend status |
| `GET /api/devices` | Get GPU/CPU info |
| `GET /api/models` | List available models |
| `POST /api/models/{name}/load` | Load a model |
| `POST /api/transcribe` | Transcribe audio file |

---

## Accessibility

| Feature | Description |
|---------|-------------|
| ARIA Labels | Screen reader support for controls |
| Role Attributes | Proper semantic roles for UI elements |
| Keyboard Navigation | Full keyboard accessibility |
| High Contrast | Works with Windows high contrast modes |
| Focus Indicators | Visible focus states for all interactive elements |

---

## See Also

- [Frontend Installation](FRONTEND-INSTALLATION.md) - Setup guide
- [Frontend Testing](FRONTEND-TESTING.md) - Testing documentation
- [Frontend-Backend Communication](FRONTEND-BACKEND-COMMUNICATION.md) - API details
- [Architecture](ARCHITECTURE.md) - System architecture
