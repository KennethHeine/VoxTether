# Running VoxTether Locally with Python

This guide explains how to run VoxTether directly with Python on your Windows PC, without building a standalone executable.

## Prerequisites

### 1. Python Installation

You need Python 3.10 or later installed on your system.

**Check if Python is installed:**
```powershell
python --version
```

If Python is not installed, download it from [python.org](https://www.python.org/downloads/windows/).

> **Important:** During installation, check the box **"Add Python to PATH"** to make Python accessible from the command line.

### 2. Git (Optional)

Git is recommended for cloning the repository, but you can also download the source as a ZIP file.

**Check if Git is installed:**
```powershell
git --version
```

If Git is not installed, download it from [git-scm.com](https://git-scm.com/download/win).

### 3. Hardware Requirements

| Requirement | Minimum | Recommended |
|------------|---------|-------------|
| OS | Windows 10 (64-bit) | Windows 11 |
| RAM | 4 GB | 8 GB |
| Disk Space | 500 MB + model size | 2 GB |
| GPU | None (CPU works) | NVIDIA with CUDA 12 |

---

## Step 1: Get the Source Code

### Option A: Clone with Git (Recommended)

```powershell
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether
```

### Option B: Download ZIP

1. Go to [github.com/KennethHeine/VoxTether](https://github.com/KennethHeine/VoxTether)
2. Click the green **"Code"** button
3. Select **"Download ZIP"**
4. Extract the ZIP to a folder (e.g., `C:\VoxTether`)
5. Open PowerShell and navigate to the folder:
   ```powershell
   cd C:\VoxTether
   ```

---

## Step 2: Create a Virtual Environment

A virtual environment keeps VoxTether's dependencies separate from your system Python.

```powershell
# Create the virtual environment
python -m venv venv
```

---

## Step 3: Activate the Virtual Environment

You need to activate the virtual environment before installing dependencies or running the app.

**PowerShell:**
```powershell
.\venv\Scripts\Activate.ps1
```

**Command Prompt (cmd.exe):**
```cmd
venv\Scripts\activate.bat
```

> **Note:** You'll see `(venv)` at the beginning of your prompt when the virtual environment is active.

### Troubleshooting: PowerShell Execution Policy

If you get an error about script execution being disabled, run this command:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try activating again.

---

## Step 4: Install Dependencies

With the virtual environment active, install the required packages:

```powershell
pip install -r requirements.txt
```

This will install all necessary dependencies:
- `faster-whisper` - Speech-to-text engine
- `sounddevice` - Audio recording
- `pystray` - System tray support
- `keyboard` - Global hotkey detection
- And other supporting packages

---

## Step 5: Run VoxTether

### Basic Run

```powershell
python -m src.main
```

VoxTether will start and appear in your system tray (near the clock).

### First Run

On first launch, VoxTether will:
1. Detect your GPU hardware
2. Prompt you to download a speech recognition model
3. Show the default hotkey configuration

### Run with Debug Logging

For troubleshooting, run with debug output:

```powershell
python -m src.main --debug
```

### Run Healthcheck

Verify your system configuration:

```powershell
python -m src.main --healthcheck
```

---

## Step 6: GPU Acceleration (Optional)

By default, VoxTether uses CPU for transcription. For faster performance with NVIDIA GPUs:

```powershell
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

Verify GPU detection:
```powershell
python -m src.main --healthcheck
```

Look for `✓ CUDA available` in the output.

---

## Using VoxTether

### Default Hotkey

**Ctrl + Shift + Space**

Press and hold the hotkey to record. Release to transcribe and insert text.

### System Tray Menu

Right-click the VoxTether tray icon to access:

| Menu Item | Description |
|-----------|-------------|
| Settings... | Configure hotkey, model, and options |
| Test Microphone | Record a 2-second test and show transcription |
| Open Models Folder | Access downloaded models |
| Open Logs | Access log files |
| Check for Updates... | Check for new versions |
| About | Show version info |
| Exit | Close VoxTether |

---

## Stopping VoxTether

1. Right-click the tray icon
2. Select **Exit**

Or press `Ctrl+C` in the PowerShell window where it's running.

---

## Running Again Later

Each time you want to run VoxTether, you need to:

1. Open PowerShell
2. Navigate to the VoxTether folder
3. Activate the virtual environment
4. Run the app

**Quick commands:**
```powershell
cd C:\path\to\VoxTether
.\venv\Scripts\Activate.ps1
python -m src.main
```

---

## Updating VoxTether

If you cloned with Git:

```powershell
cd C:\path\to\VoxTether
git pull
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
python -m src.main
```

If you downloaded as ZIP, download the latest version and extract it over the existing folder.

---

## Troubleshooting

### "python is not recognized"

Python is not in your PATH. Either:
- Reinstall Python and check "Add Python to PATH"
- Use the full path to Python (e.g., `C:\Python311\python.exe`)

### "No module named src"

Make sure you're in the VoxTether root directory (where `src/` folder is located).

### "Script cannot be loaded because running scripts is disabled"

Run this in PowerShell:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Hotkey not working

1. Check if another app is using the same hotkey
2. Try running PowerShell as Administrator:
   ```powershell
   Start-Process powershell -Verb RunAs
   ```

### No audio recording

1. Check Windows Sound settings → Recording
2. Ensure your microphone is set as default
3. Use **Test Microphone** from the tray menu

### GPU not detected

1. Ensure NVIDIA drivers are up to date
2. Install CUDA packages:
   ```powershell
   pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
   ```
3. Run healthcheck:
   ```powershell
   python -m src.main --healthcheck
   ```

---

## Summary of Commands

```powershell
# One-time setup
git clone https://github.com/KennethHeine/VoxTether.git
cd VoxTether
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt

# Run the app
python -m src.main

# Run with debug logging
python -m src.main --debug

# Run healthcheck
python -m src.main --healthcheck
```

---

## See Also

- [README](../README.md) - Project overview
- [Installation Guide](INSTALLATION.md) - All installation options
- [Architecture](ARCHITECTURE.md) - Technical details
