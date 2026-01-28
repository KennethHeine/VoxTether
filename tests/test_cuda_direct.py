"""Direct CUDA and transcription test without web server.

This script tests CUDA functionality and transcription directly,
helping diagnose issues like missing cuBLAS libraries.

Usage:
    cd src/backend
    python -m tests.test_cuda_direct
"""

import sys
import os
from pathlib import Path

# Add the backend to the path
backend_path = Path(__file__).parent.parent / "src" / "backend"
sys.path.insert(0, str(backend_path))


def _setup_cuda_dll_paths() -> None:
    """Add NVIDIA CUDA DLL paths to system PATH on Windows."""
    if sys.platform != "win32":
        return
    
    site_packages = Path(sys.prefix) / "Lib" / "site-packages" / "nvidia"
    if not site_packages.exists():
        return
    
    nvidia_bin_paths = []
    for subdir in ["cublas", "cudnn", "cuda_runtime", "cufft", "curand"]:
        bin_path = site_packages / subdir / "bin"
        if bin_path.exists():
            nvidia_bin_paths.append(str(bin_path))
    
    if nvidia_bin_paths:
        current_path = os.environ.get("PATH", "")
        new_paths = os.pathsep.join(nvidia_bin_paths)
        if nvidia_bin_paths[0] not in current_path:
            os.environ["PATH"] = new_paths + os.pathsep + current_path


# Setup CUDA DLL paths BEFORE any CUDA imports
_setup_cuda_dll_paths()


def print_header(title: str) -> None:
    """Print a section header."""
    print()
    print("=" * 60)
    print(title)
    print("=" * 60)


def print_step(step: str, status: str = "") -> None:
    """Print a step with optional status."""
    if status:
        print(f"  {step}: {status}")
    else:
        print(f"  {step}")


def test_cuda_detection() -> dict:
    """Test CUDA detection methods."""
    print_header("Step 1: CUDA Detection")
    
    result = {
        "torch_available": False,
        "torch_cuda": False,
        "ctranslate2_cuda": False,
        "cuda_device_count": 0,
        "cuda_device_name": None,
    }
    
    # Test PyTorch CUDA
    try:
        import torch
        result["torch_available"] = True
        result["torch_cuda"] = torch.cuda.is_available()
        if result["torch_cuda"]:
            result["cuda_device_name"] = torch.cuda.get_device_name(0)
            print_step("PyTorch CUDA", f"✓ Available ({result['cuda_device_name']})")
        else:
            print_step("PyTorch CUDA", "✗ Not available")
    except ImportError:
        print_step("PyTorch", "Not installed (optional)")
    
    # Test ctranslate2 CUDA
    try:
        import ctranslate2
        result["cuda_device_count"] = ctranslate2.get_cuda_device_count()
        result["ctranslate2_cuda"] = result["cuda_device_count"] > 0
        if result["ctranslate2_cuda"]:
            print_step("ctranslate2 CUDA", f"✓ {result['cuda_device_count']} device(s) detected")
        else:
            print_step("ctranslate2 CUDA", "✗ No devices detected")
    except Exception as e:
        print_step("ctranslate2 CUDA", f"✗ Error: {e}")
    
    return result


def test_cuda_libraries() -> dict:
    """Test if CUDA libraries (cuBLAS, cuDNN) are actually loadable."""
    print_header("Step 2: CUDA Libraries Check")
    
    result = {
        "cublas_available": False,
        "cudnn_available": False,
        "cublas_dll_found": False,
        "cudnn_dll_found": False,
        "cublas_error": None,
        "cudnn_error": None,
    }
    
    # Check for cublas DLL (the actual file, not the Python package)
    site_packages = Path(sys.prefix) / "Lib" / "site-packages" / "nvidia"
    cublas_dll = site_packages / "cublas" / "bin" / "cublas64_12.dll"
    cudnn_dll = site_packages / "cudnn" / "bin" / "cudnn64_9.dll"
    
    if cublas_dll.exists():
        result["cublas_available"] = True
        result["cublas_dll_found"] = True
        print_step("nvidia-cublas-cu12", f"✓ Found: {cublas_dll}")
    else:
        result["cublas_error"] = "DLL not found"
        print_step("nvidia-cublas-cu12", "✗ Not installed")
    
    # Check for nvidia-cudnn-cu12
    if cudnn_dll.exists():
        result["cudnn_available"] = True
        result["cudnn_dll_found"] = True
        print_step("nvidia-cudnn-cu12", f"✓ Found: {cudnn_dll}")
    else:
        result["cudnn_error"] = "DLL not found"
        print_step("nvidia-cudnn-cu12", "✗ Not installed")
    
    return result


def test_model_loading(device: str = "auto") -> dict:
    """Test loading a whisper model."""
    print_header(f"Step 3: Model Loading (device={device})")
    
    result = {
        "success": False,
        "device_used": None,
        "compute_type": None,
        "error": None,
    }
    
    try:
        from faster_whisper import WhisperModel
        import time
        
        # Determine device
        if device == "auto":
            try:
                import ctranslate2
                if ctranslate2.get_cuda_device_count() > 0:
                    device = "cuda"
                else:
                    device = "cpu"
            except Exception:
                device = "cpu"
        
        compute_type = "float16" if device == "cuda" else "int8"
        
        print_step(f"Loading 'tiny' model on {device} ({compute_type})...")
        
        start = time.time()
        model = WhisperModel(
            "tiny",
            device=device,
            compute_type=compute_type,
        )
        elapsed = time.time() - start
        
        result["success"] = True
        result["device_used"] = device
        result["compute_type"] = compute_type
        
        print_step("Model loaded", f"✓ in {elapsed:.2f}s")
        
        # Return the model for transcription test
        result["model"] = model
        
    except Exception as e:
        result["error"] = str(e)
        print_step("Model loading", f"✗ Failed: {e}")
    
    return result


