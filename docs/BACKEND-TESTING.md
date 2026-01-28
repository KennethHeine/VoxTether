# VoxTether Backend Testing Guide

This document describes how the VoxTether backend application is tested in depth, covering the testing framework, test categories, test organization, and CI/CD integration.

---

## Overview

VoxTether uses **pytest** as the primary testing framework for the Python backend. The test suite covers:

- **Unit tests** for individual modules and classes
- **Integration tests** for CLI tools and component interactions
- **Mocking** for hardware-dependent functionality (audio devices, CUDA)

---

## Test Framework and Tools

| Tool | Version | Purpose |
|------|---------|---------|
| **pytest** | ≥7.4.0 | Test framework and runner |
| **pytest-cov** | ≥4.1.0 | Code coverage reporting |
| **pytest-mock** | ≥3.12.0 | Mocking utilities |
| **ruff** | ≥0.1.0 | Linting and code quality |

### Installation

```bash
# From src/backend directory
cd src/backend

# Install runtime dependencies
pip install -r requirements.txt

# Install development dependencies (includes test tools)
pip install -r ../../requirements-dev.txt
```

---

## Test Directory Structure

```
tests/
├── __init__.py              # Test package initialization
├── test-recoarding.wav      # Sample audio file for integration tests
├── test_cli_integration.py  # CLI tool integration tests
├── test_model_manager.py    # ModelManager unit tests
├── test_recorder.py         # AudioRecorder unit tests
├── test_settings.py         # Settings/SettingsService unit tests
├── test_transcriber.py      # Transcriber unit tests
└── test_ui_components.py    # UI component tests (RecordingIndicator, MicTest)
```

---

## Test Categories

### 1. Unit Tests

Unit tests validate individual modules in isolation. External dependencies are mocked to ensure tests are fast and reliable.

#### Settings Tests (`test_settings.py`)

Tests the `Settings` dataclass and `SettingsService` class:

- Default settings values
- Settings serialization (`to_dict()`, `from_dict()`)
- Loading/saving settings from disk
- Handling corrupted settings files
- Settings reload functionality

```python
def test_default_settings(self):
    """Test default settings values."""
    settings = Settings()
    
    assert settings.hotkey == "ctrl+shift+space"
    assert settings.model_name == "small"
    assert settings.language == "auto"
```

#### Model Manager Tests (`test_model_manager.py`)

Tests the `ModelManager` class for Whisper model management:

- Available models catalog
- Model download status checks
- Model path resolution
- Model deletion
- Getting model info

```python
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
```

#### Transcriber Tests (`test_transcriber.py`)

Tests the `Transcriber` class for speech-to-text:

- Initialization and configuration
- `TranscriptionResult` dataclass
- `DeviceInfo` dataclass
- Device resolution (CUDA/CPU)
- NVIDIA GPU detection via nvidia-smi
- Model loading/unloading

```python
@patch('subprocess.run')
def test_detect_nvidia_gpu_via_smi_success(self, mock_run):
    """Test nvidia-smi GPU detection when successful."""
    mock_run.return_value = MagicMock(
        returncode=0,
        stdout="NVIDIA GeForce RTX 4080\n"
    )
    
    transcriber = Transcriber()
    result = transcriber._detect_nvidia_gpu_via_smi()
    
    assert result == "NVIDIA GeForce RTX 4080"
```

#### Recorder Tests (`test_recorder.py`)

Tests the `AudioRecorder` class for audio capture:

- Recorder initialization
- Custom sample rates
- Recording callbacks
- `RecordingResult` dataclass
- Device enumeration
- Device selection

**Note:** These tests use `pytest.mark.skipif` to skip when PortAudio is not available (common in CI environments).

```python
pytestmark = pytest.mark.skipif(
    not PORTAUDIO_AVAILABLE,
    reason="PortAudio library not found (Linux CI environment)"
)
```

### 2. Integration Tests

Integration tests validate component interactions and CLI tool functionality.

#### CLI Integration Tests (`test_cli_integration.py`)

Tests the `cli_test.py` tool using subprocess calls:

**CLI Commands Tested:**
- `--help` and `--version`
- `models list/info/path`
- `settings show/path/get`
- `healthcheck`
- `devices`
- `inject`
- `transcribe`
- `full-test`

**Subprocess Testing Pattern:**
```python
def test_cli_help(self):
    """Test that CLI help works."""
    result = subprocess.run(
        [sys.executable, "-m", "src.cli_test", "--help"],
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0
    assert "VoxTether CLI Test Tool" in result.stdout
```

**Direct Import Testing Pattern:**
```python
def test_create_parser(self):
    """Test parser creation."""
    from src.cli_test import create_parser

    parser = create_parser()
    args = parser.parse_args(["models", "list"])
    assert args.command == "models"
    assert args.action == "list"
```

