"""Tests for the recorder module."""

import tempfile
from pathlib import Path
from unittest.mock import patch, MagicMock
import sys

import pytest


# Skip all tests in this module if PortAudio is not available (Linux CI)
try:
    import sounddevice
    PORTAUDIO_AVAILABLE = True
except OSError:
    PORTAUDIO_AVAILABLE = False

pytestmark = pytest.mark.skipif(
    not PORTAUDIO_AVAILABLE,
    reason="PortAudio library not found (Linux CI environment)"
)


class TestAudioRecorder:
    """Tests for the AudioRecorder class."""
    
    def test_import_recorder(self):
        """Test that the recorder module can be imported."""
        from src.recorder import AudioRecorder, RecordingResult
        
        assert AudioRecorder is not None
        assert RecordingResult is not None
    
    def test_recorder_initialization(self):
        """Test recorder initialization."""
        from src.recorder import AudioRecorder
        
        recorder = AudioRecorder()
        
        assert recorder.sample_rate == 16000
        assert recorder.is_recording is False
    
    def test_recorder_custom_sample_rate(self):
        """Test recorder with custom sample rate."""
        from src.recorder import AudioRecorder
        
        recorder = AudioRecorder(sample_rate=44100)
        
        assert recorder.sample_rate == 44100
    
    def test_set_recording_callback(self):
        """Test setting recording callback."""
        from src.recorder import AudioRecorder
        
        recorder = AudioRecorder()
        callback_called = []
        
        def callback(is_recording):
            callback_called.append(is_recording)
        
        recorder.set_recording_callback(callback)
        
        # Callback should be set but not called yet
        assert len(callback_called) == 0
    
    def test_recording_result_success(self):
        """Test RecordingResult with success."""
        from src.recorder import RecordingResult
        
        result = RecordingResult(
            file_path=Path("/tmp/test.wav"),
            duration_seconds=2.5,
            sample_rate=16000,
            success=True,
        )
        
        assert result.success is True
        assert result.duration_seconds == 2.5
        assert result.error is None
    
    def test_recording_result_failure(self):
        """Test RecordingResult with failure."""
        from src.recorder import RecordingResult
        
        result = RecordingResult(
            file_path=Path(),
            duration_seconds=0,
            sample_rate=16000,
            success=False,
            error="No audio device",
        )
        
        assert result.success is False
        assert result.error == "No audio device"
    
    @patch('src.recorder.sd')
    def test_get_input_devices(self, mock_sd):
        """Test getting input devices."""
        from src.recorder import AudioRecorder
        
        mock_sd.query_devices.return_value = [
            {"name": "Microphone 1", "max_input_channels": 2, "default_samplerate": 44100},
            {"name": "Speaker", "max_input_channels": 0, "default_samplerate": 48000},
            {"name": "Microphone 2", "max_input_channels": 1, "default_samplerate": 16000},
        ]
        
        recorder = AudioRecorder()
        devices = recorder.get_input_devices()
        
        # Should only return input devices
        assert len(devices) == 2
        assert devices[0]["name"] == "Microphone 1"
        assert devices[1]["name"] == "Microphone 2"
    
    def test_set_device(self):
        """Test setting the recording device."""
        from src.recorder import AudioRecorder
        
        recorder = AudioRecorder()
        recorder.set_device(1)
        
        assert recorder._device == 1
    
    def test_stop_recording_when_not_recording(self):
        """Test stopping recording when not recording."""
        from src.recorder import AudioRecorder
        
        recorder = AudioRecorder()
        
        result = recorder.stop_recording()
        
        assert result is None


class TestRecorderIntegration:
    """Integration tests for the recorder (require audio hardware)."""
    
    @pytest.mark.skip(reason="Requires audio hardware")
    def test_record_audio(self):
        """Test actual audio recording."""
        from src.recorder import AudioRecorder
        import time
        
        recorder = AudioRecorder()
        
        # Start recording
        assert recorder.start_recording() is True
        assert recorder.is_recording is True
        
        # Record for 1 second
        time.sleep(1.0)
        
        # Stop recording
        result = recorder.stop_recording()
        
        assert result is not None
        assert result.success is True
        assert result.duration_seconds > 0.9
        assert result.file_path.exists()
        
        # Clean up
        result.file_path.unlink()
