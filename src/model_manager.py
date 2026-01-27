"""Model management for VoxTether using HuggingFace Hub."""

import logging
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

from huggingface_hub import snapshot_download
from huggingface_hub.utils import HfHubHTTPError

from .settings import get_models_path

logger = logging.getLogger(__name__)


@dataclass
class ModelInfo:
    """Information about a Whisper model."""
    
    name: str
    repo_id: str
    description: str
    size_mb: int
    recommended_for: str
    supports_gpu: bool = True


# Available models from faster-whisper compatible sources
AVAILABLE_MODELS: dict[str, ModelInfo] = {
    "tiny": ModelInfo(
        name="tiny",
        repo_id="Systran/faster-whisper-tiny",
        description="Fastest model, lower accuracy",
        size_mb=75,
        recommended_for="Quick notes, low-resource systems",
    ),
    "base": ModelInfo(
        name="base",
        repo_id="Systran/faster-whisper-base",
        description="Good balance of speed and accuracy",
        size_mb=142,
        recommended_for="General use",
    ),
    "small": ModelInfo(
        name="small",
        repo_id="Systran/faster-whisper-small",
        description="Better accuracy, moderate speed",
        size_mb=466,
        recommended_for="Recommended for most users",
    ),
    "medium": ModelInfo(
        name="medium",
        repo_id="Systran/faster-whisper-medium",
        description="High accuracy, slower",
        size_mb=1500,
        recommended_for="When accuracy is important",
    ),
    "large-v3": ModelInfo(
        name="large-v3",
        repo_id="Systran/faster-whisper-large-v3",
        description="Highest accuracy, slowest",
        size_mb=3000,
        recommended_for="When accuracy is critical",
    ),
    "large-v3-turbo": ModelInfo(
        name="large-v3-turbo",
        repo_id="Systran/faster-whisper-large-v3-turbo",
        description="Fast large model with excellent accuracy",
        size_mb=1600,
        recommended_for="Best balance of speed and accuracy",
    ),
    "distil-large-v3": ModelInfo(
        name="distil-large-v3",
        repo_id="Systran/faster-distil-whisper-large-v3",
        description="Distilled large model, faster inference",
        size_mb=1100,
        recommended_for="Fast high-quality transcription",
    ),
}


ProgressCallback = Callable[[int, int, str], None]


class ModelManager:
    """Manages Whisper model downloads and storage."""
    
    def __init__(self, models_path: Optional[Path] = None):
        """Initialize the model manager.
        
        Args:
            models_path: Optional custom path for storing models.
        """
        self._models_path = models_path or get_models_path()
    
    @property
    def models_path(self) -> Path:
        """Get the path where models are stored."""
        return self._models_path
    
    def get_available_models(self) -> dict[str, ModelInfo]:
        """Get information about all available models."""
        return AVAILABLE_MODELS.copy()
    
    def get_model_info(self, model_name: str) -> Optional[ModelInfo]:
        """Get information about a specific model.
        
        Args:
            model_name: Name of the model.
            
        Returns:
            ModelInfo if found, None otherwise.
        """
        return AVAILABLE_MODELS.get(model_name)
    
    def get_model_path(self, model_name: str) -> Optional[Path]:
        """Get the local path for a model if it exists.
        
        Args:
            model_name: Name of the model.
            
        Returns:
            Path to the model directory if it exists, None otherwise.
        """
        model_info = self.get_model_info(model_name)
        if not model_info:
            return None
        
        # Models are stored in HuggingFace cache format
        model_dir = self._models_path / model_info.repo_id.replace("/", "--")
        if model_dir.exists() and any(model_dir.iterdir()):
            return model_dir
        
        return None
    
    def is_model_downloaded(self, model_name: str) -> bool:
        """Check if a model is already downloaded.
        
        Args:
            model_name: Name of the model.
            
        Returns:
            True if the model is downloaded, False otherwise.
        """
        return self.get_model_path(model_name) is not None
    
    def get_downloaded_models(self) -> list[str]:
        """Get a list of all downloaded model names.
        
        Returns:
            List of downloaded model names.
        """
        downloaded = []
        for name in AVAILABLE_MODELS:
            if self.is_model_downloaded(name):
                downloaded.append(name)
        return downloaded
    
    def download_model(
        self,
        model_name: str,
        progress_callback: Optional[ProgressCallback] = None,
    ) -> Path:
        """Download a model from HuggingFace Hub.
        
        Args:
            model_name: Name of the model to download.
            progress_callback: Optional callback for progress updates.
                Receives (current_bytes, total_bytes, status_message).
        
        Returns:
            Path to the downloaded model directory.
            
        Raises:
            ValueError: If model_name is not recognized.
            RuntimeError: If download fails.
        """
        model_info = self.get_model_info(model_name)
        if not model_info:
            raise ValueError(f"Unknown model: {model_name}")
        
        logger.info(f"Downloading model '{model_name}' from {model_info.repo_id}")
        
        if progress_callback:
            progress_callback(0, model_info.size_mb * 1024 * 1024, "Starting download...")
        
        try:
            # Use snapshot_download to get the full model
            model_path = snapshot_download(
                repo_id=model_info.repo_id,
                local_dir=self._models_path / model_info.repo_id.replace("/", "--"),
                local_dir_use_symlinks=False,
            )
            
            if progress_callback:
                progress_callback(
                    model_info.size_mb * 1024 * 1024,
                    model_info.size_mb * 1024 * 1024,
                    "Download complete!",
                )
            
            logger.info(f"Model downloaded to {model_path}")
            return Path(model_path)
            
        except HfHubHTTPError as e:
            logger.error(f"Failed to download model: {e}")
            raise RuntimeError(f"Failed to download model '{model_name}': {e}") from e
        except Exception as e:
            logger.error(f"Unexpected error downloading model: {e}")
            raise RuntimeError(f"Unexpected error downloading model '{model_name}': {e}") from e
    
    def delete_model(self, model_name: str) -> bool:
        """Delete a downloaded model.
        
        Args:
            model_name: Name of the model to delete.
            
        Returns:
            True if the model was deleted, False if it wasn't found.
        """
        model_path = self.get_model_path(model_name)
        if not model_path:
            return False
        
        try:
            import shutil
            shutil.rmtree(model_path)
            logger.info(f"Deleted model '{model_name}' from {model_path}")
            return True
        except Exception as e:
            logger.error(f"Failed to delete model: {e}")
            return False
    
    def get_model_for_transcriber(self, model_name: str) -> str:
        """Get the model identifier for use with faster-whisper.
        
        faster-whisper can load models directly from HuggingFace by repo_id,
        or from a local path. This method returns the appropriate identifier.
        
        Args:
            model_name: Name of the model.
            
        Returns:
            Model path or repo_id for faster-whisper.
            
        Raises:
            ValueError: If model_name is not recognized.
        """
        model_info = self.get_model_info(model_name)
        if not model_info:
            raise ValueError(f"Unknown model: {model_name}")
        
        # Check if model is downloaded locally
        local_path = self.get_model_path(model_name)
        if local_path:
            return str(local_path)
        
        # Otherwise, faster-whisper will download from HuggingFace
        return model_info.repo_id
