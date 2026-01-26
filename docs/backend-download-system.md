# Backend Download System

VoxTether uses a backend distribution system that ships with the CPU backend by default and offers to download the NVIDIA CUDA backend on-demand based on detected client hardware.

## Overview

The backend download system allows users to:
- Use VoxTether immediately with the included CPU backend
- Download the NVIDIA CUDA backend when an NVIDIA GPU is detected
- Manage installed backends to save disk space
- Use VoxTether offline after initial setup

## Available Backends

### CPU Only (Default)
- **Always included** with VoxTether installation
- Works on any Windows 10/11 system
- No additional downloads required
- Suitable for testing and systems without GPU acceleration

### NVIDIA CUDA (Downloadable)
- **Recommended for:** Systems with NVIDIA graphics cards
- **Requirements:** 
  - NVIDIA GPU with CUDA support
  - **CUDA 11.8 runtime DLLs** (cublas64_11.dll, cudart64_110.dll) - can be downloaded via the Settings UI
  - Up-to-date NVIDIA drivers
- **Download size:** ~60 MB (backend) + ~403 MB (CUDA DLLs if needed)
- **Performance:** Fastest option for NVIDIA GPUs
- **Source:** Pre-built binaries from [ggml-org/whisper.cpp](https://github.com/ggml-org/whisper.cpp/releases)

> **Note:** The CUDA backend requires CUDA 11.8 runtime DLLs. VoxTether can download these automatically via Settings → Performance → "Get CUDA DLLs" button. Alternatively, you can install CUDA Toolkit 11.8 manually. See [cuda-troubleshooting.md](cuda-troubleshooting.md) for detailed setup instructions.

## How It Works

### First-Run Experience

When you launch VoxTether for the first time:

1. The application detects your hardware (GPU, CPU)
2. If an NVIDIA GPU is detected, VoxTether recommends the CUDA backend
3. You can choose to:
   - Download the recommended backend
   - Skip and use CPU-only mode
   - Open settings to download later

### Hardware Detection

VoxTether detects NVIDIA GPU hardware by checking for:
- NVIDIA GPU: Presence of NVIDIA driver files and libraries (nvcuda.dll, nvapi64.dll)

### Backend Download Process

When you choose to download a backend:

1. **Download**: The backend package (zip file) is downloaded from GitHub releases
2. **Validation**: SHA-256 checksum is verified to ensure integrity
3. **Extraction**: The package is extracted to `whisper/<backend>/` folder
4. **Verification**: The system checks that backend executables are present
5. **Activation**: The backend becomes available for use

All operations show progress feedback in the UI.

## Managing Backends

### Via Settings Window

In the VoxTether Settings window, you can:
- View available backends and their status
- Download the CUDA backend
- Remove installed backends to free disk space
- See download size and system requirements

### Via File System

Backends are stored in:
```
<VoxTether Install Dir>\whisper\<backend>\
```

For example:
- `whisper\` - CPU backend (always present, main.exe at root)
- `whisper\cuda\` - NVIDIA CUDA backend (if downloaded)

## Manual Backend Installation

For offline scenarios or custom deployments:

### Step 1: Obtain Backend Package

**For CUDA:**
Download the pre-built binary from:
- [ggml-org/whisper.cpp releases](https://github.com/ggml-org/whisper.cpp/releases) - Look for `whisper-cublas-*.zip`

### Step 2: Extract to Whisper Folder

1. Navigate to your VoxTether installation directory
2. Open the `whisper` folder
3. Create a `cuda` subfolder
4. Extract the compiled binary and its dependencies into that subfolder

Example structure:
```
VoxTether\
  whisper\
    main.exe
    (CPU backend files)
    cuda\
      main.exe
      cudnn_ops_infer64_8.dll
      (other CUDA files)
```

### Step 3: Verify Installation

1. Launch VoxTether
2. Right-click the tray icon and select "About"
3. Check that the CUDA backend is listed as available

## Hosting Custom Backend Downloads

For enterprise deployments, you can host your own backend packages:

### Step 1: Create Backend Manifest

Create a JSON file describing your backends:

```json
{
  "version": "1.0",
  "backends": [
    {
      "id": "cuda",
      "name": "NVIDIA CUDA",
      "description": "GPU acceleration for NVIDIA graphics cards",
      "downloadUrl": "https://your-server.com/backends/whisper-cuda.zip",
      "size": 61582231,
      "checksum": "sha256:abc123...",
      "requirements": "NVIDIA GPU with CUDA support and up-to-date drivers"
    }
  ]
}
```

### Step 2: Host Backend Packages

Host the backend zip files at the URLs specified in your manifest.

### Step 3: Configure VoxTether

Currently, VoxTether uses an embedded manifest. To use a custom manifest, you would need to modify the `BackendDownloadService` to fetch from your URL.

**Note:** Custom manifest URL support is planned for a future release.

## Backend Package Format

Backend packages are ZIP files containing:

- `main.exe` or `whisper.exe` - The whisper.cpp executable
- Required DLLs (e.g., CUDA libraries)
- Any additional runtime dependencies

The package structure should match:
```
whisper-cuda.zip
  main.exe
  cudnn_ops_infer64_8.dll
  cudnn_cnn_infer64_8.dll
  (other dependencies)
```

When extracted to `whisper/cuda/`, it becomes:
```
whisper/
  cuda/
    main.exe
    cudnn_ops_infer64_8.dll
    (other dependencies)
```

## Troubleshooting

### CUDA Backend Not Working (Missing DLLs)

If the CUDA backend is downloaded but VoxTether falls back to CPU mode, you likely need the CUDA 11.8 runtime DLLs.

**Easiest Solution - Use Built-in Download:**

1. Open VoxTether Settings (right-click tray icon → Settings)
2. Go to the **Performance** tab
3. Look for "Missing CUDA DLLs" status (shown in orange) under the NVIDIA CUDA backend
4. Click the **"Get CUDA DLLs"** button to download the required files (~403 MB)
5. Restart VoxTether

**Alternative Solutions:**

1. **Check logs**: Look for messages like "missing CUDA 11.8 runtime DLLs" or "cublas64_11.dll"
2. **Install CUDA Toolkit 11.8**: Download from [NVIDIA CUDA 11.8 Archive](https://developer.nvidia.com/cuda-11-8-0-download-archive)
3. **See detailed guide**: [cuda-troubleshooting.md](cuda-troubleshooting.md)

### Backend Download Fails

1. **Check internet connection**: Ensure you can reach GitHub
2. **Check disk space**: Verify you have enough free space (2x the download size)
3. **Check logs**: Open the logs folder from the tray menu to see detailed errors
4. **Try manual installation**: Download and extract the backend manually

### Backend Not Detected After Download

1. **Verify extraction**: Check that files were extracted to `whisper/cuda/`
2. **Check for main.exe**: Ensure `main.exe` or `whisper.exe` is present
3. **Restart VoxTether**: Close and relaunch the application
4. **Check logs**: Look for backend detection messages in the logs

### Downloaded Backend Not Performing Well

1. **Verify drivers**: Ensure NVIDIA GPU drivers are up-to-date
2. **Check requirements**: Confirm your hardware meets backend requirements
3. **Fall back to CPU**: CPU mode always works as a reliable fallback

### Checksum Validation Failed

1. **Retry download**: The download may have been corrupted
2. **Check network**: Unstable connections can corrupt downloads
3. **Manual download**: Download the package separately and verify manually
4. **Contact support**: If issue persists, report to VoxTether developers

## Technical Details

### Download Service Architecture

The backend download system consists of:

- **`IBackendDownloadService`**: Interface for backend management operations
- **`BackendDownloadService`**: Implementation handling downloads, validation, extraction
- **`BackendManifest`**: Model describing available backends
- **`BackendDownloadProgress`**: Progress reporting for UI feedback

### Security Features

- **SHA-256 Checksums**: All downloads are verified for integrity
- **HTTPS Downloads**: Packages are downloaded over secure connections
- **Manifest Validation**: Backend manifests are validated before use
- **Sandboxed Extraction**: Packages are extracted to controlled locations

### Storage Locations

- **Backends**: `<InstallDir>\whisper\<backend>\`
- **Temp Downloads**: `%TEMP%\VoxTether\downloads\`
- **Settings**: `%APPDATA%\VoxTether\settings.json`
- **Logs**: `%APPDATA%\VoxTether\logs\`

## Future Enhancements

Planned improvements include:

- **Remote manifest URLs**: Support for custom backend repositories
- **Resume capability**: Resume interrupted downloads
- **Background downloads**: Download backends while using VoxTether
- **Auto-updates**: Automatically update backends when new versions are available
- **Bandwidth control**: Limit download speed for slower connections

## Support

For issues with the backend download system:

1. Check the logs in `%APPDATA%\VoxTether\logs\`
2. Review this documentation for troubleshooting steps
3. Report issues on GitHub: https://github.com/KennethHeine/VoxTether/issues
4. Include log files and system information when reporting issues
