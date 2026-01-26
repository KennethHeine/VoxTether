# CUDA GPU Acceleration Troubleshooting Guide

This document provides comprehensive information about CUDA GPU acceleration in VoxTether, including setup requirements, common issues, and troubleshooting steps.

## Overview

VoxTether uses [whisper.cpp](https://github.com/ggml-org/whisper.cpp) for speech-to-text transcription. The CUDA backend provides GPU acceleration for NVIDIA graphics cards, offering significantly faster transcription compared to CPU-only mode.

## CUDA Requirements

### Hardware Requirements

- **NVIDIA GPU** with CUDA Compute Capability 3.5 or higher
- Most NVIDIA GPUs from 2013 onwards are supported (GeForce GTX 600 series and later)
- At least 2 GB of GPU memory recommended (more for larger models)

### Software Requirements

The whisper.cpp CUDA binaries (v1.8.3) require **CUDA 11.8 runtime** components:

| Required Component | DLL File | Purpose |
|-------------------|----------|---------|
| CUDA Runtime | `cudart64_110.dll` | Core CUDA runtime |
| cuBLAS | `cublas64_11.dll` | Linear algebra library |
| cuBLAS Lt | `cublasLt64_11.dll` | Lightweight cuBLAS interface |

> **Important**: The whisper.cpp CUDA binaries are built against CUDA 11.8. If you have CUDA 12.x installed, the DLL file names are different (`cublas64_12.dll` etc.) and won't work with the pre-built binaries.

## Installation Options

### Option 1: Use VoxTether Built-in Download (Easiest)

VoxTether can automatically download the required CUDA runtime DLLs for you:

1. Open VoxTether Settings (right-click tray icon → Settings)
2. Go to the **Performance** tab
3. Download the **NVIDIA CUDA** backend if not already installed
4. If the status shows **"Missing CUDA DLLs"** (in orange), click **"Get CUDA DLLs"**
5. Wait for the download to complete (~403 MB)
6. **Restart VoxTether** to use GPU acceleration

This is the easiest option as VoxTether handles all the file placement automatically.

### Option 2: Install CUDA Toolkit 11.8

This is a reliable alternative if you prefer having the full CUDA toolkit:

1. Download **CUDA Toolkit 11.8** from NVIDIA:
   - [CUDA Toolkit 11.8 Downloads](https://developer.nvidia.com/cuda-11-8-0-download-archive)
   
2. Run the installer and select at least:
   - CUDA Runtime
   - CUBLAS (cuBLAS runtime library)
   
3. After installation, verify the DLLs are in your system PATH:
   - Default location: `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\bin\`
   - This path should be automatically added to your system PATH

4. **Restart VoxTether** to pick up the new PATH

### Option 3: Manual Download from NVIDIA Redistribution Site

NVIDIA provides redistributable CUDA DLLs that can be downloaded separately (without installing the full toolkit):

1. Download the required packages from NVIDIA's redistribution site:
   - **CUDA Runtime** (~3MB): [cuda_cudart-windows-x86_64-11.8.89-archive.zip](https://developer.download.nvidia.com/compute/cuda/redist/cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-11.8.89-archive.zip)
   - **cuBLAS** (~400MB): [libcublas-windows-x86_64-11.11.3.6-archive.zip](https://developer.download.nvidia.com/compute/cuda/redist/libcublas/windows-x86_64/libcublas-windows-x86_64-11.11.3.6-archive.zip)

2. Extract the zip files and copy the DLLs from the `bin` folder to VoxTether's whisper directory:
   ```
   <VoxTether Install Dir>\whisper\cuda\Release\
   ```
   
   Required files:
   - From cuda_cudart: `cudart64_110.dll`
   - From libcublas: `cublas64_11.dll`, `cublasLt64_11.dll`

3. **Restart VoxTether**

> **Note**: These downloads are from NVIDIA's official redistribution site and are licensed for redistribution per NVIDIA's CUDA EULA.

### Option 4: Use CPU-Only Mode

If you cannot install CUDA or prefer not to use GPU acceleration:

1. Open VoxTether Settings (right-click tray icon → Settings)
2. Under "Transcription Backend", select **"CPU Only"**
3. Click Save

CPU mode works on any system without additional dependencies.

## Troubleshooting

### Error: "Process timed out without producing output"

**Symptoms:**
- Log shows: `Backend Cuda executable exists but cannot run: Process timed out without producing output`
- VoxTether falls back to CPU mode

**Cause:**
This typically means Windows is displaying a "missing DLL" error dialog that prevents the executable from producing output.

**Solution:**
1. Check if CUDA 11.8 is installed: Look for `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\`
2. Verify DLLs are in PATH: Open Command Prompt and run:
   ```cmd
   where cublas64_11.dll
   ```
3. If not found, install CUDA Toolkit 11.8 (see Installation Options above)

### Error: "Required DLL not found"

**Symptoms:**
- Log shows: `Backend Cuda executable exists but cannot run: Required DLL not found`
- Exit code `-1073741515` (0xC0000135 = STATUS_DLL_NOT_FOUND)

**Cause:**
The CUDA runtime DLLs are not installed or not in the system PATH.

**Solution:**
Same as above - install CUDA Toolkit 11.8 or place DLLs manually.

### Error: "CUDA runtime error" or Exit Code -1073740791 (0xC0000409)

**Symptoms:**
- Transcription fails immediately after recording stops
- Log shows: `Whisper transcription failed. Exit code: -1073740791`
- Log shows CUDA was initialized successfully: `ggml_cuda_init: found 1 CUDA devices`
- The crash happens during `whisper_init_from_file_with_params_no_state: loading model`

**Cause:**
This error (STATUS_STACK_BUFFER_OVERRUN) typically indicates a version mismatch between the CUDA DLLs on your system and what the whisper.cpp binary was compiled against. This can happen when:
- VoxTether's auto-downloaded CUDA DLLs are from a different cuBLAS patch version than what whisper.cpp was built with
- There are multiple CUDA installations with conflicting DLL versions in the system PATH

**Solutions:**

1. **Install the Full CUDA Toolkit 11.8.0** (Recommended)
   - Download from: https://developer.nvidia.com/cuda-11-8-0-download-archive
   - This ensures all DLLs are from the same build and are fully compatible
   - After installation, restart VoxTether

2. **Use CPU Backend** (Fallback)
   - Open Settings → Performance → Set backend to "CPU Only"
   - This bypasses all CUDA dependencies

3. **Clean Reinstall of CUDA DLLs**
   - Delete the folder: `%LOCALAPPDATA%\VoxTether\whisper\cuda\Release\`
   - Install the full CUDA Toolkit 11.8.0
   - Restart VoxTether

**Technical Details:**
The exit code `-1073740791` is the Windows NTSTATUS code `STATUS_STACK_BUFFER_OVERRUN` (0xC0000409). While this sounds like a security violation, in this context it typically indicates that a DLL function was called with parameters from an incompatible version, causing memory corruption detected by Windows' stack protection.

### CUDA 12.x Installed But CUDA 11.8 Required

**Symptoms:**
- You have CUDA 12.x installed but the CUDA backend doesn't work
- The DLL files exist but have different names (e.g., `cublas64_12.dll` instead of `cublas64_11.dll`)

**Cause:**
The whisper.cpp CUDA binaries are compiled against CUDA 11.8, which uses different DLL file names than CUDA 12.x.

**Solutions:**

1. **Install CUDA 11.8 alongside 12.x** (Recommended)
   - CUDA versions can coexist on the same system
   - Install CUDA 11.8 and add its bin directory to your PATH

2. **Rename DLLs** (Not recommended, may cause issues)
   - Copy `cublas64_12.dll` and rename to `cublas64_11.dll`
   - This is an unofficial workaround and may cause unexpected behavior

### NVIDIA GPU Not Detected

**Symptoms:**
- GPU diagnostics shows NVIDIA=False
- CUDA backend not recommended

**Cause:**
NVIDIA driver files not found in expected locations.

**Solution:**
1. Ensure NVIDIA drivers are installed:
   - Check for NVIDIA Control Panel or GeForce Experience
   - Run `nvidia-smi` in Command Prompt to verify driver
   
2. Update NVIDIA drivers:
   - [NVIDIA Driver Downloads](https://www.nvidia.com/Download/index.aspx)

### Backend Downloaded But Still Uses CPU

**Symptoms:**
- CUDA backend shows as "Installed" in Settings
- VoxTether still uses CPU mode

**Cause:**
The CUDA executable exists but fails runtime validation (usually due to missing DLLs).

**Solution:**
1. Check the VoxTether logs for specific error messages
2. Install CUDA Toolkit 11.8 as described above
3. Restart VoxTether

## Verifying CUDA Installation

### Check CUDA Toolkit Version

Open Command Prompt and run:
```cmd
nvcc --version
```

Example output:
```
nvcc: NVIDIA (R) Cuda compiler driver
Copyright (c) 2005-2022 NVIDIA Corporation
Built on Wed_Sep_21_10:41:10_Pacific_Daylight_Time_2022
Cuda compilation tools, release 11.8, V11.8.89
```

### Check CUDA DLLs in PATH

```cmd
where cublas64_11.dll
where cudart64_110.dll
```

### Check NVIDIA Driver

```cmd
nvidia-smi
```

This shows:
- GPU model and driver version
- CUDA version supported by the driver
- GPU memory usage

### Check VoxTether Logs

Logs are located at:
```
%APPDATA%\VoxTether\logs\
```

Look for messages from `BackendSelectionService` to diagnose backend selection issues.

## Performance Tips

### Model Selection

Smaller models transcribe faster on GPU:
- `tiny` / `tiny.en` - Fastest, lower accuracy
- `base` / `base.en` - Good balance
- `small` / `small.en` - Better accuracy (recommended)
- `medium` / `medium.en` - High accuracy, more GPU memory needed
- `large` - Highest accuracy, requires significant GPU memory

### GPU Memory

- Monitor GPU memory usage with `nvidia-smi`
- If you get out-of-memory errors, try a smaller model
- Close other GPU-intensive applications during transcription

## Known Issues

### whisper.cpp CUDA Binaries Require CUDA 11.8

The pre-built whisper.cpp CUDA binaries from [ggml-org/whisper.cpp releases](https://github.com/ggml-org/whisper.cpp/releases) are compiled against CUDA 11.8. This is a limitation of the upstream project, not VoxTether.

**Workarounds:**
1. Install CUDA Toolkit 11.8 (recommended)
2. Download CUDA DLLs from NVIDIA's redistribution site (see Option 2 above)
3. Build whisper.cpp from source with your CUDA version (advanced)
4. Use CPU mode as a reliable fallback

### Multiple CUDA Versions

If you have multiple CUDA versions installed, ensure CUDA 11.8 bin directory appears first in your PATH, or place the required DLLs directly in the whisper\cuda\Release\ folder.

## Automatic CUDA DLL Download

VoxTether can automatically download the required CUDA 11.8 runtime DLLs from NVIDIA's redistribution site. This eliminates the need for users to manually install the full CUDA Toolkit.

> **Important Compatibility Note:** The auto-downloaded DLLs are from NVIDIA's redistribution packages, which may be from a slightly different cuBLAS patch version than what the whisper.cpp binary was compiled against. In rare cases, this can cause a crash during transcription (exit code -1073740791). If you experience this issue, please install the full CUDA Toolkit 11.8.0 instead. See the troubleshooting section ["Error: CUDA runtime error or Exit Code -1073740791 (0xC0000409)"](#error-cuda-runtime-error-or-exit-code--1073740791-0xc0000409) for details.

### How to Download CUDA DLLs via Settings UI

The easiest way to get the required CUDA runtime DLLs is through the Settings window:

1. Open VoxTether Settings (right-click tray icon → Settings)
2. Go to the **Performance** tab
3. Under "Backend Management", you'll see the NVIDIA CUDA backend
4. If CUDA DLLs are missing, the status will show **"Missing CUDA DLLs"** in orange
5. Click the **"Get CUDA DLLs"** button to download the required files (~403 MB)
6. Wait for the download to complete
7. **Restart VoxTether** to use GPU acceleration

> **Note:** After downloading the CUDA backend, VoxTether will automatically detect if the runtime DLLs are missing and display the "Get CUDA DLLs" button.

### How It Works

When the CUDA backend is installed but the runtime DLLs are missing, VoxTether can download them directly from NVIDIA:
- **CUDA Runtime**: https://developer.download.nvidia.com/compute/cuda/redist/cuda_cudart/windows-x86_64/
- **cuBLAS**: https://developer.download.nvidia.com/compute/cuda/redist/libcublas/windows-x86_64/

The following DLLs are downloaded and placed in the `whisper\cuda\Release\` directory:
- `cudart64_110.dll` (CUDA Runtime)
- `cublas64_11.dll` (cuBLAS)
- `cublasLt64_11.dll` (cuBLAS Lightweight)

### API Usage (For Developers)

The automatic download is also available programmatically through the `IBackendDownloadService` interface:

```csharp
// Check if CUDA DLLs are already installed
if (!backendDownloadService.AreCudaDllsInstalled())
{
    // Download and install CUDA DLLs
    var success = await backendDownloadService.DownloadCudaDllsAsync(progress, cancellationToken);
}
```

### Size Considerations

- CUDA Runtime: ~3MB
- cuBLAS: ~400MB
- **Total download**: ~403MB

These files are licensed for redistribution per NVIDIA's CUDA EULA.

## Getting Help

1. **Check Logs**: Open `%APPDATA%\VoxTether\logs\` for detailed diagnostic information
2. **GitHub Issues**: Report issues at https://github.com/KennethHeine/VoxTether/issues
3. **Include Logs**: When reporting issues, include relevant log entries

## References

- [NVIDIA CUDA Toolkit 11.8 Download](https://developer.nvidia.com/cuda-11-8-0-download-archive)
- [NVIDIA CUDA Redistributable Packages](https://developer.download.nvidia.com/compute/cuda/redist/) - Official DLL downloads
- [NVIDIA CUDA EULA](https://docs.nvidia.com/cuda/eula/index.html) - Redistribution license terms
- [whisper.cpp GitHub Repository](https://github.com/ggml-org/whisper.cpp)
- [NVIDIA Driver Downloads](https://www.nvidia.com/Download/index.aspx)
- [VoxTether Backend Download System](backend-download-system.md)
