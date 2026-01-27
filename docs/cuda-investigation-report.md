# CUDA GPU Investigation Report

**Date:** January 27, 2026  
**System:** Kenneth's Laptop  
**Issue:** CUDA transcription fails with STATUS_STACK_BUFFER_OVERRUN  
**Updated:** January 27, 2026 (Additional diagnostics with voxtether-diag tool)

---

## System Configuration

| Component | Details |
|-----------|---------|
| **GPU** | NVIDIA GeForce RTX 4070 Laptop GPU |
| **Compute Capability** | 8.9 |
| **GPU Memory** | 8188 MiB |
| **NVIDIA Driver** | 573.09 |
| **CUDA Toolkit** | 11.8.0 (installed at `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8`) |
| **VoxTether Version** | 0.0.26 |
| **whisper.cpp Version** | v1.8.3 |

---

## Updated Findings (with voxtether-diag tool)

### ✅ What Works

1. **GPU Detection** - VoxTether correctly detects the NVIDIA GPU
2. **CUDA DLLs in PATH** - System CUDA 11.8 DLLs are properly in PATH
3. **whisper-cli.exe --help** - The CUDA executable runs and shows GPU info:
   ```
   ggml_cuda_init: found 1 CUDA devices:
     Device 0: NVIDIA GeForce RTX 4070 Laptop GPU, compute capability 8.9, VMM: yes
   ```
4. **CPU Transcription** - Works perfectly, ~5-6 seconds for 8-second audio with small.en model

### ❌ What Fails

1. **CUDA Transcription** - Crashes during model loading with exit code `-1073740791` (0xC0000409 = STATUS_STACK_BUFFER_OVERRUN)
2. **--no-gpu flag** - Also crashes! This confirms the issue is in the model loading code path, not GPU-specific

### Key Discovery

The crash occurs **even with the --no-gpu flag**, indicating the issue is not purely about CUDA execution but about the `ggml-cuda.dll` binary itself. The DLL was compiled against a specific CUDA Toolkit patch version that has ABI incompatibility with your installed CUDA 11.8.0.

### DLL Version Analysis

| DLL | System (CUDA 11.8.0) | Required by whisper.cpp |
|-----|---------------------|-------------------------|
| cublas64_11.dll | 6.14.11.11113 (11.11.3) | Unknown specific patch |
| cublasLt64_11.dll | 6.14.11.11113 | Unknown specific patch |
| cudart64_110.dll | 6.14.11.11080 | 6.14.11.11080 (bundled) |

**Note:** The whisper.cpp release (v1.8.3) does NOT bundle cuBLAS DLLs - only cudart and nvrtc. The `ggml-cuda.dll` links against cuBLAS at compile time, so it expects the exact ABI from whichever CUDA Toolkit was used to build it.

---

## Current State

| Backend | Status | Notes |
|---------|--------|-------|
| **CPU** | ✅ Working | 5-6 sec transcription time for ~8 sec audio |
| **CUDA** | ❌ Crashing | STATUS_STACK_BUFFER_OVERRUN during model load |

VoxTether is currently configured to use **CPU mode** (as set in settings after the CUDA failures).

---

## Next Steps (Options)

### Option 1: Continue Using CPU Mode (Easiest)
**Effort:** None  
**Status:** Already working

CPU mode works reliably. With the `small.en` model on your system, transcription takes ~5-6 seconds which is acceptable for most use cases.

**Pros:**
- No additional work needed
- Stable and reliable
- No CUDA dependency issues

**Cons:**
- Slower than GPU (but still fast enough for real-time use)

---

### Option 2: Build whisper.cpp from Source (Technical)
**Effort:** Medium-High  
**Expected Result:** Native CUDA build optimized for your system

Build whisper.cpp yourself using your installed CUDA 11.8 toolkit:

```bash
# Clone whisper.cpp
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp

# Build with CUDA support
cmake -B build -DGGML_CUDA=ON -DCMAKE_CUDA_COMPILER="C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v11.8/bin/nvcc.exe"
cmake --build build --config Release
```

