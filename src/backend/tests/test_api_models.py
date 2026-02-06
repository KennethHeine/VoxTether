"""Tests for model management API endpoints."""

import pytest
from fastapi.testclient import TestClient

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


def test_list_models(client, mock_transcriber):
    """Test listing available models."""
    response = client.get("/api/models")
    
    assert response.status_code == 200
    data = response.json()
    assert "models" in data
    assert isinstance(data["models"], list)
    assert "current_model" in data


def test_list_models_with_loaded_model(client, mock_transcriber):
    """Test listing models when a model is loaded."""
    mock_transcriber.get_current_model.return_value = "small"
    
    response = client.get("/api/models")
    
    assert response.status_code == 200
    data = response.json()
    assert data["current_model"] == "small"


def test_list_models_no_model_loaded(client, mock_transcriber):
    """Test listing models when no model is loaded."""
    mock_transcriber.get_current_model.return_value = None
    
    response = client.get("/api/models")
    
    assert response.status_code == 200
    data = response.json()
    assert data["current_model"] is None


def test_load_model(client, mock_transcriber):
    """Test loading a model."""
    response = client.post("/api/models/small/load")
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
    assert data["model"] == "small"
    assert "loaded successfully" in data["message"]


def test_load_model_calls_transcriber(client, mock_transcriber):
    """Test that loading a model calls the transcriber service."""
    client.post("/api/models/medium/load")
    
    mock_transcriber.load_model.assert_called_once_with("medium")


def test_unload_model(client, mock_transcriber):
    """Test unloading a model."""
    mock_transcriber.get_current_model.return_value = "small"
    
    response = client.post("/api/models/small/unload")
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
    assert data["model"] == "small"
    mock_transcriber.unload_model.assert_called_once()


def test_unload_model_wrong_model(client, mock_transcriber):
    """Test unloading a model that's not the currently loaded model."""
    mock_transcriber.get_current_model.return_value = "small"
    
    response = client.post("/api/models/medium/unload")
    
    assert response.status_code == 400
    data = response.json()
    assert "Cannot unload" in data["detail"]


def test_unload_model_no_model_loaded(client, mock_transcriber):
    """Test unloading when no model is loaded."""
    mock_transcriber.get_current_model.return_value = None
    
    response = client.post("/api/models/small/unload")
    
    # Should succeed even if no model is loaded
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True


def test_delete_model_invalid_name(client):
    """Test deleting a model with an invalid name."""
    response = client.delete("/api/models/nonexistent")
    
    assert response.status_code == 400
    data = response.json()
    assert "Unknown model" in data["detail"]


def test_delete_model_not_downloaded(client):
    """Test deleting a valid model that isn't downloaded."""
    response = client.delete("/api/models/small")
    
    assert response.status_code == 404


def test_load_model_invalid_name(client, mock_transcriber):
    """Test loading a model with an invalid name."""
    response = client.post("/api/models/nonexistent/load")
    
    assert response.status_code == 400
    data = response.json()
    assert "Unknown model" in data["detail"]


def test_unload_model_invalid_name(client, mock_transcriber):
    """Test unloading a model with an invalid name."""
    response = client.post("/api/models/nonexistent/unload")
    
    assert response.status_code == 400
    data = response.json()
    assert "Unknown model" in data["detail"]
