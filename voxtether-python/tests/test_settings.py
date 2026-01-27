"""Tests for the settings module."""

import json
import tempfile
from pathlib import Path

import pytest

from src.settings import Settings, SettingsService, get_app_data_path


class TestSettings:
    """Tests for the Settings dataclass."""
    
    def test_default_settings(self):
        """Test default settings values."""
        settings = Settings()
        
        assert settings.hotkey == "ctrl+shift+space"
        assert settings.model_name == "small"
        assert settings.language == "auto"
        assert settings.device == "auto"
        assert settings.compute_type == "auto"
        assert settings.show_notifications is True
        assert settings.output_mode == "clipboard"
    
    def test_settings_to_dict(self):
        """Test converting settings to dictionary."""
        settings = Settings(hotkey="ctrl+alt+space", model_name="base")
        
        data = settings.to_dict()
        
        assert data["hotkey"] == "ctrl+alt+space"
        assert data["model_name"] == "base"
    
    def test_settings_from_dict(self):
        """Test creating settings from dictionary."""
        data = {
            "hotkey": "ctrl+alt+space",
            "model_name": "medium",
            "language": "en",
        }
        
        settings = Settings.from_dict(data)
        
        assert settings.hotkey == "ctrl+alt+space"
        assert settings.model_name == "medium"
        assert settings.language == "en"
        # Should use default for unspecified fields
        assert settings.device == "auto"
    
    def test_settings_from_dict_ignores_unknown_fields(self):
        """Test that unknown fields are ignored."""
        data = {
            "hotkey": "ctrl+shift+space",
            "unknown_field": "value",
            "another_unknown": 123,
        }
        
        settings = Settings.from_dict(data)
        
        assert settings.hotkey == "ctrl+shift+space"
        assert not hasattr(settings, "unknown_field")


class TestSettingsService:
    """Tests for the SettingsService class."""
    
    def test_load_default_settings(self):
        """Test loading default settings when no file exists."""
        with tempfile.TemporaryDirectory() as tmpdir:
            settings_path = Path(tmpdir) / "settings.json"
            
            service = SettingsService(settings_path)
            
            assert service.settings.hotkey == "ctrl+shift+space"
            assert not settings_path.exists()
    
    def test_save_and_load_settings(self):
        """Test saving and loading settings."""
        with tempfile.TemporaryDirectory() as tmpdir:
            settings_path = Path(tmpdir) / "settings.json"
            
            # Save settings
            service = SettingsService(settings_path)
            service.update(hotkey="alt+space", model_name="tiny")
            
            # Load in new service instance
            service2 = SettingsService(settings_path)
            
            assert service2.settings.hotkey == "alt+space"
            assert service2.settings.model_name == "tiny"
    
    def test_update_settings(self):
        """Test updating settings."""
        with tempfile.TemporaryDirectory() as tmpdir:
            settings_path = Path(tmpdir) / "settings.json"
            
            service = SettingsService(settings_path)
            service.update(
                hotkey="ctrl+alt+space",
                show_notifications=False,
            )
            
            assert service.settings.hotkey == "ctrl+alt+space"
            assert service.settings.show_notifications is False
    
    def test_reload_settings(self):
        """Test reloading settings from disk."""
        with tempfile.TemporaryDirectory() as tmpdir:
            settings_path = Path(tmpdir) / "settings.json"
            
            # Create initial settings
            service = SettingsService(settings_path)
            service.update(hotkey="ctrl+space")
            
            # Modify file directly
            with open(settings_path, "r") as f:
                data = json.load(f)
            data["hotkey"] = "alt+space"
            with open(settings_path, "w") as f:
                json.dump(data, f)
            
            # Reload
            service.reload()
            
            assert service.settings.hotkey == "alt+space"
    
    def test_handles_corrupted_settings_file(self):
        """Test that corrupted settings file doesn't crash."""
        with tempfile.TemporaryDirectory() as tmpdir:
            settings_path = Path(tmpdir) / "settings.json"
            
            # Write invalid JSON
            with open(settings_path, "w") as f:
                f.write("not valid json{{{")
            
            # Should load default settings
            service = SettingsService(settings_path)
            
            assert service.settings.hotkey == "ctrl+shift+space"
