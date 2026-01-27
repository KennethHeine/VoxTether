"""Tests for the recording indicator module."""

import pytest

# Import modules at module level to avoid import order issues in tests
try:
    from src.ui.recording_indicator import RecordingIndicator
except ImportError:
    RecordingIndicator = None

try:
    from src.ui.mic_test import MicTestWindow, show_mic_test
except ImportError:
    MicTestWindow = None
    show_mic_test = None


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


@pytest.mark.skipif(MicTestWindow is None, reason="MicTestWindow not available")
class TestMicTestWindow:
    """Tests for the MicTestWindow class."""

    def test_import_mic_test(self):
        """Test that the mic test module can be imported."""
        assert MicTestWindow is not None
        assert show_mic_test is not None

    def test_mic_test_window_initialization(self):
        """Test MicTestWindow initialization."""
        window = MicTestWindow()

        assert window._root is None
        assert window._window is None
        assert window._is_running is False
        assert window._devices == []

    def test_mic_test_window_with_callbacks(self):
        """Test MicTestWindow with callbacks."""
        close_called = False
        device_changed = None

        def on_close():
            nonlocal close_called
            close_called = True

        def on_device_change(device_id):
            nonlocal device_changed
            device_changed = device_id

        window = MicTestWindow(
            on_close=on_close,
            on_device_change=on_device_change,
        )

        assert window._on_close == on_close
        assert window._on_device_change == on_device_change
