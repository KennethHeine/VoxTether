"""FastAPI dependency injection functions."""

from typing import Optional
from fastapi import Request, HTTPException

from services.transcriber import TranscriberService
from exceptions import ModelNotLoadedError


def get_transcriber(request: Request) -> TranscriberService:
    """Get the transcriber service from app state.

    Args:
        request: FastAPI request object.

    Returns:
        TranscriberService instance.

    Raises:
        HTTPException: If transcriber is not initialized.
    """
    transcriber = getattr(request.app.state, "transcriber", None)
    if transcriber is None:
        raise HTTPException(status_code=500, detail="Transcriber not initialized")
    return transcriber


def get_transcriber_optional(request: Request) -> Optional[TranscriberService]:
    """Get the transcriber service from app state (optional).

    Args:
        request: FastAPI request object.

    Returns:
        TranscriberService instance or None.
    """
    return getattr(request.app.state, "transcriber", None)


def require_loaded_model(transcriber: TranscriberService) -> TranscriberService:
    """Ensure that a model is loaded in the transcriber.

    Args:
        transcriber: TranscriberService instance.

    Returns:
        TranscriberService instance.

    Raises:
        ModelNotLoadedError: If no model is loaded.
    """
    if not transcriber.is_loaded():
        raise ModelNotLoadedError(
            "Model not loaded. Please wait or load a model first."
        )
    return transcriber
