"""Model management API endpoints."""

import asyncio
import logging
import os
from typing import List, Optional

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import StreamingResponse
from pydantic import BaseModel

from config import settings
from services.model_manager import ModelManager

logger = logging.getLogger(__name__)

router = APIRouter()

# Initialize model manager
model_manager = ModelManager(settings.models_path)


class ModelInfo(BaseModel):
    """Information about a model."""
    
    name: str
    display_name: str
    size_mb: int
    downloaded: bool
    path: Optional[str] = None
    description: str = ""


class ModelListResponse(BaseModel):
    """Response model for model list."""
    
    models: List[ModelInfo]
    current_model: Optional[str] = None


class LoadModelRequest(BaseModel):
    """Request to load a model."""
    
    model_name: str


@router.get("/models", response_model=ModelListResponse)
async def list_models(request: Request):
    """List available models."""
    transcriber = getattr(request.app.state, "transcriber", None)
    current_model = transcriber.get_current_model() if transcriber else None
    
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
            yield f'data: {{"status": "error", "error": "{str(e)}"}}\n\n'
    
    return StreamingResponse(
        generate_progress(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
        },
    )


@router.delete("/models/{model_name}")
async def delete_model(model_name: str):
    """Delete a downloaded model."""
    try:
        success = model_manager.delete_model(model_name)
        if not success:
            raise HTTPException(status_code=404, detail="Model not found")
        return {"success": True}
    except Exception as e:
        logger.error(f"Failed to delete model: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/models/{model_name}/load")
async def load_model(model_name: str, request: Request):
    """Load a model for transcription."""
    transcriber = getattr(request.app.state, "transcriber", None)
    if not transcriber:
        raise HTTPException(status_code=500, detail="Transcriber not initialized")
    
    try:
        await transcriber.load_model(model_name)
        return {"success": True, "model": model_name}
    except Exception as e:
        logger.error(f"Failed to load model: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/models/{model_name}/unload")
async def unload_model(request: Request):
    """Unload the current model."""
    transcriber = getattr(request.app.state, "transcriber", None)
    if not transcriber:
        raise HTTPException(status_code=500, detail="Transcriber not initialized")
    
    transcriber.unload_model()
    return {"success": True}
