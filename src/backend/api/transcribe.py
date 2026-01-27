"""Transcription API endpoints."""

import logging
import tempfile
import os
from pathlib import Path
from typing import Optional

from fastapi import APIRouter, File, Form, HTTPException, Request, UploadFile
from pydantic import BaseModel

logger = logging.getLogger(__name__)

router = APIRouter()


class TranscriptionResponse(BaseModel):
    """Response model for transcription."""
    
    text: str
    language: Optional[str] = None
    duration: float
    success: bool
    error: Optional[str] = None


class TranscriptionSettings(BaseModel):
    """Settings for transcription."""
    
    device: str = "auto"
    compute_type: str = "auto"
    language: str = "auto"
    model: Optional[str] = None


@router.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe_audio(
    request: Request,
    file: UploadFile = File(..., description="WAV audio file to transcribe"),
    language: str = Form(default="auto", description="Language code or 'auto' for detection"),
    translate: bool = Form(default=False, description="Translate to English"),
):
    """Transcribe an audio file."""
    transcriber = getattr(request.app.state, "transcriber", None)
    if not transcriber:
        raise HTTPException(status_code=500, detail="Transcriber not initialized")
    
    if not transcriber.is_loaded():
        raise HTTPException(status_code=503, detail="Model not loaded. Please wait or load a model first.")
    
    # Save uploaded file to temp location
    temp_path = None
    try:
        # Create temp file
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
            temp_path = temp_file.name
            content = await file.read()
            temp_file.write(content)
        
        logger.info(f"Transcribing uploaded audio ({len(content)} bytes)")
        
        # Transcribe
        result = await transcriber.transcribe(
            audio_path=temp_path,
            language=language,
            task="translate" if translate else "transcribe",
        )
        
        return TranscriptionResponse(
            text=result.text,
            language=result.language,
            duration=result.duration_seconds,
            success=result.success,
            error=result.error,
        )
        
    except Exception as e:
        logger.error(f"Transcription failed: {e}")
        return TranscriptionResponse(
            text="",
            duration=0,
            success=False,
            error=str(e),
        )
    finally:
        # Clean up temp file
        if temp_path and os.path.exists(temp_path):
            try:
                os.unlink(temp_path)
            except OSError:
                pass


@router.post("/settings")
async def update_settings(
    request: Request,
    settings: TranscriptionSettings,
):
    """Update transcription settings."""
    transcriber = getattr(request.app.state, "transcriber", None)
    if not transcriber:
        raise HTTPException(status_code=500, detail="Transcriber not initialized")
    
    try:
        # Update device if changed
        if settings.device != "auto" or settings.compute_type != "auto":
            await transcriber.change_device(settings.device, settings.compute_type)
        
        # Load different model if specified
        if settings.model:
            await transcriber.load_model(settings.model)
        
        return {"success": True}
    except Exception as e:
        logger.error(f"Failed to update settings: {e}")
        raise HTTPException(status_code=500, detail=str(e))
