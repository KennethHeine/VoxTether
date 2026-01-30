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

# Valid device types
VALID_DEVICES = ("auto", "cuda", "cpu")

# Valid compute types
VALID_COMPUTE_TYPES = ("auto", "float16", "int8", "float32")

# Port range for validation
MIN_PORT = 1024
MAX_PORT = 65535

# Application version
APP_VERSION = "2.0.0"
