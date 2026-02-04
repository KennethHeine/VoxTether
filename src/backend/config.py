"""Configuration settings for the VoxTether backend."""

import os
from pathlib import Path

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings

from constants import VALID_DEVICES, VALID_COMPUTE_TYPES, MIN_PORT, MAX_PORT


def get_default_models_path() -> str:
    """Get the default models path."""
    appdata = os.environ.get("APPDATA", "")
    if appdata:
        return os.path.join(appdata, "VoxTether", "models")
    # Fallback for non-Windows
    return os.path.join(Path.home(), ".voxtether", "models")


def get_default_logs_path() -> str:
    """Get the default logs path."""
    appdata = os.environ.get("APPDATA", "")
    if appdata:
        return os.path.join(appdata, "VoxTether", "logs")
    # Fallback for non-Windows
    return os.path.join(Path.home(), ".voxtether", "logs")


class Settings(BaseSettings):
    """Backend configuration settings."""
    
    # Server settings
    host: str = Field(default="127.0.0.1", description="Host to bind to")
    port: int = Field(default=5678, description="Port to bind to")
    debug: bool = Field(default=False, description="Enable debug mode")
    
    # Logging settings
    logs_path: str = Field(default_factory=get_default_logs_path, description="Path to logs directory")
    
    # Model settings
    models_path: str = Field(default_factory=get_default_models_path, description="Path to models directory")
    default_model: str = Field(default="large-v3-turbo", description="Default model to use")
    preload_model: bool = Field(default=True, description="Preload the default model on startup")
    
    # Transcription settings
    device: str = Field(default="auto", description="Device to use (auto, cuda, cpu)")
    compute_type: str = Field(default="auto", description="Compute type (auto, float16, int8, float32)")
    default_language: str = Field(default="auto", description="Default language for transcription")
    max_workers: int = Field(default=2, description="Max worker threads for transcription")
    max_upload_size_mb: int = Field(default=50, description="Maximum upload size in MB")
    
    model_config = {
        "env_prefix": "VOXTETHER_",
        "env_file": ".env",
        "extra": "ignore",
    }
    
    @field_validator("port")
    @classmethod
    def validate_port(cls, v: int) -> int:
        """Validate port number is in valid range.
        
        Args:
            v: Port number to validate.
            
        Returns:
            Validated port number.
            
        Raises:
            ValueError: If port is out of range.
        """
        if not MIN_PORT <= v <= MAX_PORT:
            raise ValueError(f"Port must be between {MIN_PORT} and {MAX_PORT}")
        return v
    
    @field_validator("device")
    @classmethod
    def validate_device(cls, v: str) -> str:
        """Validate device type.
        
        Args:
            v: Device type to validate.
            
        Returns:
            Validated device type.
            
        Raises:
            ValueError: If device type is invalid.
        """
        if v not in VALID_DEVICES:
            raise ValueError(f"Device must be one of: {', '.join(VALID_DEVICES)}")
        return v
    
    @field_validator("compute_type")
    @classmethod
    def validate_compute_type(cls, v: str) -> str:
        """Validate compute type.
        
        Args:
            v: Compute type to validate.
            
        Returns:
            Validated compute type.
            
        Raises:
            ValueError: If compute type is invalid.
        """
        if v not in VALID_COMPUTE_TYPES:
            raise ValueError(f"Compute type must be one of: {', '.join(VALID_COMPUTE_TYPES)}")
        return v


# Global settings instance
settings = Settings()

# Ensure directories exist
os.makedirs(settings.models_path, exist_ok=True)
os.makedirs(settings.logs_path, exist_ok=True)