**Requirements:**
- Visual Studio 2022 with C++ workload
- CMake 3.20+
- CUDA Toolkit 11.8 (already installed)

**Pros:**
- Guaranteed compatibility with your CUDA toolkit
- Potentially faster than pre-built binaries
- Can optimize for your specific GPU (sm_89 for RTX 4070)

**Cons:**
- Requires development tools setup
- Build process can be finicky
- Need to maintain your own builds

---

### Option 3: Try Different whisper.cpp Release Versions
**Effort:** Low-Medium  
**Expected Result:** Find a compatible pre-built binary

The whisper.cpp CUDA builds vary between releases. Try downloading different release versions:

1. Go to [whisper.cpp releases](https://github.com/ggerganov/whisper.cpp/releases)
2. Download older `whisper-cublas-*.zip` versions
3. Extract to `%LOCALAPPDATA%\VoxTether\whisper\cuda\Release\`
4. Test each version

**Versions to try:**
- v1.7.x series
- v1.6.x series

**Pros:**
- Quick to test
- No build tools required

**Cons:**
- Trial and error
- Older versions may lack features/fixes

---

### Option 4: Use CUDA Toolkit 12.x with CUDA 12 whisper.cpp Build
**Effort:** Medium  
**Expected Result:** Modern CUDA stack

Some whisper.cpp releases include CUDA 12 builds. You could:
1. Install CUDA Toolkit 12.x alongside 11.8
2. Use a CUDA 12 whisper.cpp build

**Note:** This requires VoxTether code changes to look for CUDA 12 DLLs (`cublas64_12.dll` etc.)

---

## Diagnostic Tool

VoxTether now includes a CLI diagnostic tool for troubleshooting CUDA issues:

```bash
# Navigate to the built diagnostics folder
cd src\VoxTether.Diagnostics\bin\Release\net8.0-windows

# Show system info
voxtether-diag.exe info

# Check CUDA DLLs and paths
voxtether-diag.exe dlls
voxtether-diag.exe dll-versions

# Test CPU transcription (should work)
voxtether-diag.exe cpu

# Test CUDA transcription (currently crashes)
voxtether-diag.exe cuda

# Full validation
voxtether-diag.exe check
```

---

## Recommended Action

**For now:** Continue using CPU mode. It's working and provides acceptable performance (~5-6 seconds for 8-second audio with small.en model).

**For future improvement:** Consider **Option 2 (build from source)** if you want GPU acceleration. This is the most reliable solution because:
1. It guarantees ABI compatibility with your installed CUDA 11.8.0 toolkit
2. You can optimize for your specific GPU architecture (sm_89 for RTX 4070)
3. The pre-built whisper.cpp releases have inconsistent CUDA compatibility

**Why pre-built binaries fail:** The whisper.cpp releases compile `ggml-cuda.dll` against a specific CUDA Toolkit patch version but do NOT bundle the matching cuBLAS DLLs. Your system's cuBLAS 11.11.3 (from CUDA 11.8.0) has a different ABI than what was used to compile the release.

---

## Technical Details

### Error Codes Reference
| Exit Code | Hex | Windows Status | Meaning |
|-----------|-----|----------------|---------|
| -1073740791 | 0xC0000409 | STATUS_STACK_BUFFER_OVERRUN | Stack buffer overflow detected (security check failed) |
| -1073741515 | 0xC0000135 | STATUS_DLL_NOT_FOUND | Required DLL not found |

### File Locations
| Path | Contents |
|------|----------|
| `%LOCALAPPDATA%\VoxTether\whisper\` | Whisper executables |
| `%LOCALAPPDATA%\VoxTether\whisper\cuda\Release\` | CUDA backend |
| `%APPDATA%\VoxTether\models\` | Whisper models |
| `%APPDATA%\VoxTether\logs\` | Application logs |
| `%APPDATA%\VoxTether\settings.json` | User settings |

### Current Settings
```json
{
  "transcriptionBackend": 1,  // 0=Auto, 1=CpuOnly, 2=Cuda
  "modelPath": "ggml-small.en.bin"
}
```
