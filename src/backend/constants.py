"""Constants and configuration values for VoxTether backend."""

from typing import Dict, Any

# Default transcription settings
DEFAULT_BEAM_SIZE = 5
DEFAULT_TIMEOUT_SECONDS = 120
DEFAULT_VAD_FILTER = True

# Temporary file settings
TEMP_AUDIO_SUFFIX = ".wav"

# Available Whisper models with their metadata
AVAILABLE_MODELS: Dict[str, Dict[str, Any]] = {
    "large-v3-turbo": {
        "display_name": "Large V3 Turbo",
        "size_mb": 1600,
        "description": "Excellent accuracy with faster speed. Great GPU option.",
        "repo_id": "deepdml/faster-whisper-large-v3-turbo-ct2",
    },
}

# Valid device types
VALID_DEVICES = ("auto", "cuda", "cpu")

# Valid compute types
VALID_COMPUTE_TYPES = ("auto", "float16", "int8", "float32")

# Port range for validation
MIN_PORT = 1024
MAX_PORT = 65535

# Application version
APP_VERSION = "2.0.0"
