# VoxTether Architecture Consolidation Options

This document explores two architectural approaches for consolidating VoxTether into a single unified application, with a focus on LLM features and CUDA/GPU integration.

## Background

VoxTether currently has two separate implementations:
- **Python version** (`voxtether-python/`): Uses faster-whisper with native CUDA 12 support
- **.NET version** (`src/`): Uses whisper.cpp with CUDA 11.8 support (external process)

The user request is to consolidate into a **single application** that supports:
1. Speech-to-text transcription with GPU acceleration
2. LLM-based text post-processing (future feature)
3. CUDA integration for both Whisper and LLM inference

---

## Option 1: Pure Python Application

Consolidate into a single Python application, deprecating the .NET version.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     VoxTether (Pure Python)                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                      Python Application                            │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐ │ │
│  │  │    pystray   │  │   tkinter    │  │      keyboard            │ │ │
│  │  │ (system tray)│  │ (settings UI)│  │ (global hotkeys)         │ │ │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    Transcription Layer                             │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │  faster-whisper (CTranslate2)                                │ │ │
│  │  │  - Native CUDA 12 support                                    │ │ │
│  │  │  - HuggingFace model integration                             │ │ │
│  │  │  - Automatic GPU/CPU fallback                                │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                  LLM Post-Processing Layer                        │ │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐  │ │
│  │  │  llama-cpp-py   │  │    Ollama API   │  │   transformers   │  │ │
│  │  │ (local models)  │  │ (local server)  │  │  (HuggingFace)   │  │ │
│  │  └─────────────────┘  └─────────────────┘  └──────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                      CUDA / GPU Layer                              │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │  PyTorch / CTranslate2 (CUDA 12.x)                           │ │ │
│  │  │  - Unified GPU memory management                             │ │ │
│  │  │  - Single CUDA toolkit dependency                            │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Library | Purpose |
|-----------|---------|---------|
| **UI/Tray** | pystray, tkinter | System tray, settings dialogs |
| **Hotkeys** | keyboard | Global push-to-talk detection |
| **Audio** | sounddevice, soundfile | Microphone recording to WAV |
| **Transcription** | faster-whisper | Speech-to-text with CUDA 12 |
| **LLM** | llama-cpp-python OR Ollama | Local LLM inference for post-processing |
| **Text Injection** | pyperclip, keyboard | Clipboard paste or keyboard simulation |
| **Packaging** | PyInstaller | Single-file Windows executable |

### LLM Integration Options

#### Option 1A: llama-cpp-python (Embedded)

```python
from llama_cpp import Llama

class LLMPostProcessor:
    def __init__(self, model_path: str, n_gpu_layers: int = -1):
        self.llm = Llama(
            model_path=model_path,
            n_gpu_layers=n_gpu_layers,  # -1 = all layers on GPU
            n_ctx=2048,
        )
    
    def process(self, text: str) -> str:
        prompt = f"Correct grammar and punctuation: {text}"
        output = self.llm(prompt, max_tokens=512)
        return output["choices"][0]["text"]
```

**Pros:**
- Single process, no external dependencies
- Direct GPU memory control
- Works fully offline

**Cons:**
- Larger distribution size (~500MB+ with models)
- Must bundle GGUF model files

#### Option 1B: Ollama API (External Service)

```python
import requests

class OllamaPostProcessor:
    def __init__(self, model: str = "llama3", endpoint: str = "http://localhost:11434"):
        self.model = model
        self.endpoint = endpoint
    
    def process(self, text: str) -> str:
        response = requests.post(
            f"{self.endpoint}/api/generate",
            json={"model": self.model, "prompt": f"Correct: {text}"}
        )
        return response.json()["response"]
```

**Pros:**
- Simpler application code
- User manages models via Ollama
- Smaller distribution size

**Cons:**
- Requires Ollama to be installed separately
- External process dependency

### Advantages of Pure Python

| Advantage | Description |
|-----------|-------------|
| **Native CUDA 12** | faster-whisper/CTranslate2 has excellent RTX 40-series support |
| **Unified GPU Stack** | Single CUDA version for both Whisper and LLM |
| **Rich ML Ecosystem** | Direct access to HuggingFace, PyTorch, transformers |
| **Simpler Architecture** | No interop complexity, single language |
| **8-12x Faster** | faster-whisper benchmarks show 8-12x speed improvement over whisper.cpp |
| **Lower VRAM** | CTranslate2 uses ~4.7GB vs whisper.cpp for large-v2 model |

### Disadvantages of Pure Python

| Disadvantage | Description |
|--------------|-------------|
| **Distribution Size** | PyInstaller bundles can be 200-500MB+ |
| **Startup Time** | Python/PyInstaller apps have slower cold start |
| **UI Limitations** | tkinter is basic compared to WPF |
| **Windows Integration** | Less native feel than .NET WPF |

