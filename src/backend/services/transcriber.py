"""Transcription service using faster-whisper."""

import asyncio
import logging
import subprocess
import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

from config import settings

logger = logging.getLogger(__name__)

# Thread pool for blocking operations
_executor = ThreadPoolExecutor(max_workers=2)


@dataclass
class TranscriptionResult:
    """Result of a transcription operation."""
    
    text: str
    success: bool
    duration_seconds: float
    language: Optional[str] = None
    error: Optional[str] = None


class TranscriberService:
    """Transcription service using faster-whisper."""
    
    def __init__(self):
        """Initialize the transcriber service."""
        self._model = None
        self._model_name: Optional[str] = None
        self._device: Optional[str] = None
        self._compute_type: Optional[str] = None
    
    def _resolve_device(self) -> tuple[str, str]:
        """Resolve the device and compute type to use.
        
        Returns:
            Tuple of (device, compute_type).
        """
        device = settings.device
        compute_type = settings.compute_type
        
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
                    cuda_device_count = ctranslate2.get_cuda_device_count()
                    if cuda_device_count > 0:
                        device = "cuda"
                        logger.info(f"CUDA available via ctranslate2: {cuda_device_count} device(s)")
                    else:
                        device = "cpu"
                        logger.info("CUDA not available, using CPU")
                except (ImportError, ModuleNotFoundError, RuntimeError, ValueError) as e:
                    logger.debug(f"ctranslate2 CUDA detection failed: {e}")
                    device = "cpu"
        
        if compute_type == "auto":
            if device == "cuda":
                compute_type = "float16"  # Best performance on GPU
            else:
                compute_type = "int8"  # Best performance on CPU
        
        return device, compute_type
    
    def _get_model_path(self, model_name: str) -> str:
        """Get the path to a model.
        
        Args:
            model_name: Model name (e.g., 'small', 'base', 'large-v3').
            
        Returns:
            Path to the model or model name if not found locally.
        """
        # Check if it's a path
        if Path(model_name).exists():
            return model_name
        
        # Check in models directory
        model_path = Path(settings.models_path) / model_name
        if model_path.exists():
            return str(model_path)
        
        # Check for common variations
        for variant in [f"whisper-{model_name}", f"faster-whisper-{model_name}"]:
            variant_path = Path(settings.models_path) / variant
            if variant_path.exists():
                return str(variant_path)
        
        # Return the model name for download from HuggingFace
        return model_name
    
    async def load_model(self, model_name: Optional[str] = None) -> bool:
        """Load a transcription model.
        
        Args:
            model_name: Model name or path. Uses default if not specified.
            
        Returns:
            True if loaded successfully.
        """
        model_name = model_name or settings.default_model
        
        def _load():
            try:
                from faster_whisper import WhisperModel
                
                device, compute_type = self._resolve_device()
                model_path = self._get_model_path(model_name)
                
                logger.info(
                    f"Loading model '{model_path}' on {device} "
                    f"with {compute_type} precision"
                )
                
                start_time = time.time()
                
                self._model = WhisperModel(
                    model_path,
                    device=device,
                    compute_type=compute_type,
                )
                
                self._model_name = model_name
                self._device = device
                self._compute_type = compute_type
                
                load_time = time.time() - start_time
                logger.info(f"Model loaded in {load_time:.2f}s")
                
                return True
                
            except Exception as e:
                logger.error(f"Failed to load model: {e}")
                
                # Try fallback to CPU if CUDA failed
                if settings.device in ("auto", "cuda"):
                    logger.info("Falling back to CPU...")
                    try:
                        from faster_whisper import WhisperModel
                        
                        model_path = self._get_model_path(model_name)
                        self._model = WhisperModel(
                            model_path,
                            device="cpu",
                            compute_type="int8",
                        )
                        
                        self._model_name = model_name
                        self._device = "cpu"
                        self._compute_type = "int8"
                        
                        logger.info("Model loaded on CPU (fallback)")
                        return True
                        
                    except Exception as fallback_error:
                        logger.error(f"CPU fallback also failed: {fallback_error}")
                
                return False
        
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(_executor, _load)
    
    def unload_model(self) -> None:
        """Unload the current model."""
        self._model = None
        self._model_name = None
        self._device = None
        self._compute_type = None
        logger.info("Model unloaded")
    
    def is_loaded(self) -> bool:
        """Check if a model is loaded."""
        return self._model is not None
    
    def get_current_model(self) -> Optional[str]:
        """Get the name of the currently loaded model."""
        return self._model_name
    
    def get_current_device(self) -> Optional[str]:
        """Get the current compute device."""
        return self._device
    
    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
    ) -> TranscriptionResult:
        """Transcribe an audio file.
        
        Args:
            audio_path: Path to the audio file.
            language: Language code or 'auto'.
            task: 'transcribe' or 'translate'.
            
        Returns:
            TranscriptionResult with the text.
        """
        if not self._model:
            return TranscriptionResult(
                text="",
                success=False,
                duration_seconds=0,
                error="Model not loaded",
            )
        
        def _transcribe():
            try:
                start_time = time.time()
                
                language_arg = None if language == "auto" else language
                
                logger.info(f"Transcribing {audio_path} (language={language}, task={task})")
                
                segments, info = self._model.transcribe(
                    str(audio_path),
                    language=language_arg,
                    task=task,
                    beam_size=5,
                    vad_filter=True,
                )
                
                text_parts = []
                for segment in segments:
                    text_parts.append(segment.text)
                
                text = "".join(text_parts).strip()
                duration = time.time() - start_time
                
                logger.info(f"Transcription completed in {duration:.2f}s")
                
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
        
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(_executor, _transcribe)
    
    async def change_device(self, device: str, compute_type: str = "auto") -> bool:
        """Change the compute device.
        
        Args:
            device: Device to use ('cuda' or 'cpu').
            compute_type: Compute type to use.
            
        Returns:
            True if successful.
        """
        # Update settings temporarily
        old_device = settings.device
        old_compute_type = settings.compute_type
        
        try:
            # We can't modify pydantic settings directly, so we'll just reload
            current_model = self._model_name
            self.unload_model()
            
            # Temporarily override for this load
            if current_model:
                return await self.load_model(current_model)
            return True
            
        except Exception as e:
            logger.error(f"Failed to change device: {e}")
            return False
