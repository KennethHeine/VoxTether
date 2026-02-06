"""Tests for health check API endpoints."""

import pytest
from fastapi.testclient import TestClient

from constants import APP_VERSION
from main import app


@pytest.fixture
def client():
    """Create a test client.
    
    Returns:
        TestClient instance.
    """
    return TestClient(app)


def test_health_check_no_model(client, mock_transcriber):
    """Test health check when no model is loaded."""
    # Mock transcriber with no model
    mock_transcriber.is_loaded.return_value = False
    mock_transcriber.get_current_model.return_value = None
    mock_transcriber.get_current_device.return_value = None

    app.state.transcriber = mock_transcriber

    response = client.get("/api/health")

    assert response.status_code == 200
    data = response.json()

    assert data["version"] == APP_VERSION
    assert data["model_loaded"] is False
    assert data["model_name"] is None
    assert "uptime_seconds" in data
    assert data["status"] in ("healthy", "degraded", "unhealthy")


def test_health_check_with_model(client, mock_transcriber):
    """Test health check when model is loaded."""
    # Mock transcriber with model loaded
    mock_transcriber.is_loaded.return_value = True
    mock_transcriber.get_current_model.return_value = "small"
    mock_transcriber.get_current_device.return_value = "cpu"

    app.state.transcriber = mock_transcriber

    response = client.get("/api/health")

    assert response.status_code == 200
    data = response.json()

    assert data["version"] == APP_VERSION
    assert data["model_loaded"] is True
    assert data["model_name"] == "small"
    assert data["device"] == "cpu"
    assert data["status"] == "healthy"


def test_devices_endpoint(client):
    """Test devices information endpoint."""
    response = client.get("/api/devices")

    assert response.status_code == 200
    data = response.json()

    assert "cuda_available" in data
    # May or may not have CUDA depending on environment
    assert isinstance(data["cuda_available"], bool)
