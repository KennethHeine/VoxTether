"""Tests for the ModelManager service."""

import pytest
from pathlib import Path
from unittest.mock import MagicMock, patch, AsyncMock

from services.model_manager import ModelManager
from constants import AVAILABLE_MODELS
from exceptions import ModelNotFoundError


class TestModelManager:
    """Test suite for ModelManager."""

    def test_initialization(self, temp_models_dir):
        """Test model manager initialization."""
        manager = ModelManager(str(temp_models_dir))
        
        assert manager.models_path == temp_models_dir
        assert temp_models_dir.exists()

    def test_list_models_empty(self, temp_models_dir):
        """Test listing models when none are downloaded."""
        manager = ModelManager(str(temp_models_dir))
        
        models = manager.list_models()
        
        # Should list all available models
        assert len(models) == len(AVAILABLE_MODELS)
        
        # All should be marked as not downloaded
        for model in models:
            assert not model["downloaded"]
            assert model["name"] in AVAILABLE_MODELS

    def test_list_models_with_downloaded(self, temp_models_dir):
        """Test listing models when some are downloaded."""
        manager = ModelManager(str(temp_models_dir))
        
        # Create a fake model directory with model.bin
        model_dir = temp_models_dir / "small"
        model_dir.mkdir()
        (model_dir / "model.bin").touch()
        
        models = manager.list_models()
        
        # Find the small model
        small_model = next(m for m in models if m["name"] == "small")
        assert small_model["downloaded"]
        assert small_model["path"] == str(model_dir)

    def test_is_model_downloaded(self, temp_models_dir):
        """Test checking if a model is downloaded."""
        manager = ModelManager(str(temp_models_dir))
        
        # Model not downloaded
        assert not manager.is_model_downloaded("small")
        
        # Create the model
        model_dir = temp_models_dir / "small"
        model_dir.mkdir()
        (model_dir / "model.bin").touch()
        
        # Now it should be downloaded
        assert manager.is_model_downloaded("small")

    def test_get_model_path(self, temp_models_dir):
        """Test _get_model_path method."""
        manager = ModelManager(str(temp_models_dir))
        
        # Model not found
        assert manager._get_model_path("small") is None
        
        # Create the model
        model_dir = temp_models_dir / "small"
        model_dir.mkdir()
        (model_dir / "model.bin").touch()
        
        # Now it should be found
        path = manager._get_model_path("small")
        assert path == model_dir

    def test_delete_model(self, temp_models_dir):
        """Test deleting a model."""
        manager = ModelManager(str(temp_models_dir))
        
        # Create a model
        model_dir = temp_models_dir / "small"
        model_dir.mkdir()
        (model_dir / "model.bin").touch()
        
        # Delete it
        result = manager.delete_model("small")
        
        assert result is True
        assert not model_dir.exists()

    def test_delete_nonexistent_model(self, temp_models_dir):
        """Test deleting a model that doesn't exist."""
        manager = ModelManager(str(temp_models_dir))
        
        result = manager.delete_model("nonexistent")
        
        assert result is False

    @pytest.mark.asyncio
    async def test_download_model_unknown(self, temp_models_dir):
        """Test downloading an unknown model."""
        manager = ModelManager(str(temp_models_dir))
        
        with pytest.raises(ModelNotFoundError):
            async for _ in manager.download_model_async("unknown_model"):
                pass

    @pytest.mark.asyncio
    async def test_download_model_success(self, temp_models_dir):
        """Test successful model download."""
        manager = ModelManager(str(temp_models_dir))
        
        with patch("services.model_manager.snapshot_download") as mock_download:
            # Mock successful download
            target_path = temp_models_dir / "small"
            mock_download.return_value = str(target_path)
            
            # Create the directory to simulate download
            target_path.mkdir()
            (target_path / "model.bin").touch()
            
            statuses = []
            async for progress in manager.download_model_async("small"):
                statuses.append(progress.status)
            
            # Should have downloading and complete statuses
            assert "downloading" in statuses
            assert "complete" in statuses

    @pytest.mark.asyncio
    async def test_download_model_error(self, temp_models_dir):
        """Test model download failure."""
        manager = ModelManager(str(temp_models_dir))
        
        with patch("services.model_manager.snapshot_download") as mock_download:
            # Mock download failure
            mock_download.side_effect = Exception("Network error")
            
            statuses = []
            try:
                async for progress in manager.download_model_async("small"):
                    statuses.append(progress.status)
            except Exception:
                pass  # Expected
            
            # Should have error status
            assert any(s == "error" for s in statuses)
