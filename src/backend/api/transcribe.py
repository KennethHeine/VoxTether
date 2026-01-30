"""Transcription API endpoints."""

import logging
import os
import tempfile

from fastapi import APIRouter, File, Form, UploadFile, Depends

from constants import TEMP_AUDIO_SUFFIX
from dependencies import get_transcriber
from schemas import TranscriptionResponse, TranscriptionSettings
from services.transcriber import TranscriberService
from exceptions import ModelNotLoadedError

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe_audio(
    transcriber: TranscriberService = Depends(get_transcriber),
    file: UploadFile = File(..., description="WAV audio file to transcribe"),
    language: str = Form(default="auto", description="Language code or 'auto' for detection"),
    translate: bool = Form(default=False, description="Translate to English"),
):
    """Transcribe an audio file.
    
    Args:
        transcriber: Transcriber service from dependency injection.
        file: Uploaded audio file.
        language: Language code or 'auto' for detection.
        translate: Whether to translate to English.
        
    Returns:
        Transcription result.
        
    Raises:
        ModelNotLoadedError: If no model is loaded.
    """
    if not transcriber.is_loaded():
        raise ModelNotLoadedError()
    
    # Save uploaded file to temp location
    temp_path = None
    try:
        # Create temp file
        with tempfile.NamedTemporaryFile(suffix=TEMP_AUDIO_SUFFIX, delete=False) as temp_file:
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
    settings: TranscriptionSettings,
    transcriber: TranscriberService = Depends(get_transcriber),
):
    """Update transcription settings.
    
    Args:
        settings: New transcription settings.
        transcriber: Transcriber service from dependency injection.
        
    Returns:
        Success response.
    """
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
        return {"success": False, "error": str(e)}
