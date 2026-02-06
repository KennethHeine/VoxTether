"""Transcription service using faster-whisper."""

import asyncio
import atexit
import logging
import os
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from config import settings
from constants import DEFAULT_BEAM_SIZE, DEFAULT_VAD_FILTER
from protocols import TranscriptionResult
from schemas import WordInfo

logger = logging.getLogger(__name__)


def _setup_cuda_dll_paths() -> None:
    """Add NVIDIA CUDA DLL paths to system PATH on Windows.
    
    This is required for ctranslate2 to find cublas64_12.dll and other
    CUDA runtime libraries when nvidia-cublas-cu12 is installed via pip.
    Must be called before importing ctranslate2 or faster_whisper.
    """
    if sys.platform != "win32":
        return

    # Find the site-packages nvidia directory
    site_packages = Path(sys.prefix) / "Lib" / "site-packages" / "nvidia"
    if not site_packages.exists():
        return

    # Add all nvidia bin directories to PATH
    nvidia_bin_paths = []
    for subdir in ["cublas", "cudnn", "cuda_runtime", "cufft", "curand"]:
        bin_path = site_packages / subdir / "bin"
        if bin_path.exists():
            nvidia_bin_paths.append(str(bin_path))

    if nvidia_bin_paths:
        current_path = os.environ.get("PATH", "")
        new_paths = os.pathsep.join(nvidia_bin_paths)

        # Only add if not already present
        if nvidia_bin_paths[0] not in current_path:
            os.environ["PATH"] = new_paths + os.pathsep + current_path
            logger.debug(f"Added NVIDIA DLL paths to PATH: {nvidia_bin_paths}")


# Setup CUDA DLL paths before any CUDA imports
_setup_cuda_dll_paths()

# Thread pool for blocking operations (lazy initialization)
_executor: ThreadPoolExecutor | None = None


def _get_executor() -> ThreadPoolExecutor:
    """Get the thread pool executor, creating it if needed."""
    global _executor
    if _executor is None:
        _executor = ThreadPoolExecutor(max_workers=settings.max_workers)
        # Store reference to avoid capturing a potentially None variable
        executor_ref = _executor
        atexit.register(lambda: executor_ref.shutdown(wait=True, cancel_futures=True))
    return _executor


class TranscriberService:
    """Transcription service using faster-whisper."""

    def __init__(self):
        """Initialize the transcriber service."""
        self._model = None
        self._model_name: str | None = None
        self._device: str | None = None
        self._compute_type: str | None = None
        # Override settings for device switching
        self._device_override: str | None = None
        self._compute_type_override: str | None = None

    def _resolve_device(self) -> tuple[str, str]:
        """Resolve the device and compute type to use.
        
        Returns:
            Tuple of (device, compute_type).
        """
        # Use overrides first, then settings
        device = self._device_override or settings.device
        compute_type = self._compute_type_override or settings.compute_type

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

    async def load_model(self, model_name: str | None = None) -> bool:
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
        return await loop.run_in_executor(_get_executor(), _load)

    def unload_model(self) -> None:
        """Unload the current model."""
        self._model = None
        self._model_name = None
        self._device = None
        self._compute_type = None
        self._device_override = None
        self._compute_type_override = None
        logger.info("Model unloaded")

    def is_loaded(self) -> bool:
        """Check if a model is loaded."""
        return self._model is not None

    def get_current_model(self) -> str | None:
        """Get the name of the currently loaded model."""
        return self._model_name

    def get_current_device(self) -> str | None:
        """Get the current compute device."""
        return self._device

    async def transcribe(
        self,
        audio_path: str,
        language: str = "auto",
        task: str = "transcribe",
        initial_prompt: str | None = None,
        word_timestamps: bool = False,
    ) -> TranscriptionResult:
        """Transcribe an audio file.
        
        Args:
            audio_path: Path to the audio file.
            language: Language code or 'auto'.
            task: 'transcribe' or 'translate'.
            initial_prompt: Optional prompt to guide transcription.
            word_timestamps: Whether to return word-level timestamps.
            
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
                    beam_size=DEFAULT_BEAM_SIZE,
                    vad_filter=DEFAULT_VAD_FILTER,
                    initial_prompt=initial_prompt,
                    word_timestamps=word_timestamps,
                )

                text_parts = []
                words: list[WordInfo] = []
                for segment in segments:
                    text_parts.append(segment.text)
                    # Extract word-level timestamps if requested
                    if word_timestamps and hasattr(segment, 'words') and segment.words:
                        for word in segment.words:
                            words.append(WordInfo(
                                word=word.word,
                                start=word.start,
                                end=word.end,
                                probability=word.probability,
                            ))

                text = "".join(text_parts).strip()
                duration = time.time() - start_time

                logger.info(f"Transcription completed in {duration:.2f}s")

                return TranscriptionResult(
                    text=text,
                    success=True,
                    duration_seconds=duration,
                    language=info.language,
                    words=words if word_timestamps and words else None,
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
        return await loop.run_in_executor(_get_executor(), _transcribe)

    async def change_device(self, device: str, compute_type: str = "auto") -> bool:
        """Change the compute device.
        
        Args:
            device: Device to use ('cuda', 'cpu', or 'auto').
            compute_type: Compute type to use ('float16', 'int8', 'float32', or 'auto').
            
        Returns:
            True if successful.
        """
        try:
            # Store overrides (None for "auto" to use settings defaults)
            self._device_override = device if device != "auto" else None
            self._compute_type_override = compute_type if compute_type != "auto" else None

            current_model = self._model_name
            if current_model:
                # Unload and reload with new device settings
                self._model = None
                self._device = None
                self._compute_type = None
                return await self.load_model(current_model)
            return True

        except Exception as e:
            logger.error(f"Failed to change device: {e}")
            return False
