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
  "status": "ok",
  "version": "1.0.0",
  "cuda_available": true,
  "device": "cuda",
  "compute_type": "float16",
  "model_loaded": true,
  "current_model": "small"
}
```

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
- Body: Audio file (WAV, MP3, FLAC, etc.)

**Query Parameters:**
- `language` (optional): Language code (e.g., "en", "de", "auto"). Default: "auto"

**Response:**
```json
{
  "text": "Hello, this is a test transcription.",
  "language": "en",
  "duration": 3.5,
  "segments": [
    {
      "start": 0.0,
      "end": 3.5,
      "text": "Hello, this is a test transcription."
    }
  ]
}
```

**Example with curl:**
```bash
curl -X POST "http://127.0.0.1:5678/api/transcribe?language=auto" \
  -F "file=@recording.wav"
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
