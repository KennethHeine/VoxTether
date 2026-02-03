# OpenAI Transcription API Integration Plan

This document provides a comprehensive plan for integrating OpenAI's Transcription API as an alternative transcription provider in VoxTether, allowing users to switch between the local backend (faster-whisper) and OpenAI's cloud-based API.

## Table of Contents

- [Overview](#overview)
- [OpenAI Whisper API Reference](#openai-whisper-api-reference)
- [Architecture Comparison](#architecture-comparison)
- [Implementation Plan](#implementation-plan)
  - [Phase 1: Frontend Changes](#phase-1-frontend-changes)
  - [Phase 2: Backend Changes (Optional)](#phase-2-backend-changes-optional)
  - [Phase 3: Settings and Configuration](#phase-3-settings-and-configuration)
- [Detailed Implementation Guide](#detailed-implementation-guide)
- [Security Considerations](#security-considerations)
- [Testing Strategy](#testing-strategy)
- [Migration Path](#migration-path)

---

## Overview

### Current Architecture

VoxTether currently uses a **local backend** architecture:

```
┌────────────────────┐     HTTP POST      ┌────────────────────┐
│   Electron App     │    /api/transcribe │   Python Backend   │
│   (Frontend)       │ ─────────────────► │   (FastAPI)        │
│                    │                    │   faster-whisper   │
└────────────────────┘                    └────────────────────┘
```

### Proposed Architecture (Dual Provider)

The enhanced architecture supports **both local and cloud transcription**:

```
┌────────────────────────────────────────────────────────────────────┐
│                        Electron App (Frontend)                      │
│                                                                     │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │              Transcription Provider Selector                 │  │
│   │                                                              │  │
│   │    ┌─────────────────┐          ┌─────────────────────┐     │  │
│   │    │ Local Backend   │    OR    │   OpenAI API        │     │  │
│   │    │ (faster-whisper)│          │   (Cloud)           │     │  │
│   │    └────────┬────────┘          └──────────┬──────────┘     │  │
│   └─────────────┼───────────────────────────────┼────────────────┘  │
│                 │                               │                   │
└─────────────────┼───────────────────────────────┼───────────────────┘
                  │                               │
                  ▼                               ▼
      ┌───────────────────┐          ┌─────────────────────────┐
      │  Local Python     │          │  OpenAI API             │
      │  Backend          │          │  api.openai.com         │
      │  (localhost:5678) │          │  /v1/audio/transcriptions
      └───────────────────┘          └─────────────────────────┘
```

---

## OpenAI Whisper API Reference

### Endpoint

```
POST https://api.openai.com/v1/audio/transcriptions
```

### Available Models

| Model | Description | Best For |
|-------|-------------|----------|
| `whisper-1` | Original Whisper model snapshot | General transcription |
| `gpt-4o-transcribe` | Higher quality transcription | High accuracy needs |
| `gpt-4o-mini-transcribe` | Faster, lightweight | Quick transcriptions |

### Request Format

**Content-Type:** `multipart/form-data`

**Required Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `file` | File | Audio file (mp3, mp4, mpeg, mpga, m4a, wav, webm) |
| `model` | String | Model ID (e.g., `whisper-1`) |

**Optional Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `language` | String | auto | ISO-639-1 language code |
| `prompt` | String | - | Prompt to guide transcription |
| `response_format` | String | `json` | Output format: `json`, `text`, `srt`, `verbose_json`, `vtt` |
| `temperature` | Number | 0 | Sampling temperature (0-1) |

### Response Format (JSON)

```json
{
  "text": "Transcribed text here..."
}
```

### Verbose JSON Response

```json
{
  "task": "transcribe",
  "language": "english",
  "duration": 8.47,
  "text": "Transcribed text here...",
  "segments": [
    {
      "id": 0,
      "start": 0.0,
      "end": 2.5,
      "text": "Segment text...",
      "tokens": [...]
    }
  ]
}
```

### Limitations

- **Maximum file size:** 25 MB
- **Supported formats:** mp3, mp4, mpeg, mpga, m4a, wav, webm
- **Requires internet connection**
- **API key required** (costs per usage)

### Sample Node.js Request

```javascript
const fs = require('fs');
const https = require('https');
const FormData = require('form-data');

async function transcribeWithOpenAI(audioPath, apiKey, language = 'auto') {
    const form = new FormData();
    form.append('file', fs.createReadStream(audioPath));
    form.append('model', 'whisper-1');
    if (language !== 'auto') {
        form.append('language', language);
    }
    form.append('response_format', 'verbose_json');

    return new Promise((resolve, reject) => {
        const options = {
            hostname: 'api.openai.com',
            path: '/v1/audio/transcriptions',
            method: 'POST',
            headers: {
                ...form.getHeaders(),
                'Authorization': `Bearer ${apiKey}`
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    const result = JSON.parse(data);
                    resolve({
                        text: result.text,
                        language: result.language,
                        duration: result.duration,
                        success: true
                    });
                } catch (e) {
                    reject(new Error('Failed to parse response'));
                }
            });
        });

        req.on('error', reject);
        form.pipe(req);
    });
}
```

---

## Architecture Comparison

### Feature Comparison

| Feature | Local Backend | OpenAI API |
|---------|---------------|------------|
| **Privacy** | ✅ Fully offline | ❌ Audio sent to cloud |
| **Speed** | ⚡ GPU accelerated | 🌐 Network dependent |
| **Cost** | ✅ Free (after setup) | 💰 Pay per usage |
| **Accuracy** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Model Updates** | Manual | Automatic |
| **File Size Limit** | 50 MB | 25 MB |
| **Offline Support** | ✅ Yes | ❌ No |
| **Setup Complexity** | Higher | Lower |
| **GPU Required** | Recommended | No |

### Response Format Mapping

| Local Backend Response | OpenAI API Response | Notes |
|------------------------|---------------------|-------|
| `text` | `text` | Direct mapping |
| `language` | `language` | Direct mapping |
| `duration` | `duration` | Direct mapping |
| `success` | (derive from status) | Check HTTP status |
| `error` | (derive from error response) | Handle API errors |
| `words` | `segments[].words` | Requires `verbose_json` |

---

## Implementation Plan

### Phase 1: Frontend Changes

The primary integration happens in the **Electron main process** since transcription calls are made there.

#### 1.1 Add Transcription Provider Abstraction

Create a new module to abstract transcription providers:

**File: `src/frontend-electron/src/main/transcription-provider.js`**

```javascript
/**
 * Transcription Provider Abstraction
 * 
 * Provides a unified interface for different transcription backends.
 */

const fs = require('fs');
const path = require('path');
const http = require('http');
const https = require('https');

/**
 * Transcribe using local backend
 */
async function transcribeLocal(audioPath, language, backendPort) {
    return new Promise((resolve) => {
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;
        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);

        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        const bodyEnd = `\r\n--${boundary}\r\n` +
            `Content-Disposition: form-data; name="language"\r\n\r\n${language || 'auto'}\r\n` +
            `--${boundary}--\r\n`;

        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        const options = {
            hostname: '127.0.0.1',
            port: backendPort,
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
                try {
                    resolve({ success: true, data: JSON.parse(data) });
                } catch {
                    resolve({ success: false, error: 'Failed to parse response' });
                }
            });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: error.message });
        });

        req.write(bodyBuffer);
        req.end();
    });
}

/**
 * Transcribe using OpenAI API
 */
async function transcribeOpenAI(audioPath, language, apiKey, model = 'whisper-1') {
    return new Promise((resolve) => {
        if (!apiKey) {
            resolve({ success: false, error: 'OpenAI API key not configured' });
            return;
        }

        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;

        // Build multipart form data
        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        let bodyEnd = `\r\n--${boundary}\r\n`;
        bodyEnd += `Content-Disposition: form-data; name="model"\r\n\r\n${model}\r\n`;
        
        if (language && language !== 'auto') {
            bodyEnd += `--${boundary}\r\n`;
            bodyEnd += `Content-Disposition: form-data; name="language"\r\n\r\n${language}\r\n`;
        }
        
        bodyEnd += `--${boundary}\r\n`;
        bodyEnd += `Content-Disposition: form-data; name="response_format"\r\n\r\nverbose_json\r\n`;
        bodyEnd += `--${boundary}--\r\n`;

        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        const options = {
            hostname: 'api.openai.com',
            path: '/v1/audio/transcriptions',
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${apiKey}`,
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': bodyBuffer.length
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    const result = JSON.parse(data);
                    
                    if (res.statusCode !== 200) {
                        resolve({
                            success: false,
                            error: result.error?.message || `API error: ${res.statusCode}`
                        });
                        return;
                    }
                    
                    resolve({
                        success: true,
                        data: {
                            text: result.text,
                            language: result.language,
                            duration: result.duration,
                            success: true
                        }
                    });
                } catch {
                    resolve({ success: false, error: 'Failed to parse OpenAI response' });
                }
            });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: `OpenAI API error: ${error.message}` });
        });

        req.write(bodyBuffer);
        req.end();
    });
}

/**
 * Main transcription function - routes to appropriate provider
 */
async function transcribe(audioPath, options = {}) {
    const {
        provider = 'local',
        language = 'auto',
        backendPort = 5678,
        openaiApiKey = '',
        openaiModel = 'whisper-1'
    } = options;

    if (provider === 'openai') {
        return transcribeOpenAI(audioPath, language, openaiApiKey, openaiModel);
    } else {
        return transcribeLocal(audioPath, language, backendPort);
    }
}

module.exports = {
    transcribe,
    transcribeLocal,
    transcribeOpenAI
};
```

#### 1.2 Update Main Process IPC Handler

Modify the transcription IPC handler in `main.js` to use the provider:

```javascript
// In main.js - Updated transcribe handler

const { transcribe } = require('./main/transcription-provider');

ipcMain.handle('transcribe', async (event, audioPath, language) => {
    const provider = settings.transcriptionProvider || 'local';
    
    return await transcribe(audioPath, {
        provider: provider,
        language: language,
        backendPort: BACKEND_PORT,
        openaiApiKey: settings.openaiApiKey || '',
        openaiModel: settings.openaiModel || 'whisper-1'
    });
});
```

#### 1.3 Add New Settings

Update `defaultSettings` in `main.js`:

```javascript
const defaultSettings = {
    // ... existing settings ...
    
    // Transcription Provider Settings
    transcriptionProvider: 'local',  // 'local' or 'openai'
    openaiApiKey: '',                 // OpenAI API key (stored securely)
    openaiModel: 'whisper-1',         // OpenAI model to use
};
```

#### 1.4 Update Settings UI

Add a new section to the Settings UI for transcription provider:

**HTML (in `index.html`):**

```html
<div class="settings-section">
    <h3>Transcription Provider</h3>
    
    <div class="setting-item">
        <label for="transcription-provider-select">Provider</label>
        <select id="transcription-provider-select">
            <option value="local">Local Backend (faster-whisper)</option>
            <option value="openai">OpenAI API (Cloud)</option>
        </select>
        <small>Local: Free, offline, requires backend. OpenAI: Paid, cloud-based, no setup.</small>
    </div>
    
    <div id="openai-settings" class="hidden">
        <div class="setting-item">
            <label for="openai-api-key-input">OpenAI API Key</label>
            <input type="password" id="openai-api-key-input" placeholder="sk-...">
            <button type="button" id="toggle-api-key-visibility">Show</button>
            <small>Get your API key from <a href="#" id="openai-platform-link">platform.openai.com</a></small>
        </div>
        
        <div class="setting-item">
            <label for="openai-model-select">OpenAI Model</label>
            <select id="openai-model-select">
                <option value="whisper-1">Whisper-1 (Standard)</option>
                <option value="gpt-4o-transcribe">GPT-4o Transcribe (Higher Quality)</option>
                <option value="gpt-4o-mini-transcribe">GPT-4o Mini Transcribe (Faster)</option>
            </select>
        </div>
        
        <button type="button" id="test-openai-btn">Test OpenAI Connection</button>
    </div>
</div>
```

**JavaScript (in settings module):**

```javascript
// Show/hide OpenAI settings based on provider selection
document.getElementById('transcription-provider-select').addEventListener('change', (e) => {
    const openaiSettings = document.getElementById('openai-settings');
    openaiSettings.classList.toggle('hidden', e.target.value !== 'openai');
});
```

---

### Phase 2: Backend Changes (Optional)

While the frontend can handle OpenAI API calls directly, adding backend support provides several benefits:

1. **API Key Security** - Keys stored server-side, not in Electron app
2. **Proxy Mode** - Backend acts as a proxy to OpenAI
3. **Unified Interface** - Same endpoint, provider configured server-side
4. **Future Flexibility** - Easy to add more providers

#### 2.1 Add Provider Configuration

**Update `config.py`:**

```python
class Settings(BaseSettings):
    # ... existing settings ...
    
    # Transcription Provider Settings
    transcription_provider: str = Field(
        default="local",
        description="Transcription provider: 'local' or 'openai'"
    )
    openai_api_key: str = Field(
        default="",
        description="OpenAI API key for cloud transcription"
    )
    openai_model: str = Field(
        default="whisper-1",
        description="OpenAI model to use"
    )
```

#### 2.2 Create OpenAI Transcription Service

**New file: `src/backend/services/openai_transcriber.py`**

```python
"""OpenAI Transcription Service."""

import httpx
import logging
from pathlib import Path
from typing import Optional

from config import settings
from protocols import TranscriptionResult

logger = logging.getLogger(__name__)

OPENAI_API_URL = "https://api.openai.com/v1/audio/transcriptions"


class OpenAITranscriberService:
    """Transcription service using OpenAI API."""
    
    def __init__(self):
        self._api_key: Optional[str] = None
        self._model: str = "whisper-1"
    
    def configure(self, api_key: str, model: str = "whisper-1") -> None:
        """Configure the OpenAI transcriber."""
        self._api_key = api_key
        self._model = model
    
    def is_configured(self) -> bool:
        """Check if API key is configured."""
        return bool(self._api_key)
    
    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
    ) -> TranscriptionResult:
        """Transcribe audio using OpenAI API."""
        if not self._api_key:
            return TranscriptionResult(
                text="",
                success=False,
                duration_seconds=0,
                error="OpenAI API key not configured",
            )
        
        try:
            audio_file = Path(audio_path)
            
            async with httpx.AsyncClient(timeout=60.0) as client:
                # Open file with context manager to ensure proper cleanup
                with open(audio_path, "rb") as f:
                    files = {
                        "file": (audio_file.name, f, "audio/wav"),
                    }
                    data = {
                        "model": self._model,
                        "response_format": "verbose_json",
                    }
                    
                    if language != "auto":
                        data["language"] = language
                    
                    response = await client.post(
                        OPENAI_API_URL,
                        files=files,
                        data=data,
                        headers={"Authorization": f"Bearer {self._api_key}"},
                    )
                    
                    if response.status_code != 200:
                        error_data = response.json()
                        return TranscriptionResult(
                            text="",
                            success=False,
                            duration_seconds=0,
                            error=error_data.get("error", {}).get("message", f"API error: {response.status_code}"),
                        )
                    
                    result = response.json()
                    
                    return TranscriptionResult(
                        text=result.get("text", ""),
                        success=True,
                        duration_seconds=result.get("duration", 0),
                        language=result.get("language"),
                    )
                
        except Exception as e:
            logger.error(f"OpenAI transcription failed: {e}")
            return TranscriptionResult(
                text="",
                success=False,
                duration_seconds=0,
                error=str(e),
            )
