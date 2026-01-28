# Frontend-Backend Communication Guide

This document describes how the VoxTether frontend (Electron) communicates with the backend (Python FastAPI), including all APIs and how audio is sent for transcription.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Communication Flow](#communication-flow)
- [IPC Bridge (Electron)](#ipc-bridge-electron)
- [REST API Endpoints](#rest-api-endpoints)
- [Audio Recording and Transmission](#audio-recording-and-transmission)
- [Complete Workflow Examples](#complete-workflow-examples)

---

## Architecture Overview

VoxTether uses a **client-server architecture** with two main components:

| Component | Technology | Role |
|-----------|------------|------|
| **Frontend (Client)** | Electron 40.x | Desktop app, UI, audio recording, hotkey detection |
| **Backend (Server)** | Python FastAPI | Speech-to-text transcription using faster-whisper |

```
┌─────────────────────────────────────────────────────────────────┐
│                    Electron Frontend                             │
│  ┌──────────────────┐     ┌──────────────────────────────────┐ │
│  │  Renderer Process │ IPC │     Main Process                  │ │
│  │  (UI - HTML/JS)   │◄───►│  • System tray                    │ │
│  │  • Settings UI    │     │  • Hotkey listener                │ │
│  │  • Model browser  │     │  • Audio recording                │ │
│  │  • Status display │     │  • HTTP client for backend        │ │
│  └──────────────────┘     └──────────────┬───────────────────┘ │
└─────────────────────────────────────────┼───────────────────────┘
                                          │
                                          │ HTTP REST API
                                          │ (localhost:5678)
                                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Python Backend (FastAPI)                      │
│  ┌──────────────────┐   ┌──────────────────────────────────┐   │
│  │   REST API        │   │     Services                      │   │
│  │  /api/health      │   │  • TranscriberService             │   │
│  │  /api/transcribe  │──►│  • ModelManager                   │   │
│  │  /api/models      │   │  • faster-whisper                 │   │
│  │  /api/devices     │   │                                   │   │
│  └──────────────────┘   └──────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Communication Flow

### Connection Details

| Setting | Value |
|---------|-------|
| Protocol | HTTP |
| Host | `127.0.0.1` (localhost) |
| Port | `5678` (configurable) |
| Base URL | `http://127.0.0.1:5678/api` |

### Authentication

No authentication is required. The API is designed for local-only communication.

---

## IPC Bridge (Electron)

The Electron app uses a secure IPC (Inter-Process Communication) bridge between its renderer process (UI) and main process.

### Security Model

```
Renderer Process (UI)          Main Process
     │                              │
     │   window.voxtether.xxx()     │
     ├─────────────────────────────►│
     │                              │
     │     (preload.js bridge)      │
     │                              │
     │◄─────────────────────────────┤
     │        Response data         │
```

### Available IPC Methods

The `preload.js` script exposes these methods to the UI via `window.voxtether`:

#### Settings

| Method | Description |
|--------|-------------|
| `getSettings()` | Get current application settings |
| `saveSettings(settings)` | Save application settings |

#### Backend Communication

| Method | Description |
|--------|-------------|
| `backendHealth()` | Check if backend is running |
| `getDevices()` | Get GPU/CPU device information |
| `getModels()` | List available speech models |
| `downloadModel(modelName)` | Download a model (SSE progress) |
| `loadModel(modelName)` | Load a model for transcription |
| `deleteModel(modelName)` | Delete a downloaded model |
| `transcribe(audioPath, language)` | Transcribe an audio file |

#### Utilities

| Method | Description |
|--------|-------------|
| `copyToClipboard(text)` | Copy text to clipboard |
| `openPath(path)` | Open a file/folder in system explorer |
| `openExternal(url)` | Open URL in default browser |
| `getAppInfo()` | Get app version and paths |

#### Events (Main → Renderer)

| Event | Description |
|-------|-------------|
| `onDownloadProgress(callback)` | Model download progress updates |
| `onTestMicrophone(callback)` | Microphone test request from tray |
| `onRecordingStateChanged(callback)` | Recording state changes |
| `onStatusChanged(callback)` | General status updates |

### Example: IPC Call Flow

```javascript
// In renderer.js (UI)
async function checkBackendStatus() {
    const result = await window.voxtether.backendHealth();
    if (result.success) {
        console.log('Backend is running:', result.data);
    } else {
        console.error('Backend offline:', result.error);
    }
}
```

---

## REST API Endpoints

The backend exposes these REST API endpoints:

### Health Endpoints

#### GET /api/health

Check if the backend is running and get system status.

**Request:**
```http
GET /api/health HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "status": "ok",
    "model_loaded": true,
    "device": "cuda"
}
```

---

#### GET /api/devices

Get information about available compute devices (GPU/CPU).

**Request:**
```http
GET /api/devices HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "cuda_available": true,
    "cuda_version": "12.1",
    "device_name": "NVIDIA GeForce RTX 3080"
}
```

---

### Model Endpoints

#### GET /api/models

List all available models and their download status.

**Request:**
```http
GET /api/models HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "models": [
        {
            "name": "small",
            "display_name": "Small",
            "size_mb": 466,
            "downloaded": true,
            "path": "/path/to/models/small",
            "description": "Recommended for most users"
        }
    ],
    "current_model": "small"
}
```

---

#### POST /api/models/{model_name}/download

Download a model with progress updates via Server-Sent Events (SSE).

**Request:**
```http
POST /api/models/small/download HTTP/1.1
Host: 127.0.0.1:5678
```

**Response (SSE Stream):**
```
data: {"status": "downloading", "progress": 25, "downloaded_mb": 116}

data: {"status": "downloading", "progress": 50, "downloaded_mb": 233}

data: {"status": "downloading", "progress": 75, "downloaded_mb": 349}

data: {"status": "complete", "progress": 100}
```

---

#### POST /api/models/{model_name}/load

Load a downloaded model for transcription.

**Request:**
```http
POST /api/models/small/load HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "success": true,
    "model": "small"
}
```

---

#### POST /api/models/{model_name}/unload

Unload the currently loaded model to free memory.

**Request:**
```http
POST /api/models/small/unload HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "success": true
}
```

---

#### DELETE /api/models/{model_name}

Delete a downloaded model from disk.

**Request:**
```http
DELETE /api/models/small HTTP/1.1
Host: 127.0.0.1:5678
```

**Response:**
```json
{
    "success": true
}
```

---

### Transcription Endpoints

#### POST /api/transcribe

Transcribe an audio file. **This is the main endpoint for sending audio.**

**Request:**
```http
POST /api/transcribe HTTP/1.1
Host: 127.0.0.1:5678
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW

------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="file"; filename="recording.wav"
Content-Type: audio/wav

<binary audio data>
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="language"

auto
------WebKitFormBoundary7MA4YWxkTrZu0gW--
```

**Form Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File | Yes | Audio file (WAV, MP3, FLAC, etc.) |
| `language` | String | No | Language code or "auto" (default: "auto") |
| `translate` | Boolean | No | Translate to English (default: false) |

**Response:**
```json
{
    "text": "Hello, this is a test transcription.",
    "language": "en",
    "duration": 3.5,
    "success": true,
    "error": null
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `text` | String | The transcribed text |
| `language` | String | Detected or specified language |
| `duration` | Float | Audio duration in seconds |
| `success` | Boolean | Whether transcription succeeded |
| `error` | String | Error message if failed, null otherwise |

---

### Settings Endpoints

#### POST /api/settings

Update transcription settings (device, compute type, etc.).

**Request:**
```http
POST /api/settings HTTP/1.1
Host: 127.0.0.1:5678
Content-Type: application/json

{
    "device": "cuda",
    "compute_type": "float16",
    "language": "en",
    "model": "small"
}
```

**Response:**
```json
{
    "success": true
}
```

---

## Audio Recording and Transmission

### Audio Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          AUDIO PIPELINE                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. USER HOLDS HOTKEY                                                    │
│     │                                                                    │
│     ▼                                                                    │
│  ┌──────────────────┐                                                   │
│  │  Hotkey Listener  │  (Electron main process detects key press)       │
│  └────────┬─────────┘                                                   │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐                                                   │
│  │  Audio Recorder   │  (Start recording from microphone)               │
│  │  • 16kHz sample   │                                                   │
│  │  • Mono channel   │                                                   │
│  │  • WAV format     │                                                   │
│  └────────┬─────────┘                                                   │
│           │                                                              │
│  2. USER RELEASES HOTKEY                                                 │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐                                                   │
│  │  Save WAV File    │  (Write to temporary file)                       │
│  └────────┬─────────┘                                                   │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐                                                   │
│  │  HTTP POST        │  POST /api/transcribe                            │
│  │  multipart/form   │  • Content-Type: multipart/form-data             │
│  │  ┌─────────────┐  │  • file: <audio.wav>                             │
│  │  │  audio.wav  │  │  • language: "auto"                              │
│  │  └─────────────┘  │                                                   │
│  └────────┬─────────┘                                                   │
│           │                                                              │
│           │ HTTP Request                                                 │
│           ▼                                                              │
│  ┌──────────────────────────────────────┐                               │
│  │        BACKEND (FastAPI)             │                               │
│  │  ┌────────────────────────────────┐  │                               │
│  │  │ 1. Receive multipart upload    │  │                               │
│  │  │ 2. Save to temp file           │  │                               │
│  │  │ 3. Run faster-whisper          │  │                               │
│  │  │ 4. Delete temp file            │  │                               │
│  │  │ 5. Return JSON response        │  │                               │
│  │  └────────────────────────────────┘  │                               │
│  └────────┬─────────────────────────────┘                               │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐                                                   │
│  │  JSON Response    │  { "text": "...", "success": true }              │
│  └────────┬─────────┘                                                   │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐                                                   │
│  │  Text Injection   │  (Copy to clipboard + Ctrl+V)                    │
│  └──────────────────┘                                                   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Audio Format Requirements

| Property | Value |
|----------|-------|
| Sample Rate | 16kHz (recommended) |
| Channels | Mono |
| Format | WAV (also supports MP3, FLAC, etc.) |
| Bit Depth | 16-bit PCM |

### How Audio is Sent (Code Example)

The frontend sends audio to the backend using multipart form data:

```javascript
// From src/frontend-electron/src/main.js - Transcription IPC handler
ipcMain.handle('transcribe', async (event, audioPath, language) => {
    return new Promise((resolve, _reject) => {
        // Create multipart form data boundary
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;
        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);

        // Build multipart body
        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        const bodyEnd = `\r\n--${boundary}\r\n` +
            `Content-Disposition: form-data; name="language"\r\n\r\n${language || 'auto'}\r\n` +
            `--${boundary}--\r\n`;

        // Combine header, audio data, and footer
        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        // Send HTTP request
        const options = {
            hostname: '127.0.0.1',
            port: 5678,
            path: '/api/transcribe',
            method: 'POST',
            headers: {
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': bodyBuffer.length
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                resolve({ success: true, data: JSON.parse(data) });
            });
        });

        req.write(bodyBuffer);
        req.end();
    });
});
```

### Backend Processing

The backend receives and processes audio:

```python
# From src/backend/api/transcribe.py
@router.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe_audio(
    request: Request,
    file: UploadFile = File(..., description="WAV audio file to transcribe"),
    language: str = Form(default="auto", description="Language code or 'auto'"),
    translate: bool = Form(default=False, description="Translate to English"),
):
    # 1. Save uploaded file to temp location
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
        temp_path = temp_file.name
        content = await file.read()
        temp_file.write(content)
    
    # 2. Transcribe using faster-whisper
    result = await transcriber.transcribe(
        audio_path=temp_path,
        language=language,
        task="translate" if translate else "transcribe",
    )
    
    # 3. Clean up temp file
    os.unlink(temp_path)
    
    # 4. Return result
    return TranscriptionResponse(
        text=result.text,
        language=result.language,
        duration=result.duration_seconds,
        success=result.success,
        error=result.error,
    )
```

---

## Complete Workflow Examples

### Example 1: Full Transcription Workflow

```
User                    Frontend                  Backend
  │                        │                         │
  │  Press & hold hotkey   │                         │
  │───────────────────────►│                         │
  │                        │                         │
  │                        │  Start recording        │
  │                        │  (microphone → WAV)     │
  │                        │                         │
  │  Release hotkey        │                         │
  │───────────────────────►│                         │
  │                        │                         │
  │                        │  POST /api/transcribe   │
  │                        │  (multipart: audio.wav) │
  │                        │────────────────────────►│
  │                        │                         │
  │                        │                         │ Run faster-whisper
  │                        │                         │ (GPU/CPU)
  │                        │                         │
  │                        │  JSON response          │
  │                        │◄────────────────────────│
  │                        │  {"text": "Hello..."}   │
  │                        │                         │
  │                        │  Inject text            │
  │                        │  (clipboard + Ctrl+V)   │
  │                        │                         │
  │  Text appears at cursor│                         │
  │◄───────────────────────│                         │
  │                        │                         │
```

### Example 2: Model Management Workflow

```
User                    Frontend                  Backend
  │                        │                         │
  │  Open Settings UI      │                         │
  │───────────────────────►│                         │
  │                        │                         │
  │                        │  GET /api/models        │
  │                        │────────────────────────►│
  │                        │                         │
  │                        │  Model list response    │
  │                        │◄────────────────────────│
  │                        │                         │
  │  Click "Download"      │                         │
  │───────────────────────►│                         │
  │                        │                         │
  │                        │  POST /models/X/download│
  │                        │────────────────────────►│
  │                        │                         │
  │                        │  SSE: progress 25%      │
  │  See progress bar      │◄────────────────────────│
  │                        │  SSE: progress 50%      │
  │                        │◄────────────────────────│
  │                        │  SSE: progress 100%     │
  │                        │◄────────────────────────│
  │                        │                         │
  │  Click "Load Model"    │                         │
  │───────────────────────►│                         │
  │                        │                         │
  │                        │  POST /models/X/load    │
  │                        │────────────────────────►│
  │                        │                         │
  │                        │  {"success": true}      │
  │                        │◄────────────────────────│
  │                        │                         │
  │  Model now active      │                         │
  │◄───────────────────────│                         │
```

### Example 3: Error Handling

```javascript
// Frontend error handling example
async function transcribeAudio(audioPath) {
    try {
        const result = await window.voxtether.transcribe(audioPath, 'auto');
        
        if (result.success && result.data.success) {
            // Transcription successful
            return result.data.text;
        } else if (result.success && !result.data.success) {
            // Backend returned an error
            console.error('Transcription failed:', result.data.error);
            showNotification(result.data.error, 'error');
        } else {
            // HTTP request failed
            console.error('Request failed:', result.error);
            showNotification('Backend connection failed', 'error');
        }
    } catch (error) {
        // Unexpected error
        console.error('Unexpected error:', error);
        showNotification('An unexpected error occurred', 'error');
    }
}
```

---

## Error Response Format

All error responses follow this format:

```json
{
    "detail": "Error message describing what went wrong"
}
```

**HTTP Status Codes:**

| Code | Meaning |
|------|---------|
| `200` | Success |
| `400` | Bad Request (invalid parameters) |
| `404` | Not Found (model doesn't exist) |
| `500` | Internal Server Error |
| `503` | Service Unavailable (model not loaded) |

---

## See Also

- [Frontend Features](FRONTEND-FEATURES.md) - Complete frontend feature documentation
- [Architecture Documentation](ARCHITECTURE.md) - System architecture overview
- [Backend API Documentation](BACKEND-API.md) - Detailed API reference
- [Installation Guide](INSTALLATION.md) - Setup instructions
