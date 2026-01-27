"""Settings management for VoxTether."""

import json
import os
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Optional, Any
import logging

logger = logging.getLogger(__name__)


def get_app_data_path() -> Path:
    """Get the application data folder path."""
    if os.name == "nt":
        # Windows: Use APPDATA
        app_data = os.environ.get("APPDATA", os.path.expanduser("~"))
    else:
        # Linux/Mac: Use XDG config or home
        app_data = os.environ.get("XDG_CONFIG_HOME", os.path.expanduser("~/.config"))
    
    path = Path(app_data) / "VoxTether"
    path.mkdir(parents=True, exist_ok=True)
    return path


def get_models_path() -> Path:
    """Get the models folder path."""
    path = get_app_data_path() / "models"
    path.mkdir(parents=True, exist_ok=True)
    return path


def get_logs_path() -> Path:
    """Get the logs folder path."""
    path = get_app_data_path() / "logs"
    path.mkdir(parents=True, exist_ok=True)
    return path


def get_temp_path() -> Path:
    """Get the temp folder path."""
    path = get_app_data_path() / "temp"
    path.mkdir(parents=True, exist_ok=True)
    return path


def get_recordings_path() -> Path:
    """Get the recordings folder path."""
    path = get_app_data_path() / "recordings"
    path.mkdir(parents=True, exist_ok=True)
    return path


@dataclass
class Settings:
    """Application settings that are persisted to disk."""
    
    # Hotkey configuration
    hotkey: str = "ctrl+shift+space"
    toggle_hotkey: str = "ctrl+shift+t"
    
    # Model configuration
    model_name: str = "small"  # tiny, base, small, medium, large-v3-turbo, distil-large-v3
    language: str = "auto"
    
    # Backend configuration
    compute_type: str = "auto"  # auto, int8, float16, float32
    device: str = "auto"  # auto, cuda, cpu
    
    # UI configuration
    show_notifications: bool = True
    show_recording_indicator: bool = True
    play_sounds: bool = True
    
    # Output configuration
    output_mode: str = "clipboard"  # clipboard, focused_app
    
    # System configuration
    start_with_windows: bool = False
    
    # Advanced configuration
    save_recordings: bool = False
    recordings_path: Optional[str] = None
    
    # Internal state (not directly configurable)
    first_run_completed: bool = False
    
    def to_dict(self) -> dict[str, Any]:
        """Convert settings to dictionary."""
        return asdict(self)
    
    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "Settings":
        """Create settings from dictionary."""
        # Filter out unknown fields
        known_fields = {f.name for f in cls.__dataclass_fields__.values()}
        filtered_data = {k: v for k, v in data.items() if k in known_fields}
        return cls(**filtered_data)


class SettingsService:
    """Service for loading and saving settings."""
    
    def __init__(self, settings_path: Optional[Path] = None):
        """Initialize the settings service.
        
        Args:
            settings_path: Optional custom path for settings file.
        """
        self._settings_path = settings_path or (get_app_data_path() / "settings.json")
        self._settings: Settings = self._load()
    
    @property
    def settings(self) -> Settings:
        """Get the current settings."""
        return self._settings
    
    @property
    def settings_path(self) -> Path:
        """Get the path to the settings file."""
        return self._settings_path
    
    def _load(self) -> Settings:
        """Load settings from disk."""
        try:
            if self._settings_path.exists():
                with open(self._settings_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                return Settings.from_dict(data)
        except Exception as e:
            logger.warning(f"Failed to load settings: {e}")
        
        return Settings()
    
    def save(self) -> None:
        """Save the current settings to disk."""
        try:
            self._settings_path.parent.mkdir(parents=True, exist_ok=True)
            with open(self._settings_path, "w", encoding="utf-8") as f:
                json.dump(self._settings.to_dict(), f, indent=2)
        except Exception as e:
            logger.error(f"Failed to save settings: {e}")
    
    def reload(self) -> None:
        """Reload settings from disk."""
        self._settings = self._load()
    
    def update(self, **kwargs: Any) -> None:
        """Update settings and save to disk."""
        for key, value in kwargs.items():
            if hasattr(self._settings, key):
                setattr(self._settings, key, value)
        self.save()
