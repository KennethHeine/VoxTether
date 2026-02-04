# VoxTether Backend Features

This document provides a comprehensive overview of all features in the VoxTether backend.

## Overview

The VoxTether backend is a Python FastAPI server that provides speech-to-text transcription services using faster-whisper. It runs as a standalone HTTP server and communicates with the Electron frontend via a REST API.

---

## Core Features

### 1. Speech-to-Text Transcription

The primary feature of the backend is converting audio to text using the faster-whisper library.

**Capabilities:**
- **Audio Format Support**: WAV, MP3, FLAC, and other common formats
- **Language Detection**: Automatic language detection or explicit language specification
- **Translation**: Option to translate non-English audio to English
- **VAD (Voice Activity Detection)**: Built-in filtering to skip silent sections

**Technical Details:**
- Uses CTranslate2 backend for optimized inference
- Supports beam search with configurable beam size (default: 5)
- VAD filter enabled by default for better accuracy

---

### 2. Model Management

The backend provides complete lifecycle management for Whisper models.

**Features:**
- **List Available Models**: View all supported models with their sizes and descriptions
- **Download Models**: Download models from HuggingFace with progress tracking
- **Delete Models**: Remove downloaded models to free disk space
- **Load/Unload Models**: Dynamically switch between models at runtime

**Supported Models:**

| Model | Size | Price | Best For |
|-------|------|-------|----------|
| `tiny` | ~75 MB | Free (local) | Quick notes, testing |
| `base` | ~142 MB | Free (local) | General use |
| `small` | ~466 MB | Free (local) | Recommended for most users |
| `medium` | ~1.5 GB | Free (local) | High accuracy |
| `large-v3` | ~3 GB | Free (local) | Best accuracy |
| `large-v3-turbo` | ~1.6 GB | Free (local) | Best speed/accuracy balance |
| `distil-large-v3` | ~1.1 GB | Free (local) | Fast high-quality transcription |

---

### 3. GPU Acceleration

The backend automatically detects and uses NVIDIA GPUs when available.

**Features:**
- **Auto-detection**: Automatically detects CUDA-capable GPUs
- **Fallback to CPU**: Gracefully falls back to CPU if GPU fails
- **Multiple Detection Methods**: Uses PyTorch, ctranslate2, or nvidia-smi
- **CUDA DLL Path Setup**: Automatically configures NVIDIA DLL paths on Windows

**Compute Types:**
- `float16`: Best performance on GPU (default for CUDA)
- `int8`: Best performance on CPU (default for CPU)
- `float32`: Highest precision (slower)

---

### 4. Health Monitoring

The backend provides health check endpoints for monitoring and frontend connectivity.

**Endpoints:**
- `GET /api/health`: Returns server status, model state, and device info
- `GET /api/devices`: Returns detailed GPU/CPU device information

---

### 5. Configuration System

Flexible configuration through environment variables or `.env` file.

**Configurable Settings:**

| Setting | Default | Description |
|---------|---------|-------------|
| `VOXTETHER_HOST` | `127.0.0.1` | Server bind address |
| `VOXTETHER_PORT` | `5678` | Server port |
| `VOXTETHER_DEBUG` | `False` | Enable debug mode |
| `VOXTETHER_MODELS_PATH` | `%APPDATA%/VoxTether/models` | Models directory |
| `VOXTETHER_DEFAULT_MODEL` | `small` | Default model to load |
| `VOXTETHER_PRELOAD_MODEL` | `True` | Preload model on startup |
| `VOXTETHER_DEVICE` | `auto` | Device (auto, cuda, cpu) |
| `VOXTETHER_COMPUTE_TYPE` | `auto` | Compute type |
| `VOXTETHER_DEFAULT_LANGUAGE` | `auto` | Default transcription language |

---

### 6. Command-Line Interface (CLI)

A full-featured CLI for managing the backend without the frontend.

**Commands:**

| Command | Description |
|---------|-------------|
| `python cli.py list` | List available models and download status |
| `python cli.py download <model>` | Download a model |
| `python cli.py delete <model>` | Delete a downloaded model |
| `python cli.py config` | Show current configuration |
| `python cli.py serve` | Start the backend server |
| `python cli.py info` | Show system and GPU information |

**CLI Flags:**
- `--force` / `-f`: Force re-download of a model
- `--yes` / `-y`: Skip confirmation prompts
- `--host` / `-H`: Override server host
- `--port` / `-p`: Override server port
- `--reload` / `-r`: Enable auto-reload for development
- `--debug` / `-d`: Enable debug logging

---

### 7. Logging

Comprehensive logging for debugging and monitoring.

**Features:**
- **File Logging**: Logs written to `%APPDATA%/VoxTether/logs/backend.log`
- **Console Output**: Real-time logging to stdout
- **Configurable Level**: Debug or Info level based on settings
- **Structured Format**: Timestamp, module, level, and message

---

### 8. Server-Sent Events (SSE)

Real-time progress updates for long-running operations.

**Used For:**
- Model download progress with percentage, speed, and ETA
- Allows the frontend to display accurate download progress bars

---

### 9. Async/Thread Pool Architecture

Non-blocking architecture for responsive performance.

**Implementation:**
- FastAPI async endpoints for HTTP handling
- ThreadPoolExecutor for blocking transcription operations
- Prevents model loading/transcription from blocking the HTTP server
- Proper cleanup with atexit handlers

---

### 10. CORS Security

Secure cross-origin configuration for local communication.

**Configuration:**
- Origin regex restricted to `localhost` and `127.0.0.1`
- Supports credentials for authenticated requests
- Allows all HTTP methods and headers for API flexibility

---

## API Endpoints Summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/health` | Health check and status |
| `GET` | `/api/devices` | GPU/CPU device information |
| `POST` | `/api/transcribe` | Transcribe audio file |
| `POST` | `/api/settings` | Update transcription settings |
| `GET` | `/api/models` | List available models |
| `POST` | `/api/models/{name}/download` | Download a model (SSE) |
| `POST` | `/api/models/{name}/load` | Load a model |
| `POST` | `/api/models/{name}/unload` | Unload current model |
| `DELETE` | `/api/models/{name}` | Delete a model |

---

## Data Storage

| Data | Location |
|------|----------|
| Models | `%APPDATA%\VoxTether\models\` |
| Logs | `%APPDATA%\VoxTether\logs\` |

---

## See Also

- [Backend API Documentation](BACKEND-API.md) - Detailed API reference
- [Backend Setup Guide](BACKEND-SETUP.md) - Installation and configuration
- [Architecture](ARCHITECTURE.md) - System architecture overview
