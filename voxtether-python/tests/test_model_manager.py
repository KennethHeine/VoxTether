"""Tests for the model manager module."""

import tempfile
from pathlib import Path

import pytest

from src.model_manager import ModelManager, AVAILABLE_MODELS, ModelInfo


class TestModelInfo:
    """Tests for the ModelInfo dataclass."""
    
    def test_available_models_exist(self):
        """Test that all expected models are available."""
        expected_models = [
            "tiny", "base", "small", "medium",
            "large-v3", "large-v3-turbo", "distil-large-v3",
        ]
        
        for model_name in expected_models:
            assert model_name in AVAILABLE_MODELS
            
            model_info = AVAILABLE_MODELS[model_name]
            assert isinstance(model_info, ModelInfo)
            assert model_info.name == model_name
            assert model_info.repo_id.startswith("Systran/")
            assert model_info.size_mb > 0


class TestModelManager:
    """Tests for the ModelManager class."""
    
    def test_init_creates_models_path(self):
        """Test that initialization creates the models directory."""
        with tempfile.TemporaryDirectory() as tmpdir:
            models_path = Path(tmpdir) / "models"
            
            manager = ModelManager(models_path)
            
            assert manager.models_path == models_path
    
    def test_get_available_models(self):
        """Test getting available models."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            models = manager.get_available_models()
            
            assert "small" in models
            assert "base" in models
            assert len(models) >= 5
    
    def test_get_model_info(self):
        """Test getting model info."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            info = manager.get_model_info("small")
            
            assert info is not None
            assert info.name == "small"
            assert "accuracy" in info.description.lower() or "speed" in info.description.lower()
    
    def test_get_model_info_unknown_model(self):
        """Test getting info for unknown model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            info = manager.get_model_info("nonexistent-model")
            
            assert info is None
    
    def test_is_model_downloaded_false(self):
        """Test that is_model_downloaded returns False for non-downloaded model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            assert manager.is_model_downloaded("small") is False
    
    def test_is_model_downloaded_true(self):
        """Test that is_model_downloaded returns True for downloaded model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            models_path = Path(tmpdir)
            manager = ModelManager(models_path)
            
            # Simulate downloaded model
            model_dir = models_path / "Systran--faster-whisper-small"
            model_dir.mkdir(parents=True)
            (model_dir / "model.bin").touch()
            
            assert manager.is_model_downloaded("small") is True
    
    def test_get_downloaded_models_empty(self):
        """Test getting downloaded models when none exist."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            downloaded = manager.get_downloaded_models()
            
            assert downloaded == []
    
    def test_get_downloaded_models(self):
        """Test getting downloaded models."""
        with tempfile.TemporaryDirectory() as tmpdir:
            models_path = Path(tmpdir)
            manager = ModelManager(models_path)
            
            # Simulate downloaded models
            for name in ["small", "base"]:
                info = AVAILABLE_MODELS[name]
                model_dir = models_path / info.repo_id.replace("/", "--")
                model_dir.mkdir(parents=True)
                (model_dir / "model.bin").touch()
            
            downloaded = manager.get_downloaded_models()
            
            assert "small" in downloaded
            assert "base" in downloaded
            assert len(downloaded) == 2
    
    def test_get_model_for_transcriber_downloaded(self):
        """Test getting model path for downloaded model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            models_path = Path(tmpdir)
            manager = ModelManager(models_path)
            
            # Simulate downloaded model
            model_dir = models_path / "Systran--faster-whisper-small"
            model_dir.mkdir(parents=True)
            (model_dir / "model.bin").touch()
            
            result = manager.get_model_for_transcriber("small")
            
            assert str(model_dir) == result
    
    def test_get_model_for_transcriber_not_downloaded(self):
        """Test getting model path for non-downloaded model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            result = manager.get_model_for_transcriber("small")
            
            # Should return the HuggingFace repo ID
            assert result == "Systran/faster-whisper-small"
    
    def test_get_model_for_transcriber_unknown(self):
        """Test getting model path for unknown model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            with pytest.raises(ValueError, match="Unknown model"):
                manager.get_model_for_transcriber("unknown-model")
    
    def test_delete_model(self):
        """Test deleting a downloaded model."""
        with tempfile.TemporaryDirectory() as tmpdir:
            models_path = Path(tmpdir)
            manager = ModelManager(models_path)
            
            # Simulate downloaded model
            model_dir = models_path / "Systran--faster-whisper-small"
            model_dir.mkdir(parents=True)
            (model_dir / "model.bin").touch()
            
            assert manager.is_model_downloaded("small") is True
            
            result = manager.delete_model("small")
            
            assert result is True
            assert manager.is_model_downloaded("small") is False
    
    def test_delete_model_not_downloaded(self):
        """Test deleting a model that's not downloaded."""
        with tempfile.TemporaryDirectory() as tmpdir:
            manager = ModelManager(Path(tmpdir))
            
            result = manager.delete_model("small")
            
            assert result is False