def test_transcription(model, audio_path: str = None) -> dict:
    """Test actual transcription."""
    print_header("Step 4: Transcription Test")
    
    result = {
        "success": False,
        "text": None,
        "error": None,
        "duration": None,
    }
    
    # Find test audio file
    if audio_path is None:
        test_files = [
            Path(__file__).parent / "test-recoarding.wav",
            Path(__file__).parent / "test-recording.wav",
            Path(__file__).parent / "test.wav",
        ]
        for tf in test_files:
            if tf.exists():
                audio_path = str(tf)
                break
    
    if not audio_path or not Path(audio_path).exists():
        print_step("No test audio file found")
        print_step("Create a test audio file at: tests/test-recoarding.wav")
        result["error"] = "No test audio file"
        return result
    
    print_step(f"Audio file: {audio_path}")
    print_step(f"File size: {Path(audio_path).stat().st_size:,} bytes")
    
    try:
        import time
        
        print_step("Transcribing...")
        
        start = time.time()
        segments, info = model.transcribe(
            audio_path,
            beam_size=5,
            vad_filter=True,
        )
        
        # Consume the generator - this is where CUDA is actually used!
        text_parts = []
        for segment in segments:
            text_parts.append(segment.text)
        
        text = "".join(text_parts).strip()
        elapsed = time.time() - start
        
        result["success"] = True
        result["text"] = text
        result["duration"] = elapsed
        result["language"] = info.language
        
        print_step("Transcription", f"✓ Completed in {elapsed:.2f}s")
        print_step(f"Language: {info.language}")
        print_step(f"Text: \"{text[:100]}{'...' if len(text) > 100 else ''}\"")
        
    except Exception as e:
        result["error"] = str(e)
        print_step("Transcription", f"✗ Failed: {e}")
        
        # Check for specific CUDA errors
        error_str = str(e).lower()
        if "cublas" in error_str or "cuda" in error_str:
            print()
            print("  " + "-" * 50)
            print("  CUDA LIBRARY ERROR DETECTED!")
            print("  " + "-" * 50)
            print("  The CUDA device is detected, but runtime libraries are missing.")
            print()
            print("  To fix this, install the CUDA libraries:")
            print("    pip install nvidia-cublas-cu12 nvidia-cudnn-cu12")
            print()
            print("  Or, force CPU mode by setting device='cpu'")
            print("  " + "-" * 50)
    
    return result


def run_full_test():
    """Run all tests."""
    print()
    print("╔" + "═" * 58 + "╗")
    print("║" + " VoxTether Direct CUDA & Transcription Test ".center(58) + "║")
    print("╚" + "═" * 58 + "╝")
    
    # Step 1: CUDA Detection
    cuda_result = test_cuda_detection()
    
    # Step 2: CUDA Libraries
    lib_result = test_cuda_libraries()
    
    # Step 3: Model Loading (try CUDA first)
    model_result = test_model_loading("auto")
    
    # Step 4: Transcription
    if model_result.get("model"):
        trans_result = test_transcription(model_result["model"])
        
        # If CUDA transcription failed, try CPU fallback
        if not trans_result["success"] and model_result["device_used"] == "cuda":
            print_header("Step 5: CPU Fallback Test")
            print_step("CUDA transcription failed, trying CPU...")
            
            cpu_model_result = test_model_loading("cpu")
            if cpu_model_result.get("model"):
                cpu_trans_result = test_transcription(cpu_model_result["model"])
                if cpu_trans_result["success"]:
                    print()
                    print("  ✓ CPU fallback works! Your transcription can work on CPU.")
    else:
        trans_result = {"success": False, "error": "Model not loaded"}
    
    # Summary
    print_header("Summary")
    
    cuda_detected = cuda_result["ctranslate2_cuda"] or cuda_result["torch_cuda"]
    cuda_libs_ok = lib_result["cublas_available"]
    
    if cuda_detected and not cuda_libs_ok:
        print("  ⚠ CUDA GPU detected but runtime libraries are MISSING")
        print()
        print("  Your GPU can be used, but you need to install:")
        print("    pip install nvidia-cublas-cu12 nvidia-cudnn-cu12")
        print()
        print("  Alternatively, use CPU mode (slower but works):")
        print("    Set VOXTETHER_DEVICE=cpu in your environment")
    elif cuda_detected and cuda_libs_ok:
        if trans_result["success"]:
            print("  ✓ CUDA is fully working!")
        else:
            print("  ⚠ CUDA libraries installed but transcription failed")
            print(f"    Error: {trans_result.get('error', 'Unknown')}")
    else:
        print("  ℹ No CUDA GPU detected, using CPU mode")
        if trans_result["success"]:
            print("  ✓ CPU transcription works!")
    
    print()
    return trans_result["success"]


if __name__ == "__main__":
    success = run_full_test()
    sys.exit(0 if success else 1)
