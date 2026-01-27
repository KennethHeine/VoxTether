"""Integration tests for VoxTether CLI test tool.

These tests exercise the CLI tool functionality using the provided test recording.
Some tests may require specific hardware (audio devices) or network access (model downloads).

Note: Tests that require full VoxTether dependencies will be skipped in CI environments
where those dependencies are not installed.
"""

import subprocess
import sys
from pathlib import Path

import pytest

# Path to the test recording
TEST_RECORDING = Path(__file__).parent / "test-recoarding.wav"


def _has_missing_dependencies(result: subprocess.CompletedProcess) -> bool:
    """Check if a command failed due to missing dependencies.

    Args:
        result: Completed subprocess result.

    Returns:
        True if dependencies are missing, False otherwise.
    """
    if result.returncode != 0 and "ModuleNotFoundError" in result.stderr:
        return True
    return False


class TestCLITool:
    """Tests for the CLI test tool."""

    def test_cli_help(self):
        """Test that CLI help works."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "--help"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 0
        assert "VoxTether CLI Test Tool" in result.stdout

    def test_cli_version(self):
        """Test that CLI version works."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "--version"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 0
        assert "VoxTether CLI Test Tool" in result.stdout

    def test_models_list(self):
        """Test listing available models."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "models", "list"],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed (huggingface_hub)")
        assert result.returncode == 0
        assert "Available Models" in result.stdout
        assert "tiny" in result.stdout
        assert "small" in result.stdout

    def test_models_info(self):
        """Test getting model info."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "models", "info", "--model", "small"],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed (huggingface_hub)")
        assert result.returncode == 0
        assert "Model: small" in result.stdout
        assert "HuggingFace repo:" in result.stdout

    def test_models_path(self):
        """Test getting models path."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "models", "path"],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed (huggingface_hub)")
        assert result.returncode == 0
        assert result.stdout.strip()  # Should output a path

    def test_settings_show(self):
        """Test showing settings."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "settings", "show"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 0
        assert "Current Settings" in result.stdout
        assert "hotkey:" in result.stdout

    def test_settings_path(self):
        """Test getting settings path."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "settings", "path"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 0
        assert result.stdout.strip().endswith(".json")

    def test_settings_get(self):
        """Test getting a specific setting."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "settings", "get", "--key", "model_name"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 0
        assert result.stdout.strip()  # Should output a value

    def test_settings_get_invalid_key(self):
        """Test getting an invalid setting key."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "settings", "get", "--key", "invalid_key"],
            capture_output=True,
            text=True,
        )
        assert result.returncode == 1
        assert "Unknown setting" in result.stdout

    def test_healthcheck(self):
        """Test the healthcheck command."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "healthcheck"],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed")
        # Healthcheck may pass or fail depending on environment
        assert "VoxTether Healthcheck" in result.stdout

    def test_inject_clipboard(self):
        """Test clipboard injection."""
        test_text = "VoxTether CLI Test 12345"
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "inject", test_text],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed (pyperclip)")
        assert result.returncode == 0
        assert "clipboard" in result.stdout.lower()


class TestCLIWithTestRecording:
    """Tests that use the test recording file."""

    def test_test_recording_exists(self):
        """Verify the test recording file exists."""
        assert TEST_RECORDING.exists(), f"Test recording not found: {TEST_RECORDING}"

    def test_transcribe_file_not_found(self):
        """Test transcribe with non-existent file."""
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "transcribe", "/nonexistent/file.wav"],
            capture_output=True,
            text=True,
        )
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed")
        assert result.returncode == 1
        assert "not found" in result.stdout

    @pytest.mark.skip(reason="Requires model download - run manually with: pytest -k test_transcribe_test_recording --run-slow")
    def test_transcribe_test_recording(self):
        """Test transcribing the test recording.

        This test requires a model to be downloaded and may take a while.
        Run manually with: pytest -k test_transcribe_test_recording --run-slow
        """
        result = subprocess.run(
            [
                sys.executable, "-m", "src.cli_test", "transcribe",
                str(TEST_RECORDING),
                "--model", "tiny",
                "--device", "cpu",
            ],
            capture_output=True,
            text=True,
            timeout=300,  # 5 minute timeout for model download
        )
        assert result.returncode == 0
        assert "Transcription completed" in result.stdout
        assert "Text:" in result.stdout

    @pytest.mark.skip(reason="Requires model download - run manually")
    def test_full_test_with_recording(self):
        """Test full integration test with the test recording.

        This test requires a model to be downloaded and may take a while.
        """
        result = subprocess.run(
            [
                sys.executable, "-m", "src.cli_test", "full-test",
                "--audio-file", str(TEST_RECORDING),
                "--model", "tiny",
                "--device", "cpu",
            ],
            capture_output=True,
            text=True,
            timeout=300,
        )
        assert result.returncode == 0
        assert "VoxTether Full Integration Test" in result.stdout
        assert "Test Summary" in result.stdout


class TestCLIAudioSystem:
    """Tests for audio system functionality.

    These tests may be skipped in CI environments without audio hardware.
    """

    def test_devices_command(self):
        """Test listing audio devices.

        This test may fail in environments without PortAudio.
        """
        result = subprocess.run(
            [sys.executable, "-m", "src.cli_test", "devices"],
            capture_output=True,
            text=True,
        )
        # Skip if dependencies are missing
        if _has_missing_dependencies(result):
            pytest.skip("VoxTether dependencies not installed (sounddevice)")
        # The test passes if the command runs - it may report no devices in CI
        # or fail if PortAudio is not available
        if result.returncode == 0:
            assert "Audio Input Devices" in result.stdout or "No audio input devices" in result.stdout
        else:
            # When there are no audio devices or PortAudio is not available
            assert (
                "Audio system not available" in result.stdout
                or "PortAudio" in result.stdout
                or "No audio input devices" in result.stdout
            )


class TestCLIDirectImport:
    """Tests using direct Python imports instead of subprocess."""

    def test_import_cli_test_module(self):
        """Test that the CLI test module can be imported."""
        from src.cli_test import create_parser, main

        assert main is not None
        assert create_parser is not None

    def test_create_parser(self):
        """Test parser creation."""
        from src.cli_test import create_parser

        parser = create_parser()
        assert parser is not None

        # Test parsing some arguments
        args = parser.parse_args(["models", "list"])
        assert args.command == "models"
        assert args.action == "list"

    def test_parse_transcribe_args(self):
        """Test parsing transcribe command arguments."""
        from src.cli_test import create_parser

        parser = create_parser()
        args = parser.parse_args([
            "transcribe", "test.wav",
            "--model", "tiny",
            "--device", "cpu",
            "--language", "en",
        ])

        assert args.command == "transcribe"
        assert args.audio_file == "test.wav"
        assert args.model == "tiny"
        assert args.device == "cpu"
        assert args.language == "en"

    def test_parse_full_test_args(self):
        """Test parsing full-test command arguments."""
        from src.cli_test import create_parser

        parser = create_parser()
        args = parser.parse_args([
            "full-test",
            "--audio-file", "test.wav",
            "--model", "small",
        ])

        assert args.command == "full-test"
        assert args.audio_file == "test.wav"
        assert args.model == "small"