```

#### 2.3 Create Unified Transcription Router

**New file: `src/backend/services/transcription_router.py`**

```python
"""Unified Transcription Router."""

from typing import Optional

from config import settings
from protocols import TranscriptionResult
from services.transcriber import TranscriberService
from services.openai_transcriber import OpenAITranscriberService


class TranscriptionRouter:
    """Routes transcription requests to appropriate provider."""
    
    def __init__(self):
        self._local_transcriber = TranscriberService()
        self._openai_transcriber = OpenAITranscriberService()
        self._current_provider = "local"
    
    def set_provider(self, provider: str) -> None:
        """Set the active transcription provider."""
        if provider in ("local", "openai"):
            self._current_provider = provider
    
    def configure_openai(self, api_key: str, model: str = "whisper-1") -> None:
        """Configure OpenAI provider."""
        self._openai_transcriber.configure(api_key, model)
    
    @property
    def local_transcriber(self) -> TranscriberService:
        """Get local transcriber for model management."""
        return self._local_transcriber
    
    def is_ready(self) -> bool:
        """Check if current provider is ready."""
        if self._current_provider == "openai":
            return self._openai_transcriber.is_configured()
        return self._local_transcriber.is_loaded()
    
    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
        initial_prompt: Optional[str] = None,
        word_timestamps: bool = False,
    ) -> TranscriptionResult:
        """Route transcription to appropriate provider."""
        if self._current_provider == "openai":
            return await self._openai_transcriber.transcribe(
                audio_path=audio_path,
                language=language,
                task=task,
            )
        else:
            return await self._local_transcriber.transcribe(
                audio_path=audio_path,
                language=language,
                task=task,
                initial_prompt=initial_prompt,
                word_timestamps=word_timestamps,
            )
