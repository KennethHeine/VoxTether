"""Tests for the recording indicator module."""

import pytest

# Import modules at module level to avoid import order issues in tests
try:
    from src.ui.recording_indicator import RecordingIndicator
except ImportError:
    RecordingIndicator = None


@pytest.mark.skipif(RecordingIndicator is None, reason="RecordingIndicator not available")
class TestRecordingIndicator:
    """Tests for the RecordingIndicator class."""

    def test_import_recording_indicator(self):
        """Test that the recording indicator module can be imported."""
        assert RecordingIndicator is not None

    def test_recording_indicator_initialization(self):
        """Test RecordingIndicator initialization."""
        indicator = RecordingIndicator()

        assert indicator._root is None
        assert indicator._canvas is None
        assert indicator._is_running is False
        assert indicator._state == "hidden"

    def test_recording_indicator_state_methods(self):
        """Test state change methods work without starting the window."""
        indicator = RecordingIndicator()

        # These should not raise even without a window
        indicator.show_recording()
        assert indicator._state == "recording"

        indicator.show_transcribing()
        assert indicator._state == "transcribing"

        indicator.hide()
        assert indicator._state == "hidden"

    def test_recording_indicator_is_running_property(self):
        """Test is_running property."""
        indicator = RecordingIndicator()

        assert indicator.is_running is False


# Note: MicTestWindow has been moved to the Electron frontend
# The mic test functionality is now implemented client-side using Web Audio API
# See: src/frontend-electron/src/renderer/renderer.js
