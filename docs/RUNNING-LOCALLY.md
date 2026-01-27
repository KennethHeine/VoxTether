# Running VoxTether Locally (Development)

This guide explains how to run VoxTether from source for development purposes.

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| Python | 3.11+ | Backend runtime |
| .NET SDK | 8.0+ | Frontend build |
| Git | Latest | Source control |

**Check installations:**
```powershell
python --version   # Should be 3.11+
dotnet --version   # Should be 8.0+
git --version
```

### Hardware Requirements

| Requirement | Minimum | Recommended |
|------------|---------|-------------|
| OS | Windows 10 (64-bit) | Windows 11 |
| RAM | 4 GB | 8 GB |
| Disk Space | 2 GB | 4 GB |
| GPU | None (CPU works) | NVIDIA with CUDA 12 |

---

## Step 1: Get the Source Code

```powershell
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether
```

---

## Step 2: Set Up the Backend

The backend is a Python FastAPI server that handles transcription.

```powershell
# Navigate to backend
cd src/backend

# Create virtual environment
python -m venv venv

# Activate virtual environment
.\venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt
```

### Troubleshooting: PowerShell Execution Policy

If you get an error about script execution being disabled:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

---

## Step 3: Run the Backend

With the virtual environment active:

```powershell
# Start the backend server
python -m uvicorn main:app --host 127.0.0.1 --port 5678 --reload
```

The backend will start and listen on `http://127.0.0.1:5678`.

**Verify it's running:**
```powershell
curl http://127.0.0.1:5678/api/health
```

You should see: `{"status":"ok",...}`

---

## Step 4: Set Up the Frontend

Open a **new terminal** (keep the backend running).

```powershell
# Navigate to frontend
cd src/frontend

# Restore dependencies
dotnet restore VoxTether.sln
```

---

## Step 5: Run the Frontend

```powershell
# Run the WinUI 3 application
dotnet run --project VoxTether
```

VoxTether will start and appear in your system tray.

---

## GPU Acceleration (Optional)

For faster transcription with NVIDIA GPUs:

```powershell
# In the backend virtual environment
cd src/backend
.\venv\Scripts\Activate.ps1
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Restart the backend after installing.

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

### Running Both Components

**Terminal 1 (Backend):**
```powershell
cd src/backend
.\venv\Scripts\Activate.ps1
python -m uvicorn main:app --host 127.0.0.1 --port 5678 --reload
```

**Terminal 2 (Frontend):**
```powershell
cd src/frontend
dotnet run --project VoxTether
```

### Hot Reload

- **Backend**: The `--reload` flag enables automatic reloading when Python files change
- **Frontend**: Restart the application to pick up changes

### Running Tests

```powershell
# Backend tests
cd src/backend
.\venv\Scripts\Activate.ps1
pip install pytest
pytest

# Legacy Python tests
cd ../..
pip install -r requirements-dev.txt
pytest tests/

# Frontend tests
cd src/frontend
dotnet test
```

### Linting

```powershell
# Backend linting
cd src/backend
pip install ruff
ruff check .

# Legacy code linting
cd ../..
ruff check src/ tests/
```

---

## Building for Release

```powershell
# Build everything
cd build
.\build.ps1 -Release -Version "2.0.0"

# Build with installer
.\build.ps1 -Release -CreateInstaller -Version "2.0.0"
```

Output:
- `build/output/` - Application files
- `build/VoxTether-x.x.x-win-x64.zip` - Portable ZIP
- `build/installer/VoxTether-x.x.x-Setup.exe` - Windows installer

---

## Project Structure

```
VoxTether/
├── src/
│   ├── frontend/          # WinUI 3 (.NET 8.0)
│   │   ├── VoxTether/     # Main app
│   │   ├── VoxTether.Core/
│   │   └── VoxTether.Infrastructure/
│   ├── backend/           # Python FastAPI
│   │   ├── api/           # REST endpoints
│   │   ├── services/      # Business logic
│   │   └── main.py        # Entry point
│   └── (legacy)           # Original Python UI
├── build/                 # Build scripts
├── installer/             # Inno Setup script
├── docs/                  # Documentation
└── tests/                 # Unit tests
```

---

## Troubleshooting

### Backend won't start

1. Check if port 5678 is in use:
   ```powershell
   netstat -ano | findstr 5678
   ```
2. Try a different port:
   ```powershell
   python -m uvicorn main:app --port 5679
   ```

### Frontend can't connect to backend

1. Ensure backend is running on port 5678
2. Check the backend URL in Settings

### "dotnet: command not found"

Install .NET 8.0 SDK from [dot.net](https://dot.net/download).

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
