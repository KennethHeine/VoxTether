# Model & Backend Alternatives Research

**Created:** January 27, 2026  
**Last Updated:** January 27, 2026  
**Status:** Active Research  
**Goal:** Find better transcription solutions that work reliably with NVIDIA GPUs

---

## Current Setup

| Component | Details |
|-----------|---------|
| **GPU** | NVIDIA GeForce RTX 4070 Laptop GPU (Compute 8.9) |
| **Driver** | 573.09 (supports CUDA 12.8) |
| **Current Model** | `ggml-small.en.bin` (~466 MB) |
| **Current Backend** | ✅ **CUDA 12.4 (FIXED!)** |
| **CUDA Status** | ✅ **Self-contained build - no toolkit needed!** |

---

## Problem Summary (RESOLVED)

~~The CUDA backend crashes with `STATUS_STACK_BUFFER_OVERRUN` because:~~
~~- Pre-built whisper.cpp CUDA 11.8 binaries require matching ABI~~
~~- The `ggml-cuda.dll` expects different cuBLAS ABI than installed~~

**Solution:** Switch to CUDA 12.4 build which is **self-contained** (bundles all DLLs).

Your driver (573.09) supports CUDA 12.8, which is backwards compatible with 12.4.
No separate CUDA Toolkit installation needed!

See [cuda-investigation-report.md](cuda-investigation-report.md) for the original investigation.

---

## Alternative Models

### 1. Whisper Large-v3-turbo ⭐ Recommended

**Already available in VoxTether's ModelCatalog!**

| Property | Value |
|----------|-------|
| File | `ggml-large-v3-turbo.bin` |
| Size | ~1.6 GB |
| Speed | 6.3x faster than large-v3 |
| Accuracy | Near large-v3 quality |
| GPU Optimized | Yes (pruned decoder layers) |

**Architecture:** Same as large-v3, but decoder layers reduced from 32 to 4.

**Benchmark (from OpenAI):**
| Model | Parameters | Speed vs large-v3 | Sequential WER | Chunked WER |
|-------|------------|-------------------|----------------|-------------|
| large-v3 | 1550M | 1.0x | 10.0% | 11.0% |
| large-v3-turbo | 809M | 6.3x | 10.8% | 10.9% |

**Status:** ✅ Already in ModelCatalog, ready to use

---

### 2. Distil-Whisper (distil-large-v3) ⭐ Recommended

**Knowledge-distilled version of Whisper large-v3**

| Property | Value |
|----------|-------|
| File | `ggml-distil-large-v3.bin` |
| Size | ~756 MB |
| Speed | 6x faster than large-v3 |
| Accuracy | Within 1% WER of large-v3 |
| Optimized For | Sequential long-form (what VoxTether uses) |

**Key Benefits:**
- Specifically designed for sequential long-form transcription algorithm
- Compatible with whisper.cpp, faster-whisper, and OpenAI Whisper
- Half the size of large-v3 with nearly identical accuracy
- Lower hallucination rates than original Whisper

**Download URL:**
```
https://huggingface.co/distil-whisper/distil-large-v3-ggml/resolve/main/ggml-distil-large-v3.bin
```

**Status:** ⚠️ Not in ModelCatalog yet - needs to be added

---

### 3. Quantized Models

Quantized versions (Q5, Q8) reduce model size and can improve speed with minimal accuracy loss.

| Model | Standard Size | Quantized Size | Notes |
|-------|--------------|----------------|-------|
| large-v3-turbo | 1.6 GB | 547 MB (Q5) | Good balance |
| medium | 1.5 GB | 514 MB (Q5) | Recommended for CPU |
| small | 466 MB | 181 MB (Q5) | Fast, decent accuracy |

**Status:** ✅ Already in ModelCatalog

---

## Alternative Backends

### 1. Faster-Whisper (CTranslate2)

**Python-based reimplementation using CTranslate2 inference engine**

| Feature | whisper.cpp | faster-whisper |
|---------|-------------|----------------|
| Language | C++ | Python |
| Engine | ggml | CTranslate2 |
| CUDA Support | Requires ABI match | cuDNN 9 + CUDA 12 |
| Speed | Fast | 4x faster than OpenAI |
| Batched Inference | No | Yes |
| Memory | Low | Lower with int8 |

**Benchmark (large-v2 on RTX 3070 Ti):**
| Implementation | Precision | Beam | Time (13 min audio) | VRAM |
|----------------|-----------|------|---------------------|------|
| openai/whisper | fp16 | 5 | 2m23s | 4708MB |
| whisper.cpp (FA) | fp16 | 5 | 1m05s | 4127MB |
| faster-whisper | fp16 | 5 | 1m03s | 4525MB |
| faster-whisper (batch=8) | fp16 | 5 | **17s** | 6090MB |
| faster-whisper | int8 | 5 | 59s | 2926MB |

