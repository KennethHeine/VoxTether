"""Model management API endpoints."""

import json
import logging

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse

from config import settings
from dependencies import get_transcriber
from exceptions import ModelNotFoundError
from schemas import ModelActionResponse, ModelInfo, ModelListResponse
from services.model_manager import ModelManager
from services.transcriber import TranscriberService

logger = logging.getLogger(__name__)

router = APIRouter()

# Initialize model manager
model_manager = ModelManager(settings.models_path)


@router.get("/models", response_model=ModelListResponse)
async def list_models(transcriber: TranscriberService = Depends(get_transcriber)):
    """List available models.
    
    Args:
        transcriber: Transcriber service from dependency injection.
        
    Returns:
        List of available models with download status.
    """
    current_model = transcriber.get_current_model()

    models = model_manager.list_models()
    return ModelListResponse(
        models=[
            ModelInfo(
                name=m["name"],
                display_name=m["display_name"],
                size_mb=m["size_mb"],
                downloaded=m["downloaded"],
                path=m.get("path"),
                description=m.get("description", ""),
            )
            for m in models
        ],
        current_model=current_model,
    )


@router.post("/models/{model_name}/download")
async def download_model(model_name: str):
    """Download a model with progress updates via Server-Sent Events."""

    async def generate_progress():
        """Generate SSE progress updates."""
        try:
            async for progress in model_manager.download_model_async(model_name):
                yield f"data: {progress.model_dump_json()}\n\n"
        except Exception as e:
            logger.error(f"Download failed: {e}")
            error_response = json.dumps({"status": "error", "error": str(e)})
            yield f"data: {error_response}\n\n"

    return StreamingResponse(
        generate_progress(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
        },
    )


@router.delete("/models/{model_name}", response_model=ModelActionResponse)
async def delete_model(model_name: str):
    """Delete a downloaded model.
    
    Args:
        model_name: Name of the model to delete.
        
    Returns:
        Action response indicating success or failure.
        
    Raises:
        ModelNotFoundError: If model is not found.
    """
    success = model_manager.delete_model(model_name)
    if not success:
        raise ModelNotFoundError(model_name)

    return ModelActionResponse(
        success=True,
        model=model_name,
        message=f"Model '{model_name}' deleted successfully",
    )


@router.post("/models/{model_name}/load", response_model=ModelActionResponse)
async def load_model(
    model_name: str,
    transcriber: TranscriberService = Depends(get_transcriber),
):
    """Load a model for transcription.
    
    Args:
        model_name: Name of the model to load.
        transcriber: Transcriber service from dependency injection.
        
    Returns:
        Action response indicating success or failure.
    """
    await transcriber.load_model(model_name)
    return ModelActionResponse(
        success=True,
        model=model_name,
        message=f"Model '{model_name}' loaded successfully",
    )


@router.post("/models/{model_name}/unload", response_model=ModelActionResponse)
async def unload_model(
    model_name: str,
    transcriber: TranscriberService = Depends(get_transcriber),
):
    """Unload the current model.
    
    Args:
        model_name: Name of the model to unload.
        transcriber: Transcriber service from dependency injection.
        
    Returns:
        Action response indicating success.
        
    Raises:
        HTTPException: If the specified model is not the currently loaded model.
    """
    current = transcriber.get_current_model()
    if current and current != model_name:
        raise HTTPException(
            status_code=400,
            detail=f"Cannot unload '{model_name}': currently loaded model is '{current}'"
        )

    transcriber.unload_model()
    return ModelActionResponse(
        success=True,
        model=model_name,
        message=f"Model '{model_name}' unloaded successfully",
    )
