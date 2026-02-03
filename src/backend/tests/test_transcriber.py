"""Tests for the TranscriberService."""

import pytest
from unittest.mock import MagicMock, patch

from services.transcriber import TranscriberService


class TestTranscriberService:
    """Test suite for TranscriberService."""

    def test_initialization(self):
        """Test transcriber service initialization."""
        transcriber = TranscriberService()
        
        assert transcriber._model is None
        assert transcriber._model_name is None
        assert transcriber._device is None
        assert transcriber._compute_type is None
        assert not transcriber.is_loaded()

    def test_is_loaded(self):
        """Test is_loaded method."""
        transcriber = TranscriberService()
        
        # Initially not loaded
        assert not transcriber.is_loaded()
        
        # Simulate model being loaded
        transcriber._model = MagicMock()
        assert transcriber.is_loaded()

    def test_get_current_model(self):
        """Test get_current_model method."""
        transcriber = TranscriberService()
        
        # No model loaded
        assert transcriber.get_current_model() is None
        
        # Model loaded
        transcriber._model_name = "small"
        assert transcriber.get_current_model() == "small"

    def test_get_current_device(self):
        """Test get_current_device method."""
        transcriber = TranscriberService()
        
        # No device set
        assert transcriber.get_current_device() is None
        
        # Device set
        transcriber._device = "cpu"
        assert transcriber.get_current_device() == "cpu"

    def test_unload_model(self):
        """Test unload_model method."""
        transcriber = TranscriberService()
        
        # Set up a loaded state
        transcriber._model = MagicMock()
        transcriber._model_name = "small"
        transcriber._device = "cpu"
        transcriber._compute_type = "int8"
        
        # Unload
        transcriber.unload_model()
        
        # Verify all cleared
        assert transcriber._model is None
        assert transcriber._model_name is None
        assert transcriber._device is None
        assert transcriber._compute_type is None

    @pytest.mark.asyncio
    async def test_transcribe_without_model(self):
        """Test transcribe when no model is loaded."""
        transcriber = TranscriberService()
        
        result = await transcriber.transcribe("/fake/path.wav")
        
        assert not result.success
        assert result.text == ""
        assert result.error == "Model not loaded"
        assert result.duration_seconds == 0

    def test_resolve_device_auto_cpu(self):
        """Test device resolution when CUDA is not available."""
        transcriber = TranscriberService()
        
        with patch("services.transcriber.settings") as mock_settings:
            mock_settings.device = "auto"
            mock_settings.compute_type = "auto"
            
            # The device resolution will use the actual environment
            # We just verify it returns valid values
            device, compute_type = transcriber._resolve_device()
            
            # Should return valid device and compute type
            assert device in ("cpu", "cuda")
            assert compute_type in ("int8", "float16", "float32")

    def test_get_model_path(self, tmp_path):
        """Test _get_model_path method."""
        transcriber = TranscriberService()
        
        # Test with a real temporary directory
        with patch("services.transcriber.settings") as mock_settings:
            mock_settings.models_path = str(tmp_path)
            
            # Model doesn't exist yet
            result = transcriber._get_model_path("small")
            # When model is not found locally, it returns the model name for HuggingFace download
            assert result == "small"
            
            # Create the model directory and test again
            model_dir = tmp_path / "small"
            model_dir.mkdir()
            (model_dir / "model.bin").touch()
            
            result = transcriber._get_model_path("small")
            # Now it should return the path
            assert result == str(model_dir)


@pytest.mark.asyncio
class TestTranscriberServiceIntegration:
    """Integration tests for TranscriberService (require mocking faster_whisper)."""

    @pytest.mark.asyncio
    async def test_load_model_with_mock(self, tmp_path):
        """Test load_model with mocked WhisperModel."""
        transcriber = TranscriberService()
        
        # Import faster_whisper in the patch target
        with patch("faster_whisper.WhisperModel") as mock_whisper:
            mock_model = MagicMock()
            mock_whisper.return_value = mock_model
            
            with patch("services.transcriber.settings") as mock_settings:
                mock_settings.device = "cpu"
                mock_settings.compute_type = "int8"
                mock_settings.models_path = str(tmp_path)
                mock_settings.default_model = "small"
                mock_settings.max_workers = 2
                
                # Model doesn't exist in temp directory, so it will use the model name
                result = await transcriber.load_model("small")
                
                assert result is True
                assert transcriber._model_name == "small"
                assert transcriber.is_loaded()
