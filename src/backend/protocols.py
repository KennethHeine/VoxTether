"""Protocol definitions for VoxTether backend services."""

from collections.abc import AsyncGenerator
from typing import Protocol

from schemas import DownloadProgress, WordInfo


class TranscriptionResult:
    """Result of a transcription operation."""

    def __init__(
        self,
        text: str,
        success: bool,
        duration_seconds: float,
        language: str | None = None,
        error: str | None = None,
        words: list[WordInfo] | None = None,
    ):
        self.text = text
        self.success = success
        self.duration_seconds = duration_seconds
        self.language = language
        self.error = error
        self.words = words


class TranscriberProtocol(Protocol):
    """Protocol for transcription services."""

    async def load_model(self, model_name: str | None = None) -> bool:
        """Load a transcription model."""
        ...

    def unload_model(self) -> None:
        """Unload the current model."""
        ...

    def is_loaded(self) -> bool:
        """Check if a model is loaded."""
        ...

    def get_current_model(self) -> str | None:
        """Get the name of the currently loaded model."""
        ...

    def get_current_device(self) -> str | None:
        """Get the current compute device."""
        ...

    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
        initial_prompt: str | None = None,
        word_timestamps: bool = False,
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
    ) -> AsyncGenerator[DownloadProgress]:
        """Download a model with progress updates."""
        ...

    def delete_model(self, model_name: str) -> bool:
        """Delete a downloaded model."""
        ...
