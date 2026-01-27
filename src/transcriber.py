"""Transcription engine for VoxTether using faster-whisper."""

import logging
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

logger = logging.getLogger(__name__)


@dataclass
class TranscriptionResult:
    """Result of a transcription operation."""
    
    text: str
    success: bool
    duration_seconds: float
    language: Optional[str] = None
    error: Optional[str] = None


@dataclass
class DeviceInfo:
    """Information about the compute device."""
    
    device_type: str  # "cuda" or "cpu"
    device_name: Optional[str] = None
    cuda_available: bool = False
    cuda_version: Optional[str] = None


class Transcriber:
    """Transcribes audio using faster-whisper."""
    
    def __init__(
        self,
        model_name_or_path: str = "small",
        device: str = "auto",
        compute_type: str = "auto",
    ):
        """Initialize the transcriber.
        
        Args:
            model_name_or_path: Model name (tiny, base, small, medium, large-v3, etc.)
                or path to a downloaded model directory.
            device: Device to use for inference ("auto", "cuda", or "cpu").
            compute_type: Compute type for inference ("auto", "int8", "float16", "float32").
        """
        self._model_name_or_path = model_name_or_path
        self._device = device
        self._compute_type = compute_type
        self._model = None
        self._actual_device: Optional[str] = None
        self._actual_compute_type: Optional[str] = None
    
    def _resolve_device(self) -> tuple[str, str]:
        """Resolve the device and compute type to use.
        
        Returns:
            Tuple of (device, compute_type).
        """
        device = self._device
        compute_type = self._compute_type
        
        if device == "auto":
            # Try to detect CUDA availability
            try:
                import torch
                if torch.cuda.is_available():
                    device = "cuda"
                    logger.info(f"CUDA available: {torch.cuda.get_device_name(0)}")
                else:
                    device = "cpu"
                    logger.info("CUDA not available, using CPU")
            except ImportError:
                # torch not installed, try ctranslate2 directly
                try:
                    import ctranslate2
                    if "cuda" in ctranslate2.get_supported_compute_types("cuda"):
                        device = "cuda"
                    else:
                        device = "cpu"
                except (ImportError, ModuleNotFoundError, RuntimeError, ValueError) as e:
                    logger.debug(f"ctranslate2 CUDA detection failed: {e}")
                    device = "cpu"
        
        if compute_type == "auto":
            if device == "cuda":
                compute_type = "float16"  # Best performance on GPU
            else:
                compute_type = "int8"  # Best performance on CPU
        
        return device, compute_type
    
    def load_model(self) -> bool:
        """Load the transcription model.
        
        Returns:
            True if the model was loaded successfully, False otherwise.
        """
        try:
            from faster_whisper import WhisperModel
            
            device, compute_type = self._resolve_device()
            self._actual_device = device
            self._actual_compute_type = compute_type
            
            logger.info(
                f"Loading model '{self._model_name_or_path}' on {device} "
                f"with {compute_type} precision"
            )
            
            start_time = time.time()
            
            self._model = WhisperModel(
                self._model_name_or_path,
                device=device,
                compute_type=compute_type,
            )
            
            load_time = time.time() - start_time
            logger.info(f"Model loaded in {load_time:.2f}s")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to load model: {e}")
            
            # Try fallback to CPU if CUDA failed
            if self._device in ("auto", "cuda"):
                logger.info("Falling back to CPU...")
                try:
                    from faster_whisper import WhisperModel
                    
                    self._actual_device = "cpu"
                    self._actual_compute_type = "int8"
                    
                    self._model = WhisperModel(
                        self._model_name_or_path,
                        device="cpu",
                        compute_type="int8",
                    )
                    
                    logger.info("Model loaded on CPU (fallback)")
                    return True
                    
                except Exception as fallback_error:
                    logger.error(f"CPU fallback also failed: {fallback_error}")
            
            return False
    
    def unload_model(self) -> None:
        """Unload the model to free memory."""
        self._model = None
        self._actual_device = None
        self._actual_compute_type = None
        logger.info("Model unloaded")
    
    def is_loaded(self) -> bool:
        """Check if a model is loaded."""
        return self._model is not None
    
    def get_device_info(self) -> DeviceInfo:
        """Get information about the current compute device.
        
        Returns:
            DeviceInfo with device details.
        """
        cuda_available = False
        cuda_version = None
        device_name = None
        
        try:
            import torch
            cuda_available = torch.cuda.is_available()
            if cuda_available:
                device_name = torch.cuda.get_device_name(0)
                cuda_version = torch.version.cuda
        except ImportError:
            try:
                import ctranslate2
                cuda_available = "cuda" in ctranslate2.get_supported_compute_types("cuda")
            except (ImportError, ValueError, RuntimeError):
                pass  # CUDA detection via ctranslate2 failed
        
        # Fallback: Check for NVIDIA GPU using nvidia-smi if libraries didn't detect CUDA
        # This can detect the GPU hardware even if CUDA libraries aren't properly installed
        if not cuda_available and device_name is None:
            detected_name = self._detect_nvidia_gpu_via_smi()
            if detected_name:
                device_name = detected_name
                # Note: cuda_available remains False since the CUDA libraries aren't working
                # This helps inform the user that they have an NVIDIA GPU but CUDA isn't configured
        
        return DeviceInfo(
            device_type=self._actual_device or ("cuda" if cuda_available else "cpu"),
            device_name=device_name,
            cuda_available=cuda_available,
            cuda_version=cuda_version,
        )
    
    def _detect_nvidia_gpu_via_smi(self) -> Optional[str]:
        """Detect NVIDIA GPU using nvidia-smi command.
        
        This can detect GPU hardware even when CUDA libraries aren't properly configured.
        
        Returns:
            GPU name if detected, None otherwise.
        """
        try:
            result = subprocess.run(
                ["nvidia-smi", "--query-gpu=name", "--format=csv,noheader,nounits"],
                capture_output=True,
                text=True,
                timeout=5,
                creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, 'CREATE_NO_WINDOW') else 0,
            )
            if result.returncode == 0 and result.stdout.strip():
                gpu_name = result.stdout.strip().split('\n')[0]  # Get first GPU
                logger.debug(f"Detected NVIDIA GPU via nvidia-smi: {gpu_name}")
                return gpu_name
        except (FileNotFoundError, subprocess.TimeoutExpired, OSError) as e:
            logger.debug(f"nvidia-smi detection failed: {e}")
        except Exception as e:
            logger.debug(f"Unexpected error during nvidia-smi detection: {e}")
        return None
    
    def transcribe(
        self,
        audio_path: str | Path,
        language: str = "auto",
        task: str = "transcribe",
    ) -> TranscriptionResult:
        """Transcribe an audio file.
        
        Args:
            audio_path: Path to the audio file (WAV format preferred).
            language: Language code or "auto" for auto-detection.
            task: "transcribe" or "translate" (translate to English).
            
        Returns:
            TranscriptionResult with the transcribed text.
        """
        if not self._model:
            if not self.load_model():
                return TranscriptionResult(
                    text="",
                    success=False,
                    duration_seconds=0,
                    error="Failed to load model",
                )
        
        try:
            start_time = time.time()
            
            # Prepare transcription options
            language_arg = None if language == "auto" else language
            
            logger.info(f"Transcribing {audio_path} (language={language}, task={task})")
            
            # Run transcription
            segments, info = self._model.transcribe(
                str(audio_path),
                language=language_arg,
                task=task,
                beam_size=5,
                vad_filter=True,  # Filter out non-speech
            )
            
            # Collect text from all segments
            text_parts = []
            for segment in segments:
                text_parts.append(segment.text)
            
            text = "".join(text_parts).strip()
            duration = time.time() - start_time
            
            logger.info(f"Transcription completed in {duration:.2f}s: '{text[:100]}...'")
            
            return TranscriptionResult(
                text=text,
                success=True,
                duration_seconds=duration,
                language=info.language,
            )
            
        except Exception as e:
            logger.error(f"Transcription failed: {e}")
            return TranscriptionResult(
                text="",
                success=False,
                duration_seconds=0,
                error=str(e),
            )
    
    def change_model(self, model_name_or_path: str) -> bool:
        """Change the model.
        
        Args:
            model_name_or_path: New model name or path.
            
        Returns:
            True if the model was changed successfully, False otherwise.
        """
        self.unload_model()
        self._model_name_or_path = model_name_or_path
        return self.load_model()
    
    def change_device(self, device: str, compute_type: str = "auto") -> bool:
        """Change the compute device.
        
        Args:
            device: New device ("cuda" or "cpu").
            compute_type: New compute type.
            
        Returns:
            True if the device was changed successfully, False otherwise.
        """
        self.unload_model()
        self._device = device
        self._compute_type = compute_type
        return self.load_model()