### Implementation Effort

**Estimated Effort: 2-3 weeks**

The Python version already exists in `voxtether-python/`. To consolidate:

1. ✅ Core transcription engine (already done)
2. ✅ System tray and hotkeys (already done)
3. ✅ Settings management (already done)
4. ⬜ Add LLM post-processing module (new)
5. ⬜ Add LLM settings UI (new)
6. ⬜ Update packaging for LLM dependencies (update)
7. ⬜ Remove/archive .NET version (cleanup)

---

## Option 2: C# Application Embedding Python

Keep the C#/.NET application as the primary UI, but embed Python for ML/CUDA workloads.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     VoxTether (C# + Embedded Python)                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │               C# / .NET 8.0 / WPF Application                      │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐ │ │
│  │  │ WPF Windows  │  │ System Tray  │  │  Win32 Keyboard Hooks    │ │ │
│  │  │  (Settings)  │  │ (NotifyIcon) │  │  (LowLevelHookHotkey)    │ │ │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘ │ │
│  │                          │                                         │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │                 Python.NET (pythonnet)                       │ │ │
│  │  │  PythonEngine.Initialize() ─── GIL ─── Py.Import()          │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                          │
│                              ▼                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    Embedded Python Runtime                         │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │  CPython 3.10+ (bundled or system)                           │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  │                              │                                     │ │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐  │ │
│  │  │ faster-whisper  │  │  llama-cpp-py   │  │     PyTorch      │  │ │
│  │  │ (transcription) │  │ (LLM inference) │  │  (CUDA support)  │  │ │
│  │  └─────────────────┘  └─────────────────┘  └──────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                      CUDA / GPU Layer                              │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │  NVIDIA CUDA 12.x (via Python packages)                      │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### Python.NET Integration

```csharp
using Python.Runtime;

public class PythonTranscriptionEngine : ITranscriptionEngine
{
    private dynamic _transcriber;
    
    public async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            PythonEngine.Initialize();
            using (Py.GIL())
            {
                dynamic fasterWhisper = Py.Import("faster_whisper");
                _transcriber = fasterWhisper.WhisperModel(
                    "small", 
                    device: "cuda", 
                    compute_type: "float16"
                );
            }
        });
    }
    
    public async Task<string> TranscribeAsync(string audioPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using (Py.GIL())
            {
                var result = _transcriber.transcribe(audioPath);
                // Extract text from segments
                var segments = result[0];
                var text = new StringBuilder();
                foreach (var segment in segments)
                {
                    text.Append(segment.text);
                }
                return text.ToString().Trim();
            }
        }, ct);
    }
}
```

### LLM Integration via Python.NET

```csharp
public class PythonLLMPostProcessor : ITextPostProcessor
{
    private dynamic _llm;
    
    public async Task InitializeAsync(string modelPath)
    {
        await Task.Run(() =>
        {
            using (Py.GIL())
            {
                dynamic llamaCpp = Py.Import("llama_cpp");
                _llm = llamaCpp.Llama(
                    model_path: modelPath,
                    n_gpu_layers: -1,  // All on GPU
                    n_ctx: 2048
                );
            }
        });
    }
    
    public async Task<string> ProcessAsync(string text, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using (Py.GIL())
            {
                var prompt = $"Correct grammar and punctuation: {text}";
                var result = _llm(prompt, max_tokens: 512);
                return (string)result["choices"][0]["text"];
            }
        }, ct);
    }
}
```

### Deployment Requirements

For Python.NET embedding to work:

1. **Python Runtime** - Must be installed or bundled
   - Option A: Require user to install Python 3.10+
   - Option B: Bundle Python embeddable package (~30MB)

2. **Python Packages** - Must be available at runtime
   - faster-whisper, llama-cpp-python, torch, etc.
   - Either pre-installed in user's Python or bundled in app folder

3. **CUDA Toolkit** - For GPU acceleration
   - CUDA 12.x for CTranslate2/PyTorch

### Advantages of C# + Embedded Python

| Advantage | Description |
|-----------|-------------|
| **Native WPF UI** | Rich Windows-native UI with XAML |
| **Existing Codebase** | Reuse existing .NET infrastructure |
| **Type Safety** | C# interfaces for clean architecture |
| **.NET Ecosystem** | NuGet packages, strong tooling |
| **Full Python ML** | Access to entire Python ML ecosystem |

### Disadvantages of C# + Embedded Python

| Disadvantage | Description |
|--------------|-------------|
| **Complexity** | Two runtimes, interop boundaries, GIL management |
| **Deployment** | Must bundle Python + packages OR require user installation |
| **Performance Overhead** | C# ↔ Python marshalling costs |
| **Debugging** | Harder to debug across language boundary |
| **Version Matching** | Must ensure Python version matches pythonnet requirements |
| **Distribution Size** | Even larger than pure Python (WPF + Python + ML libs) |

