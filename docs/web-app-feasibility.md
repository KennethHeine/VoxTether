# VoxTether Web App Feasibility Analysis

This document analyzes the feasibility of converting VoxTether from a Windows desktop application to a client-side web application while maintaining the same core features.

## Table of Contents

- [Executive Summary](#executive-summary)
- [Current VoxTether Features](#current-voxtether-features)
- [Web Implementation Options](#web-implementation-options)
- [Feature-by-Feature Analysis](#feature-by-feature-analysis)
- [Performance Comparison](#performance-comparison)
- [Available Models](#available-models)
- [GPU Acceleration in Browser](#gpu-acceleration-in-browser)
- [Architecture Recommendations](#architecture-recommendations)
- [Conclusion](#conclusion)
- [References](#references)

---

## Executive Summary

**Can VoxTether be made into a client web app with all the same features?**

**Short Answer: Partially.** A web-based version can provide:
- ✅ Fully offline speech-to-text transcription
- ✅ Privacy-first local processing
- ✅ Cross-platform support (Windows, macOS, Linux)
- ⚠️ Modified push-to-talk (browser-based, not system-wide)
- ❌ No system-wide global hotkeys
- ❌ No automatic text injection into other applications
- ❌ No system tray functionality

The core transcription functionality can be replicated with good performance using WebAssembly or WebGPU-accelerated Whisper implementations. However, the system-level integration features (global hotkeys, text injection) cannot be achieved in a standard web browser due to security restrictions.

---

## Current VoxTether Features

| Feature | Description | Web Feasibility |
|---------|-------------|-----------------|
| Push-to-talk recording | Press and hold hotkey to record | ⚠️ Partial - browser only |
| Global hotkeys | System-wide keyboard shortcuts | ❌ Not possible |
| Fully offline transcription | Local whisper.cpp processing | ✅ Fully possible |
| Text insertion | Types transcribed text at cursor | ❌ Not possible |
| System tray | Background system tray icon | ❌ Not possible |
| GPU acceleration | CUDA for NVIDIA GPUs | ⚠️ WebGPU instead |
| Model selection | Multiple Whisper model sizes | ✅ Fully possible |
| Language support | Multi-language transcription | ✅ Fully possible |
| Start with Windows | Auto-start on login | ❌ Not possible |

---

## Web Implementation Options

### Option 1: whisper.cpp WebAssembly (WASM)

The official whisper.cpp project includes a WebAssembly port that runs entirely in the browser.

**Pros:**
- Direct port of the original whisper.cpp
- Same model files as desktop version
- Fully offline, all processing local
- No server required

**Cons:**
- Slower than native (2-3x real-time for tiny/base models)
- Limited to CPU (SIMD only, no GPU)
- Memory constraints (~2GB browser limit)
- Only practical for smaller models

**Demo:** https://ggml.ai/whisper.cpp/

### Option 2: Transformers.js with WebGPU

Hugging Face's Transformers.js provides Whisper models that can leverage WebGPU for GPU acceleration.

**Pros:**
- WebGPU enables GPU acceleration
- Near real-time transcription with GPU
- Modern JavaScript API
- Good browser support (Chrome, Edge, Safari)

**Cons:**
- WebGPU not available in all browsers
- Different model format (ONNX)
- Requires downloading models to browser cache

**Example:**
```javascript
import { pipeline } from '@xenova/transformers';

const transcriber = await pipeline(
  'automatic-speech-recognition',
  'Xenova/whisper-tiny.en',
  { device: 'webgpu' }  // Enable GPU acceleration
);

const result = await transcriber(audioBlob, {
  chunk_length_s: 30,
  stride_length_s: 5,
  return_timestamps: true
});
```

**Demo:** https://github.com/xenova/whisper-web

### Option 3: Progressive Web App (PWA)

A PWA combines web technologies with some native-like features.

**Pros:**
- Can work offline
- Installable on desktop
- Can request microphone permissions

**Cons:**
- Still bound by browser security restrictions
- No system-wide hotkeys
- No text injection capability

---

## Feature-by-Feature Analysis

### 1. Speech-to-Text Transcription ✅

**Fully achievable.** Browser-based Whisper implementations provide accurate transcription:

| Approach | Performance | GPU Support |
|----------|-------------|-------------|
| whisper.cpp WASM | 2-3x real-time (CPU) | ❌ No |
| Transformers.js WASM | Similar to whisper.cpp | ❌ No |
| Transformers.js WebGPU | Near real-time | ✅ Yes |

### 2. Push-to-Talk Recording ⚠️

**Partially achievable.** The browser can record audio when:
- The browser tab is focused
- User grants microphone permission

**Limitations:**
- Cannot detect key presses when browser is in background
- Cannot use system-wide hotkeys like Ctrl+Alt+Space
- Limited to in-browser button clicks or keyboard events while focused

**Workaround:** Use a "click to record" button or spacebar hold while browser is focused.

### 3. Global Hotkeys ❌

**Not achievable.** Browsers cannot:
- Detect keyboard events when not focused
- Register system-wide keyboard shortcuts
- Intercept OS-reserved key combinations

**Why:** Security and privacy restrictions prevent web pages from acting as keyloggers or interfering with system functionality.

### 4. Text Injection ❌

**Not achievable.** Browsers cannot:
- Type text into other applications
- Simulate keyboard input outside the browser
- Access or control other windows/applications

**Partial workaround:** Copy transcribed text to clipboard for manual pasting (Ctrl+V).

### 5. System Tray ❌

**Not achievable.** Web applications cannot:
- Create system tray icons
- Run in the background
- Display native notifications while minimized

**Partial workaround:** PWA can show browser notifications and run as a standalone window.

### 6. Privacy & Offline Mode ✅

**Fully achievable.** Browser-based Whisper:
- Processes all audio locally
- Never sends data to servers
- Works without internet (after initial model download)
- Models cached in browser storage

---

## Performance Comparison

### Transcription Speed (10 seconds of audio)

| Platform | Model | Backend | Time | Notes |
|----------|-------|---------|------|-------|
| Desktop (VoxTether) | base | CPU | ~2-3s | Native C++ |
| Desktop (VoxTether) | base | CUDA | <1s | GPU accelerated |
| Browser | base | WASM | ~5-8s | SIMD only |
| Browser | base | WebGPU | ~1-2s | GPU accelerated |
| Desktop (VoxTether) | tiny | CPU | ~1s | Native C++ |
| Browser | tiny | WASM | ~3-4s | SIMD only |
| Browser | tiny | WebGPU | <1s | GPU accelerated |

### Memory Usage

| Platform | tiny Model | base Model | small Model |
|----------|-----------|-----------|-------------|
| Desktop | ~150 MB | ~300 MB | ~600 MB |
| Browser (WASM) | ~200 MB | ~400 MB | Limited |
| Browser (WebGPU) | ~200 MB | ~400 MB | ~700 MB |

**Note:** Browsers have memory limits (~2-4 GB depending on browser). Models larger than "small" may not work reliably.

---

## Available Models

### Compatible Models for Browser

| Model | Size | Quality | Browser Recommended |
|-------|------|---------|---------------------|
| whisper-tiny | ~75 MB | Basic | ✅ Yes - Fast |
| whisper-tiny.en | ~75 MB | Good (English only) | ✅ Yes - Fastest |
| whisper-base | ~142 MB | Good | ✅ Yes |
| whisper-base.en | ~142 MB | Better (English only) | ✅ Yes |
| whisper-small | ~466 MB | Better | ⚠️ Marginal |
| whisper-small.en | ~466 MB | Great (English only) | ⚠️ Marginal |
| whisper-medium | ~1.5 GB | Great | ❌ Too large |
| whisper-large-v3 | ~3 GB | Best | ❌ Too large |

### Model Sources

**For whisper.cpp WASM:**
- Original ggml format models from: https://huggingface.co/ggerganov/whisper.cpp

**For Transformers.js:**
- ONNX format models from: https://huggingface.co/Xenova
  - `Xenova/whisper-tiny`
  - `Xenova/whisper-tiny.en`
  - `Xenova/whisper-base`
  - `Xenova/whisper-base.en`
  - `Xenova/whisper-small`
  - `Xenova/whisper-small.en`

---

## GPU Acceleration in Browser

### WebGPU Overview

WebGPU is a modern graphics and compute API that enables GPU acceleration for machine learning in browsers. It replaces WebGL for compute workloads with much better performance.

### Browser Support

| Browser | WebGPU Support |
|---------|----------------|
| Chrome 113+ | ✅ Full support |
| Edge 113+ | ✅ Full support |
| Safari 17+ | ✅ Full support |
| Firefox | 🔄 Experimental (behind flag) |

**Note:** Browser support evolves rapidly. Check [caniuse.com/webgpu](https://caniuse.com/webgpu) for current coverage. Chrome, Edge, and Safari account for the majority of desktop browser usage, making WebGPU broadly available, though Firefox users may need to enable experimental flags.

### Performance with WebGPU

- Up to **80% of native GPU performance**
- **10x faster** than WASM-only approaches
- Near real-time transcription for smaller models
- Significant improvement for base and small models

### Enabling WebGPU in Transformers.js

```javascript
const transcriber = await pipeline(
  'automatic-speech-recognition',
  'Xenova/whisper-base.en',
  { 
    device: 'webgpu',  // Use GPU
    // Fallback to WASM if WebGPU unavailable
  }
);
```

### GPU Memory Considerations

- Browser GPU memory is shared with the operating system
- Large models may cause out-of-memory errors
- Recommend testing with target hardware before deployment

---

## Architecture Recommendations

### Recommended: Hybrid PWA + Browser Extension

To maximize features while working within browser limitations:

```
┌─────────────────────────────────────────────────────────────┐
│                    VoxTether Web PWA                        │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │   Audio      │  │  Whisper     │  │   Clipboard      │   │
│  │   Recorder   │  │  WebGPU      │  │   Copy           │   │
│  │   (MediaAPI) │  │  Engine      │  │   (Clipboard API)│   │
│  └──────────────┘  └──────────────┘  └──────────────────┘   │
└─────────────────────────────────────────────────────────────┘

Optional Browser Extension (for enhanced features):
┌─────────────────────────────────────────────────────────────┐
│  Browser Extension                                          │
│  • Background hotkey detection (within browser)             │
│  • Text paste into active tab                               │
│  • Cross-tab communication                                  │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

```
Frontend:
- React or Vue.js for UI
- Transformers.js for Whisper inference
- Web Audio API for recording
- IndexedDB for model caching

Build Tools:
- Vite or webpack
- TypeScript

Deployment:
- Static hosting (GitHub Pages, Netlify, Vercel)
- Service Worker for offline support
```

### Alternative: Electron App

For full feature parity, consider Electron instead of a pure web app:

**Pros:**
- All desktop VoxTether features possible
- System-wide hotkeys via `globalShortcut`
- System tray with `Tray` API
- Native keyboard simulation
- GPU acceleration via native whisper.cpp

**Cons:**
- Not a "web app" - requires installation
- Larger bundle size
- Separate builds for Windows/macOS/Linux

---

## Conclusion

### Summary Table

| Feature | Desktop VoxTether | Web App | Electron |
|---------|-------------------|---------|----------|
| Offline transcription | ✅ | ✅ | ✅ |
| GPU acceleration | ✅ CUDA | ✅ WebGPU | ✅ CUDA/Metal |
| Large models (medium+) | ✅ | ❌ | ✅ |
| System-wide hotkeys | ✅ | ❌ | ✅ |
| Text injection | ✅ | ❌ | ✅ |
| System tray | ✅ | ❌ | ✅ |
| Cross-platform | Windows only | All browsers | Windows/macOS/Linux |
| No installation | ❌ | ✅ | ❌ |

### Recommendation

1. **For web-first, privacy-focused use case:** Build a PWA with Transformers.js + WebGPU. Users can manually copy/paste transcribed text.

2. **For full feature parity:** Consider an Electron-based cross-platform app instead of a pure web app.

3. **Hybrid approach:** Offer both - a web version for quick access and a desktop app for power users who need system-wide hotkeys and text injection.

### Minimum Viable Web App

A practical web version would include:
- Click-to-record button (spacebar while focused)
- WebGPU-accelerated Whisper (tiny or base model)
- Copy to clipboard button
- Model download/caching
- Works offline after first load

This would provide ~80% of VoxTether's core value (offline transcription) while sacrificing the system integration features.

---

## References

### Official Resources
- [whisper.cpp WASM Demo](https://ggml.ai/whisper.cpp/)
- [whisper.cpp WASM Source](https://github.com/ggml-org/whisper.cpp/tree/master/examples/whisper.wasm)
- [Transformers.js Documentation](https://huggingface.co/docs/transformers.js)
- [Transformers.js WebGPU Guide](https://huggingface.co/docs/transformers.js/en/guides/webgpu)

### Example Projects
- [Whisper Web (Xenova)](https://github.com/xenova/whisper-web) - Complete browser-based transcription app
- [whisper.wasm](https://github.com/timur00kh/whisper.wasm) - TypeScript wrapper with React components

*Note: External project links may change over time. If links are broken, search for "Whisper WebGPU browser" or "Transformers.js Whisper" for current alternatives.*

### Technical Documentation
- [WebGPU Specification](https://www.w3.org/TR/webgpu/)
- [Web Audio API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API)
- [Clipboard API](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard_API)
- [ONNX Runtime Web](https://onnxruntime.ai/docs/tutorials/web/)

### Performance Benchmarks
- [WebGPU for ML Workloads](https://blog.logrocket.com/webgpu-accelerate-ml-workloads-browser/)
- [ONNX Runtime Web + WebGPU](https://opensource.microsoft.com/blog/2024/02/29/onnx-runtime-web-unleashes-generative-ai-in-the-browser-using-webgpu)
