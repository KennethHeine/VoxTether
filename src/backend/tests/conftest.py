"""Pytest configuration and fixtures for VoxTether backend tests."""

import pytest
from unittest.mock import MagicMock, AsyncMock


@pytest.fixture
def temp_models_dir(tmp_path):
    """Create a temporary models directory.
    
    Args:
        tmp_path: pytest tmp_path fixture.
        
    Returns:
        Path to temporary models directory.
    """
    models_dir = tmp_path / "models"
    models_dir.mkdir()
    return models_dir


@pytest.fixture
def mock_transcriber():
    """Create a mock transcriber service.
    
    Returns:
        Mock TranscriberService instance.
    """
    transcriber = MagicMock()
    transcriber.is_loaded.return_value = True
    transcriber.get_current_model.return_value = "small"
    transcriber.get_current_device.return_value = "cpu"
    transcriber.load_model = AsyncMock(return_value=True)
    transcriber.unload_model = MagicMock()
    
    # Mock transcribe method with new parameters
    async def mock_transcribe(
        audio_path,
        language="auto",
        task="transcribe",
        initial_prompt=None,
        word_timestamps=False,
    ):
        from protocols import TranscriptionResult
        return TranscriptionResult(
            text="Test transcription",
            success=True,
            duration_seconds=1.5,
            language="en",
        )
    
    transcriber.transcribe = mock_transcribe
    
    return transcriber


@pytest.fixture
def mock_model_manager(temp_models_dir):
    """Create a mock model manager.
    
    Args:
        temp_models_dir: Temporary models directory fixture.
        
    Returns:
        Mock ModelManager instance.
    """
    from services.model_manager import ModelManager
    
    manager = ModelManager(str(temp_models_dir))
    
    # Mock the download method
    async def mock_download(model_name):
        from schemas import DownloadProgress
        yield DownloadProgress(
            status="downloading",
            progress=50.0,
            downloaded_mb=50.0,
            total_mb=100.0,
        )
        yield DownloadProgress(
            status="complete",
            progress=100.0,
            downloaded_mb=100.0,
            total_mb=100.0,
        )
    
    manager.download_model_async = mock_download
    
    return manager


@pytest.fixture
def sample_audio_file(tmp_path):
    """Create a sample WAV audio file for testing.
    
    Args:
        tmp_path: pytest tmp_path fixture.
        
    Returns:
        Path to sample audio file.
    """
    # Create a minimal valid WAV file (44 bytes header + 1 sample)
    audio_file = tmp_path / "test.wav"
    
    # Minimal WAV file header (RIFF format)
    wav_header = bytes([
        # RIFF header
        0x52, 0x49, 0x46, 0x46,  # "RIFF"
        0x24, 0x00, 0x00, 0x00,  # File size - 8
        0x57, 0x41, 0x56, 0x45,  # "WAVE"
        # fmt chunk
        0x66, 0x6D, 0x74, 0x20,  # "fmt "
        0x10, 0x00, 0x00, 0x00,  # Chunk size (16)
        0x01, 0x00,              # Audio format (1 = PCM)
        0x01, 0x00,              # Number of channels (1)
        0x44, 0xAC, 0x00, 0x00,  # Sample rate (44100)
        0x88, 0x58, 0x01, 0x00,  # Byte rate
        0x02, 0x00,              # Block align
        0x10, 0x00,              # Bits per sample (16)
        # data chunk
        0x64, 0x61, 0x74, 0x61,  # "data"
        0x00, 0x00, 0x00, 0x00,  # Data size (0)
    ])
    
    audio_file.write_bytes(wav_header)
    return audio_file
