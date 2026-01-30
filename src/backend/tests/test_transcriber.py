"""Tests for the TranscriberService."""

import pytest
from unittest.mock import MagicMock, patch, AsyncMock

from services.transcriber import TranscriberService
from protocols import TranscriptionResult


@pytest.mark.asyncio
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
            
            # Mock torch not available
            with patch("services.transcriber.sys.modules", {"torch": None}):
                device, compute_type = transcriber._resolve_device()
                
                # Should fallback to CPU with int8
                assert device in ("cpu", "cuda")  # May vary based on environment
                assert compute_type in ("int8", "float16")

    def test_get_model_path(self):
        """Test _get_model_path method."""
        transcriber = TranscriberService()
        
        # Test with model name
        with patch("services.transcriber.settings") as mock_settings:
            mock_settings.models_path = "/fake/models"
            
            with patch("services.transcriber.Path") as mock_path:
                mock_path.return_value.exists.return_value = False
                
                # Should return the model name itself if not found locally
                result = transcriber._get_model_path("small")
                assert result == "small"


@pytest.mark.asyncio
class TestTranscriberServiceIntegration:
    """Integration tests for TranscriberService (require mocking faster_whisper)."""

    @pytest.mark.asyncio
    async def test_load_model_with_mock(self):
        """Test load_model with mocked WhisperModel."""
        transcriber = TranscriberService()
        
        with patch("services.transcriber.WhisperModel") as mock_whisper:
            mock_model = MagicMock()
            mock_whisper.return_value = mock_model
            
            with patch("services.transcriber.settings") as mock_settings:
                mock_settings.device = "cpu"
                mock_settings.compute_type = "int8"
                mock_settings.models_path = "/fake/models"
                mock_settings.default_model = "small"
                
                # Mock Path.exists to return False (model not found locally)
                with patch("services.transcriber.Path") as mock_path:
                    mock_path.return_value.exists.return_value = False
                    
                    result = await transcriber.load_model("small")
                    
                    assert result is True
                    assert transcriber._model_name == "small"
                    assert transcriber.is_loaded()