### Implementation Effort

**Estimated Effort: 4-6 weeks**

1. ⬜ Add Python.NET (pythonnet) NuGet package
2. ⬜ Create Python environment bundling strategy
3. ⬜ Implement `PythonTranscriptionEngine` (replace whisper.cpp)
4. ⬜ Implement `PythonLLMPostProcessor` (new)
5. ⬜ Update DI container and settings
6. ⬜ Add LLM settings UI
7. ⬜ Handle Python initialization/shutdown lifecycle
8. ⬜ Bundle Python packages with installer
9. ⬜ Test GPU sharing between Whisper and LLM

---

## Comparison Summary

| Aspect | Pure Python | C# + Embedded Python |
|--------|-------------|---------------------|
| **Complexity** | ⭐ Simple | ⭐⭐⭐ Complex |
| **GPU/CUDA Support** | ⭐⭐⭐ Excellent | ⭐⭐⭐ Excellent |
| **UI Quality** | ⭐⭐ Basic (tkinter) | ⭐⭐⭐ Native WPF |
| **Distribution Size** | ~200-500MB | ~400-700MB |
| **Startup Time** | ⭐⭐ Moderate | ⭐ Slow (two runtimes) |
| **ML Ecosystem Access** | ⭐⭐⭐ Direct | ⭐⭐⭐ Via interop |
| **Development Speed** | ⭐⭐⭐ Fast | ⭐⭐ Moderate |
| **Debugging** | ⭐⭐⭐ Easy | ⭐ Hard (cross-language) |
| **Maintenance** | ⭐⭐⭐ Low | ⭐⭐ Higher |
| **Implementation Effort** | 2-3 weeks | 4-6 weeks |

---

## Recommendation

**Recommended: Option 1 - Pure Python Application**

### Rationale

1. **Already Exists**: The Python version in `voxtether-python/` is functional and actively developed.

2. **Native GPU Support**: faster-whisper with CUDA 12 solves the RTX 40-series compatibility issues that plague the .NET/whisper.cpp version.

3. **LLM Integration is Natural**: Python is the dominant language for ML/AI. Libraries like llama-cpp-python, transformers, and Ollama have first-class Python support.

4. **Simpler Architecture**: No interop complexity. Single language, single runtime, single GPU stack.

5. **Performance**: faster-whisper benchmarks show 8-12x speed improvement over whisper.cpp on GPU.

6. **Community & Ecosystem**: Most new ML models, tools, and optimizations are Python-first.

7. **Existing Work**: The .NET version's `ITextPostProcessor` interface is designed as a future extension point for LLM integration—this same pattern can be cleanly implemented in Python.

### Migration Path

1. **Deprecate .NET version** - Mark as "maintenance mode" (already done in README)
2. **Add LLM post-processing to Python version** - New module with llama-cpp-python or Ollama
3. **Improve Python UI** - Consider switching from tkinter to a more modern UI framework (e.g., CustomTkinter, PyQt, or PySide6) if needed
4. **Archive .NET code** - Keep for reference but stop active development

### Future Enhancement: LLM Post-Processing

Add to `voxtether-python/src/`:

```
src/
├── postprocessor.py          # NEW: LLM post-processing
├── postprocessor_ollama.py   # NEW: Ollama API client
├── postprocessor_local.py    # NEW: llama-cpp-python integration
└── ui/
    └── llm_settings.py       # NEW: LLM model selection UI
```

---

## Alternative Consideration: Process-Based Approach

If the .NET UI is strongly preferred, consider a **process-based architecture** instead of embedding:

```
┌────────────────────────────────────────────────────────────────┐
│                  C# / WPF Application                          │
│  (UI, Settings, Hotkeys, Audio Recording)                      │
└───────────────────────────┬────────────────────────────────────┘
                            │ IPC (stdio, named pipes, or REST)
                            ▼
┌────────────────────────────────────────────────────────────────┐
│              Python Backend Process                            │
│  (faster-whisper, LLM inference, CUDA)                         │
└────────────────────────────────────────────────────────────────┘
```

This is essentially what the current .NET version does with whisper.cpp (process-based), but extended to include LLM. This adds latency but avoids the complexity of in-process embedding.

---

## Conclusion

VoxTether should consolidate on the **Pure Python** architecture. The Python ML ecosystem is vastly superior for GPU-accelerated speech recognition and LLM inference. The existing Python version already solves the CUDA 12 / RTX 40-series compatibility issues, and extending it with LLM post-processing is straightforward.

The .NET version should be archived or maintained only for users who specifically need CPU-only or CUDA 11.8 environments.
