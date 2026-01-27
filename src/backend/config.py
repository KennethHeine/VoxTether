"""Configuration settings for the VoxTether backend."""

import os
from pathlib import Path
from typing import Optional

from pydantic import Field
from pydantic_settings import BaseSettings


def get_default_models_path() -> str:
    """Get the default models path."""
    appdata = os.environ.get("APPDATA", "")
    if appdata:
        return os.path.join(appdata, "VoxTether", "models")
    # Fallback for non-Windows
    return os.path.join(Path.home(), ".voxtether", "models")


class Settings(BaseSettings):
    """Backend configuration settings."""
    
    # Server settings
    host: str = Field(default="127.0.0.1", description="Host to bind to")
    port: int = Field(default=5678, description="Port to bind to")
    debug: bool = Field(default=False, description="Enable debug mode")
    
    # Model settings
    models_path: str = Field(default_factory=get_default_models_path, description="Path to models directory")
    default_model: str = Field(default="small", description="Default model to use")
    preload_model: bool = Field(default=True, description="Preload the default model on startup")
    
    # Transcription settings
    device: str = Field(default="auto", description="Device to use (auto, cuda, cpu)")
    compute_type: str = Field(default="auto", description="Compute type (auto, float16, int8, float32)")
    default_language: str = Field(default="auto", description="Default language for transcription")
    
    class Config:
        env_prefix = "VOXTETHER_"
        env_file = ".env"
        extra = "ignore"


# Global settings instance
settings = Settings()

# Ensure models directory exists
os.makedirs(settings.models_path, exist_ok=True)