### 3. UI Component Tests (`test_ui_components.py`)

Tests UI components like `RecordingIndicator` and `MicTestWindow`:

- Component initialization
- State transitions
- Callback handling

```python
def test_recording_indicator_state_methods(self):
    """Test state change methods work without starting the window."""
    indicator = RecordingIndicator()

    indicator.show_recording()
    assert indicator._state == "recording"

    indicator.show_transcribing()
    assert indicator._state == "transcribing"

    indicator.hide()
    assert indicator._state == "hidden"
```

---

## Mocking Strategies

### Hardware Mocking

VoxTether tests mock hardware dependencies to run in CI environments without audio devices or GPUs.

#### Audio Device Mocking

```python
@patch('src.recorder.sd')
def test_get_input_devices(self, mock_sd):
    """Test getting input devices."""
    mock_sd.query_devices.return_value = [
        {"name": "Microphone 1", "max_input_channels": 2, "default_samplerate": 44100},
        {"name": "Speaker", "max_input_channels": 0, "default_samplerate": 48000},
    ]
    
    recorder = AudioRecorder()
    devices = recorder.get_input_devices()
    
    assert len(devices) == 1  # Only input devices
```

#### GPU Detection Mocking

```python
@patch('subprocess.run')
def test_detect_nvidia_gpu_via_smi_not_found(self, mock_run):
    """Test nvidia-smi GPU detection when nvidia-smi is not installed."""
    mock_run.side_effect = FileNotFoundError("nvidia-smi not found")
    
    transcriber = Transcriber()
    result = transcriber._detect_nvidia_gpu_via_smi()
    
    assert result is None
```

### Dependency Availability Checks

Tests check for missing dependencies and skip gracefully:

```python
def _has_missing_dependencies(result: subprocess.CompletedProcess) -> bool:
    """Check if a command failed due to missing dependencies."""
    if result.returncode != 0:
        if "ModuleNotFoundError" in result.stderr:
            return True
        if "PortAudio library not found" in result.stderr:
            return True
    return False
```

### Temporary Directories

Tests use `tempfile.TemporaryDirectory()` for isolated file operations:

```python
def test_save_and_load_settings(self):
    """Test saving and loading settings."""
    with tempfile.TemporaryDirectory() as tmpdir:
        settings_path = Path(tmpdir) / "settings.json"
        
        service = SettingsService(settings_path)
        service.update(hotkey="alt+space", model_name="tiny")
        
        service2 = SettingsService(settings_path)
        assert service2.settings.hotkey == "alt+space"
```

---

## Skipping Tests

### Hardware-Dependent Tests

Tests requiring hardware are skipped in CI:

```python
@pytest.mark.skip(reason="Requires audio hardware")
def test_record_audio(self):
    """Test actual audio recording."""
    # ...

@pytest.mark.skip(reason="Requires model download")
def test_transcribe_audio_file(self):
    """Test actual transcription."""
    # ...
```

### PortAudio Availability

The recorder tests are skipped entirely when PortAudio is not available:

```python
try:
    import sounddevice
    PORTAUDIO_AVAILABLE = True
except (OSError, ModuleNotFoundError):
    PORTAUDIO_AVAILABLE = False

pytestmark = pytest.mark.skipif(
    not PORTAUDIO_AVAILABLE,
    reason="PortAudio library not found (Linux CI environment)"
)
```

### UI Component Availability

UI component tests check for import availability:

```python
try:
    from src.ui.recording_indicator import RecordingIndicator
except ImportError:
    RecordingIndicator = None

@pytest.mark.skipif(RecordingIndicator is None, reason="RecordingIndicator not available")
class TestRecordingIndicator:
    # ...
```

---

## Running Tests

### Run All Tests

```bash
# Run all tests with verbose output
pytest

# Run with pytest's default configuration from pyproject.toml
pytest tests/ -v --tb=short
```

### Run Specific Test Files

```bash
# Run settings tests only
pytest tests/test_settings.py

# Run model manager tests only
pytest tests/test_model_manager.py

# Run CLI integration tests only
pytest tests/test_cli_integration.py
```

### Run Specific Test Classes or Methods

```bash
# Run a specific test class
pytest tests/test_settings.py::TestSettings

# Run a specific test method
pytest tests/test_settings.py::TestSettings::test_default_settings
```

### Run Tests with Coverage

```bash
# Generate coverage report
pytest --cov=src --cov-report=html

# View coverage report
open htmlcov/index.html
```

### Run Slow/Manual Tests

Some tests require model downloads and are skipped by default:

```bash
# Run slow tests manually
pytest -k test_transcribe_test_recording --run-slow
```

---

## CI/CD Pipeline

The test pipeline is defined in `.github/workflows/ci.yml`.

### Pipeline Jobs

