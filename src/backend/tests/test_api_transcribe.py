"""Tests for transcription API endpoints."""

import pytest
from fastapi.testclient import TestClient

from config import settings
from main import app


@pytest.fixture
def client(mock_transcriber):
    """Create test client with mocked transcriber.
    
    Args:
        mock_transcriber: Mock transcriber fixture.
        
    Returns:
        TestClient instance.
    """
    app.state.transcriber = mock_transcriber
    return TestClient(app)


def test_transcribe_success(client, mock_transcriber, sample_audio_file):
    """Test successful transcription."""
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
            data={"language": "auto", "translate": "false"},
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
    assert data["text"] == "Test transcription"
    assert data["language"] == "en"


def test_transcribe_with_language(client, mock_transcriber, sample_audio_file):
    """Test transcription with specific language."""
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
            data={"language": "en"},
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True


def test_transcribe_with_translate(client, mock_transcriber, sample_audio_file):
    """Test transcription with translation to English."""
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
            data={"language": "de", "translate": "true"},
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True


def test_transcribe_no_model(client, mock_transcriber, sample_audio_file):
    """Test transcription when no model is loaded."""
    mock_transcriber.is_loaded.return_value = False
    
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
        )
    
    assert response.status_code == 503
    data = response.json()
    assert "detail" in data


def test_transcribe_invalid_file_type(client, mock_transcriber, tmp_path):
    """Test transcription with invalid file type."""
    text_file = tmp_path / "test.txt"
    text_file.write_text("not audio")
    
    with open(text_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.txt", f, "text/plain")},
        )
    
    assert response.status_code == 400
    data = response.json()
    assert "Invalid file type" in data["detail"]


def test_transcribe_valid_extensions(client, mock_transcriber, sample_audio_file):
    """Test transcription accepts various audio extensions."""
    valid_extensions = [".wav", ".mp3", ".flac", ".ogg", ".m4a", ".webm"]
    
    for ext in valid_extensions:
        with open(sample_audio_file, "rb") as f:
            response = client.post(
                "/api/transcribe",
                files={"file": (f"test{ext}", f, "audio/wav")},
            )
        
        assert response.status_code == 200, f"Failed for extension {ext}"


def test_transcribe_file_too_large(client, mock_transcriber, tmp_path, monkeypatch):
    """Test transcription rejects files that are too large."""
    # Temporarily set a very small limit
    monkeypatch.setattr(settings, "max_upload_size_mb", 1)  # 1 MB limit
    
    # Create a file larger than 1 MB
    large_file = tmp_path / "large.wav"
    large_file.write_bytes(b"x" * (2 * 1024 * 1024))  # 2 MB
    
    with open(large_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("large.wav", f, "audio/wav")},
        )
    
    assert response.status_code == 413
    data = response.json()
    assert "File too large" in data["detail"]


def test_transcribe_with_initial_prompt(client, mock_transcriber, sample_audio_file):
    """Test transcription with initial prompt."""
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
            data={
                "language": "auto",
                "initial_prompt": "VoxTether, transcription, API",
            },
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True


def test_transcribe_with_word_timestamps(client, mock_transcriber, sample_audio_file):
    """Test transcription with word timestamps option."""
    with open(sample_audio_file, "rb") as f:
        response = client.post(
            "/api/transcribe",
            files={"file": ("test.wav", f, "audio/wav")},
            data={"word_timestamps": "true"},
        )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
    # Note: words may be None if mock doesn't return them
