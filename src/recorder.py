"""Audio recording for VoxTether using sounddevice."""

import logging
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

import numpy as np
import sounddevice as sd
import soundfile as sf

from .settings import get_temp_path

logger = logging.getLogger(__name__)


@dataclass
class RecordingResult:
    """Result of a recording operation."""
    
    file_path: Path
    duration_seconds: float
    sample_rate: int
    success: bool
    error: Optional[str] = None


RecordingCallback = Callable[[bool], None]  # is_recording


class AudioRecorder:
    """Records audio from the microphone to WAV files."""
    
    # Whisper expects 16kHz mono audio
    SAMPLE_RATE = 16000
    CHANNELS = 1
    
    def __init__(
        self,
        sample_rate: int = SAMPLE_RATE,
        channels: int = CHANNELS,
        device: Optional[int] = None,
    ):
        """Initialize the audio recorder.
        
        Args:
            sample_rate: Sample rate in Hz (default 16000 for Whisper).
            channels: Number of audio channels (default 1 for mono).
            device: Audio device index, or None for default device.
        """
        self._sample_rate = sample_rate
        self._channels = channels
        self._device = device
        
        self._is_recording = False
        self._audio_data: list[np.ndarray] = []
        self._audio_data_lock = threading.Lock()  # Protects _audio_data access
        self._recording_thread: Optional[threading.Thread] = None
        self._stop_event = threading.Event()
        self._recording_callback: Optional[RecordingCallback] = None
        self._stream: Optional[sd.InputStream] = None
    
    @property
    def is_recording(self) -> bool:
        """Check if recording is in progress."""
        return self._is_recording
    
    @property
    def sample_rate(self) -> int:
        """Get the sample rate."""
        return self._sample_rate
    
    def set_recording_callback(self, callback: Optional[RecordingCallback]) -> None:
        """Set a callback that will be called when recording state changes.
        
        Args:
            callback: Callback function that receives a boolean indicating
                whether recording is active.
        """
        self._recording_callback = callback
    
    def get_input_devices(self) -> list[dict]:
        """Get a list of available input devices.
        
        Returns:
            List of device info dictionaries with 'index', 'name', and 'channels'.
        """
        devices = []
        try:
            for i, device in enumerate(sd.query_devices()):
                if device["max_input_channels"] > 0:
                    devices.append({
                        "index": i,
                        "name": device["name"],
                        "channels": device["max_input_channels"],
                        "default": device.get("default_samplerate", 0),
                    })
        except Exception as e:
            logger.error(f"Failed to query audio devices: {e}")
        return devices
    
    def set_device(self, device: Optional[int]) -> None:
        """Set the recording device.
        
        Args:
            device: Audio device index, or None for default device.
        """
        self._device = device
    
    def start_recording(self) -> bool:
        """Start recording audio.
        
        Returns:
            True if recording started successfully, False otherwise.
        """
        if self._is_recording:
            logger.warning("Already recording")
            return False
        
        try:
            with self._audio_data_lock:
                self._audio_data = []
            self._stop_event.clear()
            self._is_recording = True
            
            def audio_callback(indata: np.ndarray, frames: int, time_info, status) -> None:
                if status:
                    logger.warning(f"Audio stream status: {status}")
                with self._audio_data_lock:
                    self._audio_data.append(indata.copy())
            
            self._stream = sd.InputStream(
                samplerate=self._sample_rate,
                channels=self._channels,
                device=self._device,
                callback=audio_callback,
                dtype=np.float32,
            )
            self._stream.start()
            
            logger.info("Recording started")
            
            if self._recording_callback:
                self._recording_callback(True)
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to start recording: {e}")
            self._is_recording = False
            return False
    
    def stop_recording(self) -> Optional[RecordingResult]:
        """Stop recording and save the audio to a file.
        
        Returns:
            RecordingResult with the file path, or None if no recording was active.
        """
        if not self._is_recording:
            logger.warning("Not recording")
            return None
        
        try:
            # Stop the stream first
            if self._stream:
                self._stream.stop()
                self._stream.close()
                self._stream = None
            
            self._is_recording = False
            
            if self._recording_callback:
                self._recording_callback(False)
            
            # Combine all recorded chunks (thread-safe access)
            with self._audio_data_lock:
                if not self._audio_data:
                    return RecordingResult(
                        file_path=Path(),
                        duration_seconds=0,
                        sample_rate=self._sample_rate,
                        success=False,
                        error="No audio data recorded",
                    )
                
                audio = np.concatenate(self._audio_data)
                self._audio_data = []  # Clear to prevent memory leaks
            duration = len(audio) / self._sample_rate
            
            logger.info(f"Recording stopped. Duration: {duration:.2f}s")
            
            # Save to a temporary WAV file
            temp_dir = get_temp_path()
            temp_file = temp_dir / f"recording_{id(self)}_{int(duration * 1000)}.wav"
            
            sf.write(str(temp_file), audio, self._sample_rate)
            
            return RecordingResult(
                file_path=temp_file,
                duration_seconds=duration,
                sample_rate=self._sample_rate,
                success=True,
            )
            
        except Exception as e:
            logger.error(f"Failed to stop recording: {e}")
            self._is_recording = False
            return RecordingResult(
                file_path=Path(),
                duration_seconds=0,
                sample_rate=self._sample_rate,
                success=False,
                error=str(e),
            )
    
    def test_microphone(self, duration_seconds: float = 2.0) -> Optional[RecordingResult]:
        """Record a test audio clip.
        
        Args:
            duration_seconds: Duration of the test recording.
            
        Returns:
            RecordingResult with the test recording.
        """
        import time
        
        if not self.start_recording():
            return None
        
        time.sleep(duration_seconds)
        
        return self.stop_recording()
    
    def cleanup_temp_files(self) -> None:
        """Clean up temporary recording files."""
        try:
            temp_dir = get_temp_path()
            for file in temp_dir.glob("recording_*.wav"):
                try:
                    file.unlink()
                except (FileNotFoundError, OSError):
                    pass  # File already deleted or in use
        except Exception as e:
            logger.warning(f"Failed to cleanup temp files: {e}")
