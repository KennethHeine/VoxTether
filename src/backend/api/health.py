"""Health check API endpoints."""

import subprocess
from typing import Optional

from fastapi import APIRouter, Request

router = APIRouter()


def _detect_nvidia_gpu_via_smi() -> Optional[str]:
    """Detect NVIDIA GPU using nvidia-smi command."""
    try:
        result = subprocess.run(
            ["nvidia-smi", "--query-gpu=name", "--format=csv,noheader,nounits"],
            capture_output=True,
            text=True,
            timeout=5,
            creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, "CREATE_NO_WINDOW") else 0,
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip().split("\n")[0]
    except (FileNotFoundError, subprocess.TimeoutExpired, OSError):
        pass
    return None


def _get_device_info() -> dict:
    """Get information about available compute devices."""
    cuda_available = False
    cuda_version = None
    device_name = None
    
    try:
        import torch
        cuda_available = torch.cuda.is_available()
        if cuda_available:
            device_name = torch.cuda.get_device_name(0)
            cuda_version = torch.version.cuda
    except ImportError:
        try:
            import ctranslate2
            cuda_available = ctranslate2.get_cuda_device_count() > 0
            if cuda_available:
                device_name = _detect_nvidia_gpu_via_smi()
        except (ImportError, ValueError, RuntimeError):
            pass
    
    # Fallback to nvidia-smi if libraries didn't detect
    if not device_name:
        device_name = _detect_nvidia_gpu_via_smi()
    
    return {
        "cuda_available": cuda_available,
        "cuda_version": cuda_version,
        "device_name": device_name,
    }


@router.get("/health")
async def health_check(request: Request):
    """Health check endpoint."""
    transcriber = getattr(request.app.state, "transcriber", None)
    model_loaded = transcriber.is_loaded() if transcriber else False
    current_device = transcriber.get_current_device() if transcriber else None
    
    return {
        "status": "ok",
        "model_loaded": model_loaded,
        "device": current_device,
    }


@router.get("/devices")
async def get_devices():
    """Get information about available compute devices."""
    return _get_device_info()
