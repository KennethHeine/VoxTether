# VoxTether Backend API Documentation

The VoxTether backend provides a REST API for speech-to-text transcription using faster-whisper.

## Base URL

```
http://127.0.0.1:5678/api
```

The backend binds to `127.0.0.1` by default for security. For network access, start with `--host 0.0.0.0`.

---

## Authentication

No authentication is required. The API is intended for localhost use only.

---

## Endpoints

### Health

#### GET /api/health

Check if the backend is running and get system information.

**Response:**
```json
{
  "status": "healthy",
  "version": "2.0.0",
  "model_loaded": true,
  "model_name": "small",
  "device": "cuda",
  "uptime_seconds": 123.45,
  "checks": {
    "transcriber": "healthy",
    "model": "loaded"
  }
}
```

**Status Values:**
- `healthy`: Backend running, model loaded
- `degraded`: Backend running, no model loaded
- `unhealthy`: Backend not ready

---

### Models

#### GET /api/models

List all available models and their download status.

**Response:**
```json
{
  "models": [
    {
      "name": "tiny",
      "display_name": "Tiny",
      "size_mb": 75,
      "downloaded": true,
      "path": "/path/to/models/tiny",
      "description": "Fastest, lowest accuracy. Good for quick notes."
    },
    {
      "name": "small",
      "display_name": "Small",
      "size_mb": 466,
      "downloaded": true,
      "path": "/path/to/models/small",
      "description": "Good balance of speed and accuracy. Recommended for most users."
    }
  ],
  "current_model": "small"
}
```

#### POST /api/models/{model_name}/load

Load a downloaded model for transcription.

**Parameters:**
- `model_name` (path): Name of the model to load (e.g., "small", "medium")

**Response (Success):**
```json
{
  "success": true,
  "model": "small"
}
```

**Response (Error):**
```json
{
  "detail": "Model not found: large-v3"
}
```

#### POST /api/models/{model_name}/unload

Unload the currently loaded model.

**Response:**
```json
{
  "success": true
}
```

---

### Transcription

#### POST /api/transcribe

Transcribe an audio file.

**Request:**
- Content-Type: `multipart/form-data`
- Body: Audio file (WAV, MP3, FLAC, OGG, M4A, WebM)

**Form Parameters:**
- `file` (required): Audio file to transcribe
- `language` (optional): Language code (e.g., "en", "de", "auto"). Default: "auto"
- `translate` (optional): Translate to English. Default: false
- `initial_prompt` (optional): Prompt to guide transcription (e.g., domain-specific terms)
- `word_timestamps` (optional): Return word-level timestamps. Default: false

**Response:**
```json
{
  "text": "Hello, this is a test transcription.",
  "language": "en",
  "duration": 3.5,
  "success": true,
  "error": null,
  "words": null
}
```

**Response with word_timestamps=true:**
```json
{
  "text": "Hello, this is a test.",
  "language": "en",
  "duration": 3.5,
  "success": true,
  "error": null,
  "words": [
    {"word": "Hello,", "start": 0.0, "end": 0.5, "probability": 0.95},
    {"word": "this", "start": 0.6, "end": 0.8, "probability": 0.98},
    {"word": "is", "start": 0.9, "end": 1.0, "probability": 0.97},
    {"word": "a", "start": 1.1, "end": 1.2, "probability": 0.99},
    {"word": "test.", "start": 1.3, "end": 1.8, "probability": 0.96}
  ]
}
```

**Error Responses:**
- `400 Bad Request`: Invalid file type
- `413 Request Entity Too Large`: File exceeds size limit (default: 50 MB)
- `503 Service Unavailable`: No model loaded

**Example with curl:**
```bash
curl -X POST "http://127.0.0.1:5678/api/transcribe" \
  -F "file=@recording.wav" \
  -F "language=auto" \
  -F "initial_prompt=VoxTether, transcription, API"
```

---

### Devices

#### GET /api/devices

Get information about available compute devices.

**Response:**
```json
{
  "cuda_available": true,
  "device_name": "NVIDIA GeForce RTX 3080",
  "cuda_version": "12.1",
  "device_count": 1,
  "current_device": "cuda"
}
```

---

## Error Responses

All endpoints return standard HTTP status codes:

- `200 OK`: Request successful
- `400 Bad Request`: Invalid request parameters
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

Error response format:
```json
{
  "detail": "Error message describing what went wrong"
}
```

---

## Available Models

| Name | Display Name | Size | Description |
|------|--------------|------|-------------|
| `tiny` | Tiny | ~75 MB | Fastest, lowest accuracy |
| `base` | Base | ~142 MB | Fast with reasonable accuracy |
| `small` | Small | ~466 MB | Recommended for most users |
| `medium` | Medium | ~1.5 GB | High accuracy, slower |
| `large-v3` | Large V3 | ~3 GB | Best accuracy, requires GPU |
| `large-v3-turbo` | Large V3 Turbo | ~1.6 GB | Excellent accuracy, fast on GPU |
| `distil-large-v3` | Distil Large V3 | ~1.1 GB | Distilled model, good speed/accuracy |

---

## OpenAPI Documentation

When the backend is running, visit:
- **Swagger UI**: http://127.0.0.1:5678/docs
- **ReDoc**: http://127.0.0.1:5678/redoc