```

#### 2.4 Update API Endpoints

Modify `api/transcribe.py` to use the router:

```python
# Update to use TranscriptionRouter instead of direct TranscriberService
from services.transcription_router import TranscriptionRouter

# The router handles provider selection transparently
```

---

### Phase 3: Settings and Configuration

#### 3.1 Settings Schema Update

Add new settings to `settings.json`:

```json
{
  "windowToggleHotkey": "Ctrl+Shift+V",
  "toggleRecordingHotkey": "Ctrl+Shift+R",
  "modelName": "small",
  "language": "auto",
  "outputMode": "ClipboardAndPaste",
  "showNotifications": true,
  "showRecordingIndicator": true,
  "audioDeviceId": -1,
  "clipboardDelayMs": 50,
  "firstRunCompleted": false,
  "backendPort": 5678,
  "backendHost": "127.0.0.1",
  "startMinimized": true,
  "startWithWindows": false,
  "theme": "system",
  
  "transcriptionProvider": "local",
  "openaiApiKey": "",
  "openaiModel": "whisper-1"
}
```

#### 3.2 Secure API Key Storage

For production, consider using Electron's `safeStorage`:

```javascript
const { safeStorage } = require('electron');

function saveApiKey(key) {
    if (safeStorage.isEncryptionAvailable()) {
        const encrypted = safeStorage.encryptString(key);
        // Store encrypted buffer
        return encrypted;
    }
    // Fallback: store as-is (not recommended)
    return key;
}

