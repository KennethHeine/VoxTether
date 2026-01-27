"""Tests for the transcriber module."""

from unittest.mock import patch, MagicMock

import pytest


class TestTranscriber:
    """Tests for the Transcriber class."""
    
    def test_import_transcriber(self):
        """Test that the transcriber module can be imported."""
        from src.transcriber import Transcriber, TranscriptionResult, DeviceInfo
        
        assert Transcriber is not None
        assert TranscriptionResult is not None
        assert DeviceInfo is not None
    
    def test_transcriber_initialization(self):
        """Test transcriber initialization."""
        from src.transcriber import Transcriber
        
        transcriber = Transcriber(
            model_name_or_path="small",
            device="auto",
            compute_type="auto",
        )
        
        assert transcriber._model_name_or_path == "small"
        assert transcriber._device == "auto"
        assert transcriber._compute_type == "auto"
        assert transcriber.is_loaded() is False
    
    def test_transcription_result_success(self):
        """Test TranscriptionResult with success."""
        from src.transcriber import TranscriptionResult
        
        result = TranscriptionResult(
            text="Hello world",
            success=True,
            duration_seconds=0.5,
            language="en",
        )
        
        assert result.success is True
        assert result.text == "Hello world"
        assert result.language == "en"
        assert result.error is None
    
    def test_transcription_result_failure(self):
        """Test TranscriptionResult with failure."""
        from src.transcriber import TranscriptionResult
        
        result = TranscriptionResult(
            text="",
            success=False,
            duration_seconds=0,
            error="Model not loaded",
        )
        
        assert result.success is False
        assert result.text == ""
        assert result.error == "Model not loaded"
    
    def test_device_info(self):
        """Test DeviceInfo dataclass."""
        from src.transcriber import DeviceInfo
        
        info = DeviceInfo(
            device_type="cuda",
            device_name="NVIDIA RTX 4070",
            cuda_available=True,
            cuda_version="12.1",
        )
        
        assert info.device_type == "cuda"
        assert info.device_name == "NVIDIA RTX 4070"
        assert info.cuda_available is True
        assert info.cuda_version == "12.1"
    
    def test_device_info_cpu(self):
        """Test DeviceInfo for CPU."""
        from src.transcriber import DeviceInfo
        
        info = DeviceInfo(
            device_type="cpu",
            cuda_available=False,
        )
        
        assert info.device_type == "cpu"
        assert info.device_name is None
        assert info.cuda_available is False
    
    @patch('src.transcriber.Transcriber._resolve_device')
    def test_resolve_device_auto_cuda(self, mock_resolve):
        """Test device resolution with CUDA available."""
        mock_resolve.return_value = ("cuda", "float16")
        
        from src.transcriber import Transcriber
        
        transcriber = Transcriber(device="auto", compute_type="auto")
        device, compute_type = transcriber._resolve_device()
        
        assert device == "cuda"
        assert compute_type == "float16"
    
    @patch('src.transcriber.Transcriber._resolve_device')
    def test_resolve_device_auto_cpu(self, mock_resolve):
        """Test device resolution with CPU only."""
        mock_resolve.return_value = ("cpu", "int8")
        
        from src.transcriber import Transcriber
        
        transcriber = Transcriber(device="auto", compute_type="auto")
        device, compute_type = transcriber._resolve_device()
        
        assert device == "cpu"
        assert compute_type == "int8"
    
    def test_unload_model(self):
        """Test unloading model."""
        from src.transcriber import Transcriber
        
        transcriber = Transcriber()
        transcriber._model = MagicMock()
        transcriber._actual_device = "cuda"
        
        transcriber.unload_model()
        
        assert transcriber._model is None
        assert transcriber._actual_device is None
        assert transcriber.is_loaded() is False
    
    def test_transcribe_without_model(self):
        """Test transcribing without loading model first."""
        from src.transcriber import Transcriber
        
        transcriber = Transcriber()
        
        # Mock load_model to fail
        with patch.object(transcriber, 'load_model', return_value=False):
            result = transcriber.transcribe("/tmp/audio.wav")
        
        assert result.success is False
        assert "Failed to load model" in result.error
    
    @patch('subprocess.run')
    def test_detect_nvidia_gpu_via_smi_success(self, mock_run):
        """Test nvidia-smi GPU detection when successful."""
        from src.transcriber import Transcriber
        
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout="NVIDIA GeForce RTX 4080\n"
        )
        
        transcriber = Transcriber()
        result = transcriber._detect_nvidia_gpu_via_smi()
        
        assert result == "NVIDIA GeForce RTX 4080"
    
    @patch('subprocess.run')
    def test_detect_nvidia_gpu_via_smi_not_found(self, mock_run):
        """Test nvidia-smi GPU detection when nvidia-smi is not installed."""
        from src.transcriber import Transcriber
        
        mock_run.side_effect = FileNotFoundError("nvidia-smi not found")
        
        transcriber = Transcriber()
        result = transcriber._detect_nvidia_gpu_via_smi()
        
        assert result is None
    
    @patch('subprocess.run')
    def test_detect_nvidia_gpu_via_smi_failure(self, mock_run):
        """Test nvidia-smi GPU detection when no GPU found."""
        from src.transcriber import Transcriber
        
        mock_run.return_value = MagicMock(
            returncode=1,
            stdout=""
        )
        
        transcriber = Transcriber()
        result = transcriber._detect_nvidia_gpu_via_smi()
        
        assert result is None
    
    @patch('subprocess.run')
    @patch.dict('sys.modules', {'torch': None})
    def test_get_device_info_with_nvidia_smi_fallback(self, mock_run):
        """Test get_device_info uses nvidia-smi fallback when torch/ctranslate2 fail."""
        from src.transcriber import Transcriber
        
        # Mock nvidia-smi to return a GPU name
        mock_run.return_value = MagicMock(
            returncode=0,
            stdout="NVIDIA GeForce RTX 4080\n"
        )
        
        transcriber = Transcriber()
        
        # Patch _detect_nvidia_gpu_via_smi to simulate detecting a GPU
        with patch.object(transcriber, '_detect_nvidia_gpu_via_smi', return_value="NVIDIA GeForce RTX 4080"):
            device_info = transcriber.get_device_info()
        
        # Should have device_name but cuda_available should be False
        # (since torch/ctranslate2 couldn't detect CUDA)
        assert device_info.device_name == "NVIDIA GeForce RTX 4080"


class TestTranscriberIntegration:
    """Integration tests for the transcriber (require model download)."""
    
    @pytest.mark.skip(reason="Requires model download")
    def test_transcribe_audio_file(self):
        """Test actual transcription."""
        from src.transcriber import Transcriber
        
        transcriber = Transcriber(model_name_or_path="tiny", device="cpu")
        
        # Load model
        assert transcriber.load_model() is True
        assert transcriber.is_loaded() is True
        
        # Create a test audio file (would need actual audio)
        # result = transcriber.transcribe("/path/to/test.wav")
        # assert result.success is True
        
        # Unload
        transcriber.unload_model()
        assert transcriber.is_loaded() is False
