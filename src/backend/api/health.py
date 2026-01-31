"""Health check API endpoints."""

import subprocess
import time
from typing import Optional

from fastapi import APIRouter, Request, Depends

from constants import APP_VERSION
from dependencies import get_transcriber_optional
from schemas import HealthCheckResponse, DeviceInfo
from services.transcriber import TranscriberService

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


@router.get("/health", response_model=HealthCheckResponse)
async def health_check(
    request: Request,
    transcriber: Optional[TranscriberService] = Depends(get_transcriber_optional),
):
    """Enhanced health check endpoint with detailed status.
    
    Args:
        request: FastAPI request object.
        transcriber: Transcriber service (optional).
        
    Returns:
        Detailed health check response.
    """
    model_loaded = transcriber.is_loaded() if transcriber else False
    current_model = transcriber.get_current_model() if transcriber else None
    current_device = transcriber.get_current_device() if transcriber else None
    
    # Calculate uptime
    start_time = getattr(request.app.state, "start_time", time.time())
    uptime_seconds = time.time() - start_time
    
    # Determine overall status
    if transcriber and model_loaded:
        status = "healthy"
    elif transcriber:
        status = "degraded"  # Transcriber exists but no model loaded
    else:
        status = "unhealthy"  # No transcriber
    
    # Component checks
    checks = {
        "transcriber": "healthy" if transcriber else "unhealthy",
        "model": "loaded" if model_loaded else "not_loaded",
    }
    
    return HealthCheckResponse(
        status=status,
        version=APP_VERSION,
        model_loaded=model_loaded,
        model_name=current_model,
        device=current_device,
        uptime_seconds=uptime_seconds,
        checks=checks,
    )


@router.get("/devices", response_model=DeviceInfo)
async def get_devices():
    """Get information about available compute devices.
    
    Returns:
        Device information.
    """
    return DeviceInfo(**_get_device_info())
