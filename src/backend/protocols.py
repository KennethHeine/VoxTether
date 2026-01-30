"""Protocol definitions for VoxTether backend services."""

from typing import Optional, Protocol, AsyncGenerator
from dataclasses import dataclass


@dataclass
class TranscriptionResult:
    """Result of a transcription operation."""

    text: str
    success: bool
    duration_seconds: float
    language: Optional[str] = None
    error: Optional[str] = None


class TranscriberProtocol(Protocol):
    """Protocol for transcription services."""

    async def load_model(self, model_name: Optional[str] = None) -> bool:
        """Load a transcription model."""
        ...

    def unload_model(self) -> None:
        """Unload the current model."""
        ...

    def is_loaded(self) -> bool:
        """Check if a model is loaded."""
        ...

    def get_current_model(self) -> Optional[str]:
        """Get the name of the currently loaded model."""
        ...

    def get_current_device(self) -> Optional[str]:
        """Get the current compute device."""
        ...

    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
    ) -> TranscriptionResult:
        """Transcribe an audio file."""
        ...


@dataclass
class DownloadProgress:
    """Progress update for model download."""

    status: str  # "downloading", "complete", "error"
    progress: float = 0.0  # 0-100
    downloaded_mb: float = 0.0
    total_mb: float = 0.0
    speed_mbps: float = 0.0
    error: Optional[str] = None


class ModelManagerProtocol(Protocol):
    """Protocol for model management services."""

    def list_models(self) -> list:
        """List all available models with download status."""
        ...

    def is_model_downloaded(self, model_name: str) -> bool:
        """Check if a model is downloaded."""
        ...

    async def download_model_async(
        self, model_name: str
    ) -> AsyncGenerator[DownloadProgress, None]:
        """Download a model with progress updates."""
        ...

    def delete_model(self, model_name: str) -> bool:
        """Delete a downloaded model."""
        ...