**CUDA Requirements:**
- CUDA 12 + cuDNN 9 (recommended)
- Or CUDA 11 + cuDNN 8 with ctranslate2==3.24.0
- Or CUDA 12 + cuDNN 8 with ctranslate2==4.4.0

**Integration Complexity:** 🔴 High
- Requires Python subprocess or native bindings
- Significant rewrite of `WhisperCppEngine`
- Would need new backend abstraction

**Status:** 📋 Research phase - not implemented

---

### 2. Whisper.cpp with Custom Build

**Build whisper.cpp from source with your CUDA toolkit**

**Requirements:**
- Visual Studio 2022 with C++ workload
- CMake 3.20+
- CUDA Toolkit 11.8 (already installed)

**Build Commands:**
```bash
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
cmake -B build -DGGML_CUDA=ON -DCMAKE_CUDA_COMPILER="C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v11.8/bin/nvcc.exe"
cmake --build build --config Release
```

**Benefits:**
- Guaranteed ABI compatibility
- Can optimize for your GPU (sm_89 for RTX 4070)
- No code changes to VoxTether needed

**Status:** 📋 Not attempted yet

---

### 3. Try Older Whisper.cpp Releases

Different whisper.cpp versions have different CUDA compatibility:

| Version | CUDA Build | Notes |
|---------|------------|-------|
| v1.8.3 | ❌ Crashes | Current version |
| v1.7.x | ❓ Unknown | Worth testing |
| v1.6.x | ❓ Unknown | Worth testing |

**Status:** 📋 Not tested yet

---

## Action Items

### Quick Wins (No Code Changes)
- [ ] Try `large-v3-turbo` model with CPU backend
- [ ] Try quantized models (Q5 versions) for faster CPU inference

### Minor Code Changes
- [ ] Add `distil-large-v3` to ModelCatalog
- [ ] Test performance comparison between models

### Medium Effort
- [ ] Build whisper.cpp from source with CUDA 11.8
- [ ] Test older whisper.cpp releases for CUDA compatibility

### Major Changes (Future Consideration)
- [ ] Evaluate faster-whisper as alternative backend
- [ ] Design backend abstraction layer for multiple engines

---

## Performance Comparison Matrix

| Model | Size | CPU Time* | GPU Time* | Accuracy | Notes |
|-------|------|-----------|-----------|----------|-------|
| tiny.en | 75 MB | ~1s | - | Basic | Very fast, low accuracy |
| base.en | 142 MB | ~2s | - | Good | Default choice |
| small.en | 466 MB | ~5-6s | - | Better | Current model |
| medium.en | 1.5 GB | ~15s | - | Great | Good for accuracy |
| large-v3 | 3.1 GB | ~30s+ | ~3s | Best | Slow on CPU |
| large-v3-turbo | 1.6 GB | ~10s | ~1.5s | Near-best | 🎯 Best GPU choice |
| distil-large-v3 | 756 MB | ~8s | ~1s | Near-best | 🎯 Best balance |

*Estimated times for ~8 second audio on your system

---

## References

- [whisper.cpp releases](https://github.com/ggerganov/whisper.cpp/releases)
- [faster-whisper GitHub](https://github.com/SYSTRAN/faster-whisper)
- [distil-whisper on HuggingFace](https://huggingface.co/distil-whisper/distil-large-v3)
- [whisper-large-v3-turbo on HuggingFace](https://huggingface.co/openai/whisper-large-v3-turbo)
- [whisper.cpp GGML models](https://huggingface.co/ggerganov/whisper.cpp)
- [VoxTether CUDA Investigation Report](cuda-investigation-report.md)

---

## Changelog

### January 27, 2026 (Update 2) - SOLUTION IMPLEMENTED! 🎉
**VoxTether now uses CUDA 12.4 backend instead of CUDA 11.8!**

The key discovery: whisper.cpp v1.8.3 provides TWO CUDA builds:
- `whisper-cublas-11.8.0-bin-x64.zip` (62 MB) - requires separate CUDA Toolkit
- `whisper-cublas-12.4.0-bin-x64.zip` (460 MB) - **SELF-CONTAINED with all DLLs**

Your driver 573.09 supports CUDA 12.8, which is backwards compatible with 12.4.

**Changes made:**
- Updated `BackendDownloadService.cs` to download CUDA 12.4 build
- Updated `BackendSelectionService.cs` to check for CUDA 12 DLLs
- Updated tests for CUDA 12 DLL names
- No separate CUDA Toolkit installation needed!

**To test:**
1. Open VoxTether Settings → Performance
2. Click "Download CUDA Backend" (will download ~460 MB)
3. Set Backend to "CUDA" 
4. Test transcription - should now work on GPU!

### January 27, 2026
- Initial research document created
- Documented current setup and CUDA issue
- Researched large-v3-turbo, distil-large-v3 models
- Evaluated faster-whisper as alternative backend
- Created action items and performance matrix
