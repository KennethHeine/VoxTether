"""Custom exceptions for VoxTether backend."""


class VoxTetherError(Exception):
    """Base exception for VoxTether backend."""

    def __init__(self, message: str, status_code: int = 500):
        self.message = message
        self.status_code = status_code
        super().__init__(message)


class ModelNotLoadedError(VoxTetherError):
    """Raised when attempting to use a model that is not loaded."""

    def __init__(self, message: str = "Model not loaded"):
        super().__init__(message, status_code=503)


class ModelNotFoundError(VoxTetherError):
    """Raised when a requested model is not found."""

    def __init__(self, model_name: str):
        super().__init__(f"Model '{model_name}' not found", status_code=404)


class TranscriptionError(VoxTetherError):
    """Raised when transcription fails."""

    def __init__(self, message: str):
        super().__init__(f"Transcription failed: {message}", status_code=500)


class ModelDownloadError(VoxTetherError):
    """Raised when model download fails."""

    def __init__(self, model_name: str, reason: str):
        super().__init__(
            f"Failed to download model '{model_name}': {reason}", status_code=500
        )


class InvalidConfigurationError(VoxTetherError):
    """Raised when configuration is invalid."""

    def __init__(self, message: str):
        super().__init__(f"Invalid configuration: {message}", status_code=500)