function loadApiKey(encryptedKey) {
    if (safeStorage.isEncryptionAvailable() && Buffer.isBuffer(encryptedKey)) {
        return safeStorage.decryptString(encryptedKey);
    }
    return encryptedKey;
}
```

---

## Detailed Implementation Guide

### Step-by-Step Implementation

#### Step 1: Create the Transcription Provider Module

1. Create directory: `src/frontend-electron/src/main/`
2. Create file: `transcription-provider.js` (see code above)
3. Export the transcribe function

#### Step 2: Update Main Process

1. Import the new module in `main.js`
2. Replace the inline transcription code with provider call
3. Add new settings to `defaultSettings`

#### Step 3: Update Settings UI

1. Add HTML for provider selection
2. Add JavaScript for show/hide logic
3. Add API key input with visibility toggle
4. Add model selector for OpenAI

#### Step 4: Update Settings Module

1. Add new settings to `saveGeneralSettings()`
2. Add new settings to `applySettingsToUI()`
3. Handle secure API key storage

#### Step 5: Update IPC Preload (if needed)

If adding new IPC methods:
```javascript
// In preload.js - add if needed
testOpenAIConnection: (apiKey) => ipcRenderer.invoke('test-openai-connection', apiKey),
```

#### Step 6: Test Integration

1. Test local backend still works
2. Test OpenAI API with valid key
3. Test switching between providers
4. Test error handling (no key, invalid key, network issues)

---

## Security Considerations

### API Key Security

| Risk | Mitigation |
|------|------------|
| Key exposure in settings file | Use `safeStorage` encryption |
| Key in process memory | Clear after use, minimize storage time |
| Key transmitted insecurely | Always use HTTPS for OpenAI API |
| Key logged accidentally | Redact in logs, avoid console.log |

### Network Security

| Risk | Mitigation |
|------|------------|
| Audio data exposure | Only sent when OpenAI selected |
| Man-in-middle attacks | TLS/HTTPS enforced |
| Data retention | Review OpenAI data policies |

### Best Practices

1. **Never log API keys** - Redact in all logging
2. **Use encrypted storage** - Electron's `safeStorage` API
3. **Clear sensitive data** - Minimize time in memory
4. **Inform users** - Make it clear when data is sent to cloud

---

## Testing Strategy

### Unit Tests

1. Test `transcription-provider.js` with mocked responses
2. Test settings loading/saving with new fields
3. Test API key encryption/decryption

### Integration Tests

1. Test local backend transcription flow
2. Test OpenAI API transcription flow (with test key)
3. Test provider switching mid-session

### E2E Tests

1. Record audio → transcribe with local backend
2. Record audio → transcribe with OpenAI (mock API)
3. Settings change → verify provider switch
4. Error handling → API failures, network issues

### Manual Testing Checklist

- [ ] Local backend transcription works
- [ ] OpenAI transcription works with valid key
- [ ] Error shown with missing/invalid API key
- [ ] Provider switch works without restart
- [ ] Settings persist across app restarts
- [ ] API key hidden in settings UI by default

---

## Migration Path

### For Existing Users

1. **No breaking changes** - Default to local backend
2. **Opt-in** - OpenAI requires explicit configuration
3. **Preserve settings** - All existing settings maintained

### Configuration Migration

```javascript
// In main.js - settings migration
function migrateSettings(settings) {
    // Add new settings with defaults if missing
    if (!settings.hasOwnProperty('transcriptionProvider')) {
        settings.transcriptionProvider = 'local';
    }
    if (!settings.hasOwnProperty('openaiApiKey')) {
        settings.openaiApiKey = '';
    }
    if (!settings.hasOwnProperty('openaiModel')) {
        settings.openaiModel = 'whisper-1';
    }
    return settings;
}
```

---

## Cost Estimation (OpenAI)

### Pricing (as of 2024)

| Model | Cost per Minute |
|-------|-----------------|
| `whisper-1` | ~$0.006/minute |
| `gpt-4o-transcribe` | Higher (check OpenAI pricing) |

### Usage Estimate

| Usage Pattern | Minutes/Month | Estimated Cost |
|---------------|---------------|----------------|
| Light (5 min/day) | 150 | ~$0.90 |
| Moderate (15 min/day) | 450 | ~$2.70 |
| Heavy (30 min/day) | 900 | ~$5.40 |

---

## Summary

This integration plan provides a comprehensive guide to adding OpenAI Transcription API support to VoxTether. The key benefits include:

1. **User Choice** - Switch between local (free, private) and cloud (convenient, high-quality)
2. **Minimal Changes** - Frontend-only changes sufficient for basic integration
3. **Backend Flexibility** - Optional backend changes for enterprise features
4. **Security First** - API key encryption and secure storage
5. **Backward Compatible** - No changes for users who prefer local transcription

The recommended approach is to start with **Phase 1 (Frontend Changes)** for a minimum viable implementation, then add **Phase 2 (Backend Changes)** for advanced features like proxy mode and centralized API key management.
