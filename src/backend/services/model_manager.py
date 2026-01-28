"""Model management service for VoxTether backend."""

import asyncio
import logging
import os
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Any, AsyncGenerator, Dict, List, Optional

from pydantic import BaseModel

logger = logging.getLogger(__name__)


# Available Whisper models with their sizes
AVAILABLE_MODELS = {
    "tiny": {
        "display_name": "Tiny",
        "size_mb": 75,
        "description": "Fastest, lowest accuracy. Good for quick notes.",
        "repo_id": "Systran/faster-whisper-tiny",
    },
    "base": {
        "display_name": "Base", 
        "size_mb": 142,
        "description": "Fast with reasonable accuracy.",
        "repo_id": "Systran/faster-whisper-base",
    },
    "small": {
        "display_name": "Small",
        "size_mb": 466,
        "description": "Good balance of speed and accuracy. Recommended for most users.",
        "repo_id": "Systran/faster-whisper-small",
    },
    "medium": {
        "display_name": "Medium",
        "size_mb": 1500,
        "description": "High accuracy, slower transcription.",
        "repo_id": "Systran/faster-whisper-medium",
    },
    "large-v3": {
        "display_name": "Large V3",
        "size_mb": 3000,
        "description": "Best accuracy, slowest. Requires significant GPU memory.",
        "repo_id": "Systran/faster-whisper-large-v3",
    },
    "large-v3-turbo": {
        "display_name": "Large V3 Turbo",
        "size_mb": 1600,
        "description": "Excellent accuracy with faster speed. Great GPU option.",
        "repo_id": "deepdml/faster-whisper-large-v3-turbo-ct2",
    },
    "distil-large-v3": {
        "display_name": "Distil Large V3",
        "size_mb": 1100,
        "description": "Distilled model with excellent speed/accuracy trade-off.",
        "repo_id": "Systran/faster-distil-whisper-large-v3",
    },
}


class DownloadProgress(BaseModel):
    """Progress update for model download."""
    
    status: str  # "downloading", "complete", "error"
    progress: float = 0.0  # 0-100
    downloaded_mb: float = 0.0
    total_mb: float = 0.0
    speed_mbps: float = 0.0
    error: Optional[str] = None


class ModelManager:
    """Manages Whisper model downloads and storage."""
    
    def __init__(self, models_path: str):
        """Initialize the model manager.
        
        Args:
            models_path: Path to store downloaded models.
        """
        self.models_path = Path(models_path)
        self.models_path.mkdir(parents=True, exist_ok=True)
    
    def list_models(self) -> List[Dict[str, Any]]:
        """List all available models with download status.
        
        Returns:
            List of model information dictionaries.
        """
        models = []
        
        for name, info in AVAILABLE_MODELS.items():
            model_path = self._get_model_path(name)
            downloaded = model_path is not None
            
            models.append({
                "name": name,
                "display_name": info["display_name"],
                "size_mb": info["size_mb"],
                "description": info["description"],
                "downloaded": downloaded,
                "path": str(model_path) if model_path else None,
            })
        
        return models
    
    def _get_model_path(self, model_name: str) -> Optional[Path]:
        """Get the path to a downloaded model.
        
        Args:
            model_name: Model name.
            
        Returns:
            Path to the model directory if it exists.
        """
        # Check for model in various locations
        possible_paths = [
            self.models_path / model_name,
            self.models_path / f"faster-whisper-{model_name}",
            self.models_path / f"whisper-{model_name}",
        ]
        
        for path in possible_paths:
            if path.exists() and (path / "model.bin").exists():
                return path
        
        return None
    
    def is_model_downloaded(self, model_name: str) -> bool:
        """Check if a model is downloaded.
        
        Args:
            model_name: Model name.
            
        Returns:
            True if the model is downloaded.
        """
        return self._get_model_path(model_name) is not None
    
    async def download_model_async(self, model_name: str) -> AsyncGenerator[DownloadProgress, None]:
        """Download a model with progress updates.
        
        Args:
            model_name: Model name to download.
            
        Yields:
            DownloadProgress updates.
        """
        if model_name not in AVAILABLE_MODELS:
            yield DownloadProgress(status="error", error=f"Unknown model: {model_name}")
            return
        
        model_info = AVAILABLE_MODELS[model_name]
        repo_id = model_info["repo_id"]
        total_mb = model_info["size_mb"]
        
        # Use huggingface_hub for downloading
        try:
            from huggingface_hub import snapshot_download
            
            target_path = self.models_path / model_name
            
            yield DownloadProgress(
                status="downloading",
                progress=0,
                downloaded_mb=0,
                total_mb=total_mb,
            )
            
            # Download in a thread to not block
            def _download():
                return snapshot_download(
                    repo_id=repo_id,
                    local_dir=str(target_path),
                    local_dir_use_symlinks=False,
                )
            
            loop = asyncio.get_event_loop()
            
            # Start download in background
            download_task = loop.run_in_executor(None, _download)
            
            # Poll for progress (simplified - huggingface_hub doesn't have great progress callbacks)
            while not download_task.done():
                await asyncio.sleep(0.5)
                
                # Estimate progress based on downloaded files
                if target_path.exists():
                    total_size = sum(f.stat().st_size for f in target_path.rglob("*") if f.is_file())
                    downloaded_mb = total_size / (1024 * 1024)
                    progress = min(95, (downloaded_mb / total_mb) * 100)
                    
                    yield DownloadProgress(
                        status="downloading",
                        progress=progress,
                        downloaded_mb=downloaded_mb,
                        total_mb=total_mb,
                    )
            
            # Get result (will raise if there was an error)
            await download_task
            
            yield DownloadProgress(
                status="complete",
                progress=100,
                downloaded_mb=total_mb,
                total_mb=total_mb,
            )
            
        except Exception as e:
            logger.error(f"Download failed: {e}")
            yield DownloadProgress(status="error", error=str(e))
    
    def delete_model(self, model_name: str) -> bool:
        """Delete a downloaded model.
        
        Args:
            model_name: Model name to delete.
            
        Returns:
            True if deleted successfully.
        """
        model_path = self._get_model_path(model_name)
        if model_path is None:
            return False
        
        try:
            shutil.rmtree(model_path)
            logger.info(f"Deleted model: {model_name}")
            return True
        except Exception as e:
            logger.error(f"Failed to delete model: {e}")
            return False
