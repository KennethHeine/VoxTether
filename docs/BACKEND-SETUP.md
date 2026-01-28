# VoxTether Backend Setup Guide

This guide explains how to set up and run the VoxTether backend server.

## Overview

The VoxTether backend is a Python FastAPI server that handles:
- Speech-to-text transcription using faster-whisper
- Model management (downloading, loading, unloading)
- GPU acceleration with CUDA

## Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| Python | 3.13+ | 3.13+ |
| RAM | 4 GB | 8 GB+ |
| GPU | None | NVIDIA with CUDA 12 |
| Disk | 500 MB | 5 GB (for models) |

---

## Quick Start

### 1. Install Dependencies

```bash
cd src/backend
pip install -r requirements.txt
```

### 2. Download a Model

```bash
python cli.py download small
```

### 3. Start the Server

```bash
python cli.py serve
```

The server will start on `http://127.0.0.1:5678`.

---

## CLI Reference

The backend includes a CLI tool (`cli.py`) for managing models and configuration.

### List Available Models

```bash
python cli.py list
```

Output:
```
Models directory: /path/to/models

Available Models:
--------------------------------------------------------------------------------
Name                 Size         Downloaded   Description
--------------------------------------------------------------------------------
tiny                 75 MB        ✗ No         Fastest, lowest accuracy
base                 142 MB       ✗ No         Fast with reasonable accuracy
small                466 MB       ✓ Yes        Good balance of speed and accuracy
medium               1500 MB      ✗ No         High accuracy, slower transcription
large-v3             3000 MB      ✗ No         Best accuracy, slowest
large-v3-turbo       1600 MB      ✗ No         Excellent accuracy with faster speed
distil-large-v3      1100 MB      ✗ No         Distilled model with excellent speed
--------------------------------------------------------------------------------
```

### Download a Model

```bash
# Download the 'small' model (recommended)
python cli.py download small

# Force re-download
python cli.py download small --force
```

### Delete a Model

```bash
# Delete with confirmation
python cli.py delete small

# Delete without confirmation
python cli.py delete small --yes
```

### Show Configuration

```bash
python cli.py config
```

Output:
```
Current Configuration:
--------------------------------------------------
Host:           127.0.0.1
Port:           5678
Debug:          False
Models path:    C:\Users\...\AppData\Roaming\VoxTether\models
Default model:  small
Preload model:  True
Device:         auto
Compute type:   auto
Language:       auto
--------------------------------------------------
```

### Start the Server

```bash
# Start with default settings
python cli.py serve

# Start on a specific port
python cli.py serve --port 8000

# Start accessible from network
python cli.py serve --host 0.0.0.0

# Start with auto-reload (development)
python cli.py serve --reload

# Start with debug logging
python cli.py serve --debug
```

### Show System Info

```bash
python cli.py info
```

---

## Configuration

### Environment Variables

All settings can be configured via environment variables with the `VOXTETHER_` prefix:

| Variable | Default | Description |
|----------|---------|-------------|
| `VOXTETHER_HOST` | `127.0.0.1` | Server bind address |
| `VOXTETHER_PORT` | `5678` | Server port |
| `VOXTETHER_DEBUG` | `False` | Enable debug mode |
| `VOXTETHER_MODELS_PATH` | `%APPDATA%/VoxTether/models` | Models directory |
| `VOXTETHER_DEFAULT_MODEL` | `small` | Default model to load |
| `VOXTETHER_PRELOAD_MODEL` | `True` | Preload model on startup |
| `VOXTETHER_DEVICE` | `auto` | Device (auto, cuda, cpu) |
| `VOXTETHER_COMPUTE_TYPE` | `auto` | Compute type (auto, float16, int8) |
| `VOXTETHER_DEFAULT_LANGUAGE` | `auto` | Default transcription language |

### Using a .env File

Create a `.env` file in `src/backend/`:

```env
VOXTETHER_HOST=0.0.0.0
VOXTETHER_PORT=5678
VOXTETHER_DEFAULT_MODEL=medium
VOXTETHER_DEVICE=cuda
```

---

## GPU Acceleration

### Installing CUDA Support

For NVIDIA GPU acceleration:

```bash
# Option 1: Using pip (recommended)
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12

# Option 2: Install CUDA Toolkit 12.x from NVIDIA
# https://developer.nvidia.com/cuda-downloads
```

### Verify GPU Detection

```bash
python cli.py info
```

Output with GPU:
```
System Information:
--------------------------------------------------
PyTorch version: 2.1.0+cu121
CUDA available:  True
CUDA version:    12.1
GPU count:       1
  GPU 0: NVIDIA GeForce RTX 3080

faster-whisper: Available
--------------------------------------------------
```

---

## Running as a Service

### Windows (Using NSSM)

1. Download [NSSM](https://nssm.cc/download)
2. Install as a service:

```powershell
nssm install VoxTetherBackend "C:\Python313\python.exe" "C:\path\to\src\backend\cli.py serve"
nssm set VoxTetherBackend AppDirectory "C:\path\to\src\backend"
nssm start VoxTetherBackend
```

### Linux (Using systemd)

Create `/etc/systemd/system/voxtether-backend.service`:

```ini
[Unit]
Description=VoxTether Backend Server
After=network.target

[Service]
Type=simple
User=your-user
WorkingDirectory=/path/to/src/backend
ExecStart=/usr/bin/python3 cli.py serve --host 0.0.0.0
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Then:
```bash
sudo systemctl enable voxtether-backend
sudo systemctl start voxtether-backend
```

---

## Network Deployment

To make the backend accessible from other machines:

1. Start with network binding:
   ```bash
   python cli.py serve --host 0.0.0.0
   ```

2. Configure firewall to allow port 5678

3. On client machines, configure the frontend to connect to the server's IP

---

## Troubleshooting

### Server won't start

1. Check if port 5678 is in use:
   ```bash
   # Windows
   netstat -ano | findstr 5678
   
   # Linux
   lsof -i :5678
   ```

2. Try a different port:
   ```bash
   python cli.py serve --port 5679
   ```

### GPU not detected

1. Verify CUDA installation:
   ```bash
   python cli.py info
   ```

2. Install CUDA packages:
   ```bash
   pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
   ```

3. Check NVIDIA driver version (should support CUDA 12)

### Model download fails

1. Check internet connection
2. Verify disk space
3. Try manual download:
   ```bash
   python cli.py download small --force
   ```

### Out of memory

1. Use a smaller model (tiny, base, small)
2. Force CPU mode:
   ```bash
   export VOXTETHER_DEVICE=cpu
   python cli.py serve
   ```

---

## API Documentation

See [BACKEND-API.md](BACKEND-API.md) for full API documentation.

When the server is running, interactive documentation is available at:
- Swagger UI: http://127.0.0.1:5678/docs
- ReDoc: http://127.0.0.1:5678/redoc
