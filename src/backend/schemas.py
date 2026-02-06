"""Pydantic schemas for API request/response models."""

from typing import List, Optional
from pydantic import BaseModel, Field, field_validator

from constants import VALID_DEVICES, VALID_COMPUTE_TYPES


# ============================================================================
# Model Management Schemas
# ============================================================================


class ModelInfo(BaseModel):
    """Information about a model."""

    name: str
    display_name: str
    size_mb: int
    downloaded: bool
    path: Optional[str] = None
    description: str = ""


class ModelListResponse(BaseModel):
    """Response model for model list."""

    models: List[ModelInfo]
    current_model: Optional[str] = None


class LoadModelRequest(BaseModel):
    """Request to load a model."""

    model_name: str


class ModelActionResponse(BaseModel):
    """Generic response for model actions."""

    success: bool
    model: Optional[str] = None
    message: Optional[str] = None


# ============================================================================
# Transcription Schemas
# ============================================================================


class WordInfo(BaseModel):
    """Information about a single word with timestamp."""

    word: str = Field(..., description="The word text")
    start: float = Field(..., description="Start time in seconds")
    end: float = Field(..., description="End time in seconds")
    probability: float = Field(..., description="Confidence probability (0-1)")


class TranscriptionResponse(BaseModel):
    """Response model for transcription."""

    text: str
    language: Optional[str] = None
    duration: float
    success: bool
    error: Optional[str] = None
    words: Optional[List[WordInfo]] = Field(None, description="Word-level timestamps if requested")


class TranscriptionSettings(BaseModel):
    """Settings for transcription."""

    device: str = "auto"
    compute_type: str = "auto"
    language: str = "auto"
    model: Optional[str] = None

    @field_validator("device")
    @classmethod
    def validate_device(cls, v: str) -> str:
        """Validate device type."""
        if v not in VALID_DEVICES:
            raise ValueError(f"Device must be one of: {', '.join(VALID_DEVICES)}")
        return v

    @field_validator("compute_type")
    @classmethod
    def validate_compute_type(cls, v: str) -> str:
        """Validate compute type."""
        if v not in VALID_COMPUTE_TYPES:
            raise ValueError(f"Compute type must be one of: {', '.join(VALID_COMPUTE_TYPES)}")
        return v


# ============================================================================
# Health Check Schemas
# ============================================================================


class ComponentHealth(BaseModel):
    """Health status of a component."""

    status: str  # "healthy", "degraded", "unhealthy"
    message: Optional[str] = None


class HealthCheckResponse(BaseModel):
    """Detailed health check response."""

    status: str = Field(
        ..., description="Overall status: healthy, degraded, or unhealthy"
    )
    version: str = Field(..., description="Application version")
    model_loaded: bool = Field(..., description="Whether a model is currently loaded")
    model_name: Optional[str] = Field(None, description="Name of the loaded model")
    device: Optional[str] = Field(None, description="Compute device (cuda/cpu)")
    uptime_seconds: float = Field(..., description="Server uptime in seconds")
    checks: dict = Field(
        default_factory=dict, description="Individual component health checks"
    )


class DeviceInfo(BaseModel):
    """Information about available compute devices."""

    cuda_available: bool
    cuda_version: Optional[str] = None
    device_name: Optional[str] = None


# ============================================================================
# Error Response Schemas
# ============================================================================


class ErrorResponse(BaseModel):
    """Standard error response."""

    error: str = Field(..., description="Error message")
    detail: Optional[str] = Field(None, description="Detailed error information")
    status_code: int = Field(..., description="HTTP status code")


# ============================================================================
# Download Progress Schema
# ============================================================================


class DownloadProgress(BaseModel):
    """Progress update for model download."""

    status: str = Field(..., description="Status: downloading, complete, or error")
    progress: float = Field(0.0, description="Progress percentage (0-100)")
    downloaded_mb: float = Field(0.0, description="Downloaded megabytes")
    total_mb: float = Field(0.0, description="Total megabytes")
    speed_mbps: float = Field(0.0, description="Download speed in MB/s")
    error: Optional[str] = Field(None, description="Error message if status is error")
