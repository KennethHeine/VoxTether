"""Transcription API endpoints."""

import logging
import os
import tempfile
from pathlib import Path

from typing import Optional

from fastapi import APIRouter, File, Form, HTTPException, UploadFile, Depends

from config import settings
from constants import (
    TEMP_AUDIO_SUFFIX, SUPPORTED_LANGUAGES, MAX_INITIAL_PROMPT_LENGTH,
    AVAILABLE_MODELS, VALID_DEVICES, VALID_COMPUTE_TYPES,
)
from dependencies import get_transcriber
from schemas import TranscriptionResponse, TranscriptionSettings
from services.transcriber import TranscriberService
from exceptions import ModelNotLoadedError

logger = logging.getLogger(__name__)

router = APIRouter()

# Allowed audio file extensions and content types
ALLOWED_AUDIO_EXTENSIONS = {".wav", ".mp3", ".flac", ".ogg", ".m4a", ".webm"}
ALLOWED_CONTENT_TYPES = {
    "audio/wav", "audio/x-wav", "audio/wave",
    "audio/mpeg", "audio/mp3",
    "audio/flac", "audio/x-flac",
    "audio/ogg", "audio/m4a", "audio/mp4", "audio/webm",
    "application/octet-stream",  # Allow generic binary for flexibility
}


@router.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe_audio(
    transcriber: TranscriberService = Depends(get_transcriber),
    file: UploadFile = File(..., description="Audio file to transcribe"),
    language: str = Form(default="auto", description="Language code or 'auto' for detection"),
    translate: bool = Form(default=False, description="Translate to English"),
    initial_prompt: Optional[str] = Form(default=None, description="Prompt to guide transcription (e.g., domain-specific terms)"),
    word_timestamps: bool = Form(default=False, description="Return word-level timestamps"),
):
    """Transcribe an audio file.
    
    Args:
        transcriber: Transcriber service from dependency injection.
        file: Uploaded audio file.
        language: Language code or 'auto' for detection.
        translate: Whether to translate to English.
        initial_prompt: Optional prompt to guide transcription.
        word_timestamps: Whether to return word-level timestamps.
        
    Returns:
        Transcription result.
        
    Raises:
        ModelNotLoadedError: If no model is loaded.
        HTTPException: If file type is invalid or file is too large.
    """
    if not transcriber.is_loaded():
        raise ModelNotLoadedError()
    
    # Validate file extension (allow files without extension for flexibility)
    if file.filename:
        ext = Path(file.filename).suffix.lower()
        if ext and ext not in ALLOWED_AUDIO_EXTENSIONS:
            raise HTTPException(
                status_code=400,
                detail=f"Invalid file type: {ext}. Allowed: {', '.join(sorted(ALLOWED_AUDIO_EXTENSIONS))}"
            )
    
    # Validate content type
    if file.content_type and file.content_type not in ALLOWED_CONTENT_TYPES:
        logger.warning(f"Unexpected content type: {file.content_type}")
    
    # Validate language code
    if language and language not in SUPPORTED_LANGUAGES:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid language code: '{language}'. Use 'auto' for automatic detection or a valid ISO 639-1 code."
        )
    
    # Validate initial_prompt length
    if initial_prompt and len(initial_prompt) > MAX_INITIAL_PROMPT_LENGTH:
        raise HTTPException(
            status_code=400,
            detail=f"Initial prompt too long. Maximum length: {MAX_INITIAL_PROMPT_LENGTH} characters."
        )
    
    # Save uploaded file to temp location
    temp_path = None
    try:
        # Read file content
        content = await file.read()
        
        # Check file size
        max_size = settings.max_upload_size_mb * 1024 * 1024
        if len(content) > max_size:
            raise HTTPException(
                status_code=413,
                detail=f"File too large. Maximum size: {settings.max_upload_size_mb} MB"
            )
        
        # Create temp file
        with tempfile.NamedTemporaryFile(suffix=TEMP_AUDIO_SUFFIX, delete=False) as temp_file:
            temp_path = temp_file.name
            temp_file.write(content)
        
        logger.info(f"Transcribing uploaded audio ({len(content)} bytes)")
        
        # Transcribe
        result = await transcriber.transcribe(
            audio_path=temp_path,
            language=language,
            task="translate" if translate else "transcribe",
            initial_prompt=initial_prompt,
            word_timestamps=word_timestamps,
        )
        
        return TranscriptionResponse(
            text=result.text,
            language=result.language,
            duration=result.duration_seconds,
            success=result.success,
            error=result.error,
            words=result.words,
        )
        
    except HTTPException:
        # Re-raise HTTP exceptions to be handled by FastAPI
        raise
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
        
    Raises:
        HTTPException: If device, compute_type, or model name is invalid.
    """
    # Validate device
    if settings.device not in VALID_DEVICES:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid device: '{settings.device}'. Must be one of: {', '.join(VALID_DEVICES)}"
        )
    
    # Validate compute type
    if settings.compute_type not in VALID_COMPUTE_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid compute type: '{settings.compute_type}'. Must be one of: {', '.join(VALID_COMPUTE_TYPES)}"
        )
    
    # Validate model name if specified
    if settings.model and settings.model not in AVAILABLE_MODELS:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid model: '{settings.model}'. Must be one of: {', '.join(AVAILABLE_MODELS.keys())}"
        )
    
    # Update device if changed
    if settings.device != "auto" or settings.compute_type != "auto":
        await transcriber.change_device(settings.device, settings.compute_type)
    
    # Load different model if specified
    if settings.model:
        await transcriber.load_model(settings.model)
    
    return {"success": True}
