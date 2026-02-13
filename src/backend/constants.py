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
        "description": "Fastest, lowest accuracy. Good for quick testing.",
        "repo_id": "Systran/faster-whisper-tiny",
    },
    "base": {
        "display_name": "Base",
        "size_mb": 145,
        "description": "Fast with reasonable accuracy. Good for general use.",
        "repo_id": "Systran/faster-whisper-base",
    },
    "small": {
        "display_name": "Small",
        "size_mb": 465,
        "description": "Balanced speed and accuracy. Recommended for CPU.",
        "repo_id": "Systran/faster-whisper-small",
    },
    "medium": {
        "display_name": "Medium",
        "size_mb": 1460,
        "description": "High accuracy, moderate speed. Good for GPU.",
        "repo_id": "Systran/faster-whisper-medium",
    },
    "large-v3": {
        "display_name": "Large V3",
        "size_mb": 2950,
        "description": "Best accuracy. Requires GPU for reasonable speed.",
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
        "size_mb": 1510,
        "description": "Distilled large model. Fast with near-large accuracy.",
        "repo_id": "Systran/faster-distil-whisper-large-v3",
    },
}

# Supported language codes for transcription
SUPPORTED_LANGUAGES = {
    "auto", "af", "am", "ar", "as", "az", "ba", "be", "bg", "bn", "bo", "br",
    "bs", "ca", "cs", "cy", "da", "de", "el", "en", "es", "et", "eu", "fa",
    "fi", "fo", "fr", "gl", "gu", "ha", "haw", "he", "hi", "hr", "ht", "hu",
    "hy", "id", "is", "it", "ja", "jw", "ka", "kk", "km", "kn", "ko", "la",
    "lb", "ln", "lo", "lt", "lv", "mg", "mi", "mk", "ml", "mn", "mr", "ms",
    "mt", "my", "ne", "nl", "nn", "no", "oc", "pa", "pl", "ps", "pt", "ro",
    "ru", "sa", "sd", "si", "sk", "sl", "sn", "so", "sq", "sr", "su", "sv",
    "sw", "ta", "te", "tg", "th", "tk", "tl", "tr", "tt", "uk", "ur", "uz",
    "vi", "yi", "yo", "yue", "zh",
}

# Maximum length for initial_prompt
MAX_INITIAL_PROMPT_LENGTH = 1000

# Valid device types
VALID_DEVICES = ("auto", "cuda", "cpu")

# Valid compute types
VALID_COMPUTE_TYPES = ("auto", "float16", "int8", "float32")

# Port range for validation
MIN_PORT = 1024
MAX_PORT = 65535

# Application version
APP_VERSION = "2.0.0"