| Job | Runner | Purpose |
|-----|--------|---------|
| `test-backend` | ubuntu-latest | Test backend starts correctly |
| `test-python` | ubuntu-latest | Run pytest suite |
| `build-frontend` | windows-latest | Build Electron app |
| `test-frontend-e2e` | ubuntu-latest | Playwright E2E tests |
| `build-complete` | ubuntu-latest | Verify all jobs passed |

### Test Backend Job

Starts the FastAPI server and checks health endpoint:

```yaml
- name: Test backend starts correctly
  run: |
    cd src/backend
    timeout 10 python -m uvicorn main:app --host 127.0.0.1 --port 5678 &
    sleep 5
    curl -f http://127.0.0.1:5678/api/health || echo "Health check failed"
```

### Test Backend Job

Tests that the FastAPI server starts correctly:

```yaml
- name: Install backend dependencies
  run: |
    python -m pip install --upgrade pip
    pip install -r src/backend/requirements.txt

- name: Run backend linting (ruff)
  run: |
    pip install ruff
    ruff check src/backend/

- name: Test backend starts correctly
  run: |
    cd src/backend
    timeout 10 python -m uvicorn main:app --host 127.0.0.1 --port 5678 &
    sleep 5
    curl -f http://127.0.0.1:5678/api/health || echo "Health check failed"
```

### Pipeline Triggers

```yaml
on:
  pull_request:
    branches: [main]
  push:
    branches: [main]
  workflow_dispatch:  # Manual trigger
```

---

## Pytest Configuration

Test configuration is in `pyproject.toml`:

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
python_files = ["test_*.py"]
python_classes = ["Test*"]
python_functions = ["test_*"]
addopts = "-v --tb=short"
```

---

## Linting

Ruff is used for linting tests:

```bash
# Lint source and test files
ruff check src/ tests/
```

Ruff configuration in `pyproject.toml`:

```toml
[tool.ruff]
line-length = 100
target-version = "py310"
select = ["E", "F", "W", "I", "N", "UP", "B", "C4"]
ignore = ["E501"]
```

---

## Best Practices

### Writing New Tests

1. **Follow naming conventions**: Test files should be named `test_*.py`, classes `Test*`, functions `test_*`
2. **Use descriptive names**: Test names should describe what is being tested
3. **Add docstrings**: Document the purpose of each test
4. **Mock external dependencies**: Use `unittest.mock.patch` for hardware/network
5. **Use temporary directories**: Use `tempfile.TemporaryDirectory()` for file tests
6. **Skip hardware tests**: Mark hardware-dependent tests with `@pytest.mark.skip`

### Test Organization

1. **Group related tests**: Use test classes to group related tests
2. **Separate unit and integration**: Keep unit tests focused and integration tests separate
3. **Use fixtures sparingly**: Prefer simple test setup over complex fixtures

### Example Test Pattern

```python
"""Tests for the example module."""

from unittest.mock import patch, MagicMock
import tempfile
from pathlib import Path

import pytest

from src.example import ExampleClass


class TestExampleClass:
    """Tests for the ExampleClass."""
    
    def test_initialization(self):
        """Test ExampleClass initialization."""
        instance = ExampleClass()
        assert instance.value == "default"
    
    def test_with_mock(self):
        """Test using mocking."""
        with patch('src.example.external_dependency') as mock_dep:
            mock_dep.return_value = "mocked"
            result = ExampleClass().get_external_value()
            assert result == "mocked"
    
    def test_with_temp_directory(self):
        """Test with temporary directory."""
        with tempfile.TemporaryDirectory() as tmpdir:
            path = Path(tmpdir) / "test.txt"
            ExampleClass().write_file(path, "content")
            assert path.exists()
```

---

## Troubleshooting

### Tests Fail with Import Errors

Ensure you're in a virtual environment with all dependencies:

```bash
cd src/backend
python -m venv venv
source venv/bin/activate  # Linux/Mac
venv\Scripts\activate     # Windows
pip install -r requirements.txt
pip install -r ../../requirements-dev.txt
```

### PortAudio Not Found

On Linux, install PortAudio:

```bash
sudo apt-get install libportaudio2
```

### Tests Skip Due to Missing Dependencies

Check which dependencies are available:

```bash
python -c "import sounddevice; print('sounddevice OK')"
python -c "import faster_whisper; print('faster-whisper OK')"
```

### Coverage Report Not Generated

Ensure pytest-cov is installed:

```bash
pip install pytest-cov
pytest --cov=src --cov-report=html
```

---

## See Also

- [Architecture Documentation](ARCHITECTURE.md) - System architecture overview
- [Backend API Documentation](BACKEND-API.md) - API reference
- [Backend Setup Guide](BACKEND-SETUP.md) - Environment setup
- [Installation Guide](INSTALLATION.md) - Full installation instructions
