"""Protocol definitions for VoxTether backend services."""

from typing import Optional, Protocol, AsyncGenerator

from schemas import DownloadProgress


class TranscriptionResult:
    """Result of a transcription operation."""

    def __init__(
        self,
        text: str,
        success: bool,
        duration_seconds: float,
        language: Optional[str] = None,
        error: Optional[str] = None,
    ):
        self.text = text
        self.success = success
        self.duration_seconds = duration_seconds
        self.language = language
        self.error = error


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
