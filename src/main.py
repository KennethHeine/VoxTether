"""VoxTether - Push-to-talk dictation for Windows.

Fully offline speech-to-text using faster-whisper.
"""

import argparse
import logging
import os
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Optional

from . import __version__
from .hotkey import HotkeyListener
from .injector import InjectionMode, TextInjector
from .model_manager import ModelManager
from .recorder import AudioRecorder
from .settings import (
    Settings,
    SettingsService,
    get_logs_path,
    get_models_path,
)
from .transcriber import Transcriber
from .tray import TrayManager


def setup_logging(debug: bool = False) -> None:
    """Set up logging for the application.
    
    Args:
        debug: Whether to enable debug logging.
    """
    log_level = logging.DEBUG if debug else logging.INFO

    # Create logs directory
    logs_path = get_logs_path()
    log_file = logs_path / "voxtether.log"

    # Configure logging
    logging.basicConfig(
        level=log_level,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        handlers=[
            logging.FileHandler(log_file, encoding="utf-8"),
            logging.StreamHandler(sys.stdout),
        ],
    )

    # Reduce noise from third-party loggers
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("huggingface_hub").setLevel(logging.WARNING)


logger = logging.getLogger(__name__)


def get_icon_path() -> Optional[Path]:
    """Get the path to the application icon.

    Returns:
        Path to the icon file, or None if not found.
    """
    # Try relative to this module (development)
    module_dir = Path(__file__).parent
    icon_path = module_dir.parent / "assets" / "icon.ico"
    if icon_path.exists():
        return icon_path

    # Try relative to sys.executable (frozen app)
    if hasattr(sys, "_MEIPASS"):
        icon_path = Path(sys._MEIPASS) / "assets" / "icon.ico"
        if icon_path.exists():
            return icon_path

    return None


class VoxTetherApp:
    """Main VoxTether application controller."""

    def __init__(self):
        """Initialize the application."""
        self._settings_service = SettingsService()
        self._model_manager = ModelManager()
        self._transcriber: Transcriber | None = None
        self._recorder = AudioRecorder()
        self._injector = TextInjector()
        self._hotkey_listener = HotkeyListener()
        self._tray_manager = TrayManager(icon_path=get_icon_path(), tooltip="VoxTether")
        self._recording_indicator = None  # Lazy initialization

        self._is_running = False
        self._is_recording = False
        self._shutdown_event = threading.Event()

    @property
    def settings(self) -> Settings:
        """Get the current settings."""
        return self._settings_service.settings

    def _setup_transcriber(self) -> bool:
        """Set up the transcriber with current settings.
        
        Returns:
            True if setup was successful, False otherwise.
        """
        settings = self.settings

        # Get model path or name
        model = self._model_manager.get_model_for_transcriber(settings.model_name)

        self._transcriber = Transcriber(
            model_name_or_path=model,
            device=settings.device,
            compute_type=settings.compute_type,
        )

        # Preload the model to avoid threading issues with pystray
        # Loading in a background thread while pystray is running can cause GIL issues
        logger.info("Preloading transcription model...")
        if not self._transcriber.load_model():
            logger.error("Failed to preload transcription model")
            return False

        return True

    def _setup_hotkeys(self) -> bool:
        """Set up global hotkeys.
        
        Returns:
            True if setup was successful, False otherwise.
        """
        settings = self.settings

        # Register push-to-talk hotkey
        return self._hotkey_listener.register_push_to_talk(
            settings.hotkey,
            on_press=self._on_hotkey_press,
            on_release=self._on_hotkey_release,
        )

    def _setup_tray(self) -> bool:
        """Set up the system tray.
        
        Returns:
            True if setup was successful, False otherwise.
        """
        self._tray_manager.set_callbacks(
            on_settings=self._show_settings,
            on_test_mic=self._test_microphone,
            on_about=self._show_about,
            on_exit=self._shutdown,
            on_open_models=self._open_models_folder,
            on_open_logs=self._open_logs_folder,
            on_check_updates=self._check_updates,
        )

        return self._tray_manager.start()

    def _get_recording_indicator(self):
        """Get or create the recording indicator.
        
        Returns:
            RecordingIndicator instance, or None if disabled.
        """
        if not self.settings.show_recording_indicator:
            return None

        if self._recording_indicator is None:
            from .ui.recording_indicator import RecordingIndicator
            self._recording_indicator = RecordingIndicator()
            self._recording_indicator.start()

        return self._recording_indicator

    def _on_hotkey_press(self) -> None:
        """Handle push-to-talk hotkey press."""
        if self._is_recording:
            return

        logger.info("Hotkey pressed - starting recording")
        self._is_recording = True

        # Update tray icon
        self._tray_manager.set_recording(True)

        # Show recording indicator
        indicator = self._get_recording_indicator()
        if indicator:
            indicator.show_recording()

        # Start recording
        if not self._recorder.start_recording():
            logger.error("Failed to start recording")
            self._is_recording = False
            self._tray_manager.set_recording(False)
            if indicator:
                indicator.hide()

            if self.settings.show_notifications:
                self._tray_manager.show_notification(
                    "Recording Failed",
                    "Could not start recording. Check microphone.",
                )

    def _on_hotkey_release(self) -> None:
        """Handle push-to-talk hotkey release."""
        if not self._is_recording:
            return

        logger.info("Hotkey released - stopping recording")
        self._is_recording = False

        # Update tray icon
        self._tray_manager.set_recording(False)
        self._tray_manager.set_status("Transcribing...")

        # Show transcribing indicator
        indicator = self._get_recording_indicator()
        if indicator:
            indicator.show_transcribing()

        # Stop recording
        result = self._recorder.stop_recording()

        if not result or not result.success:
            logger.error(f"Recording failed: {result.error if result else 'Unknown error'}")
            self._tray_manager.set_status("Ready")
            if indicator:
                indicator.hide()
            return

        logger.info(f"Recorded {result.duration_seconds:.2f}s audio")

        # Transcribe in background
        def transcribe():
            try:
                self._transcribe_and_inject(result.file_path)
            finally:
                # Hide indicator when done
                if indicator:
                    indicator.hide()
                # Clean up temp file
                try:
                    result.file_path.unlink()
                except FileNotFoundError:
                    pass  # File already deleted
                except OSError as e:
                    logger.warning(f"Failed to delete temp file: {e}")

        thread = threading.Thread(target=transcribe, daemon=True)
        thread.start()

    def _transcribe_and_inject(self, audio_path: Path) -> None:
        """Transcribe audio and inject the text.
        
        Args:
            audio_path: Path to the audio file.
        """
        if not self._transcriber:
            logger.error("Transcriber not initialized")
            self._tray_manager.set_status("Ready")
            return

        # Transcribe
        result = self._transcriber.transcribe(
            audio_path,
            language=self.settings.language,
        )

        self._tray_manager.set_status("Ready")

        if not result.success:
            logger.error(f"Transcription failed: {result.error}")

            if self.settings.show_notifications:
                self._tray_manager.show_notification(
                    "Transcription Failed",
                    result.error or "Unknown error",
                )
            return

        text = result.text.strip()
        if not text:
            logger.info("No speech detected")
            return

        logger.info(f"Transcribed in {result.duration_seconds:.2f}s: '{text}'")

        # Inject text
        mode = (
            InjectionMode.FOCUSED_APP
            if self.settings.output_mode == "focused_app"
            else InjectionMode.CLIPBOARD
        )
        self._injector.mode = mode

        if not self._injector.inject(text):
            logger.error("Failed to inject text")

            if self.settings.show_notifications:
                self._tray_manager.show_notification(
                    "Injection Failed",
                    "Could not inject text. Check clipboard.",
                )
            return

        if self.settings.show_notifications:
            preview = text[:50] + "..." if len(text) > 50 else text
            self._tray_manager.show_notification(
                "Transcribed" if mode == InjectionMode.CLIPBOARD else "Text Inserted",
                preview,
            )

    def _show_settings(self) -> None:
        """Show the settings window."""
        from .ui.settings_window import SettingsWindow

        def on_save(settings: Settings) -> None:
            # Re-setup with new settings
            self._setup_transcriber()
            self._hotkey_listener.unregister_all()
            self._setup_hotkeys()

        window = SettingsWindow(
            self._settings_service,
            self._model_manager,
            self._transcriber or Transcriber(),
            on_save=on_save,
        )
        window.show()

    def _test_microphone(self) -> None:
        """Test the microphone with visual feedback."""
        from .ui.mic_test import MicTestWindow

        def on_close():
            self._tray_manager.set_status("Ready")

        self._tray_manager.set_status("Testing...")
        window = MicTestWindow(on_close=on_close)
        window.show()

    def _show_about(self) -> None:
        """Show the about dialog."""
        import tkinter as tk
        from tkinter import messagebox

        # Get device info
        device_info = self._transcriber.get_device_info() if self._transcriber else None
        device_text = "Unknown"
        if device_info:
            device_text = (
                f"{device_info.device_type.upper()}"
                + (f" ({device_info.device_name})" if device_info.device_name else "")
            )

        message = (
            f"VoxTether v{__version__}\n\n"
            f"Push-to-talk dictation for Windows.\n"
            f"Fully offline using faster-whisper.\n\n"
            f"Model: {self.settings.model_name}\n"
            f"Device: {device_text}\n\n"
            f"https://github.com/KennethHeine/VoxTether"
        )

        root = tk.Tk()
        root.withdraw()
        messagebox.showinfo("About VoxTether", message)
        root.destroy()

    def _open_models_folder(self) -> None:
        """Open the models folder in file explorer."""
        path = get_models_path()
        if os.name == "nt":
            os.startfile(str(path))
        else:
            subprocess.run(["xdg-open", str(path)], check=False)

    def _open_logs_folder(self) -> None:
        """Open the logs folder in file explorer."""
        path = get_logs_path()
        if os.name == "nt":
            os.startfile(str(path))
        else:
            subprocess.run(["xdg-open", str(path)], check=False)

    def _check_updates(self) -> None:
        """Check for updates."""
        import webbrowser
        webbrowser.open("https://github.com/KennethHeine/VoxTether/releases")

    def _check_first_run(self) -> bool:
        """Check if this is the first run and show model setup if needed.
        
        Returns:
            True if we can continue, False if user exited.
        """
        if self.settings.first_run_completed:
            return True

        # Check if any model is available
        downloaded = self._model_manager.get_downloaded_models()
        if downloaded:
            self._settings_service.update(first_run_completed=True)
            return True

        # Show model setup
        from .ui.model_setup import ModelSetupWindow

        setup_complete = threading.Event()
        selected_model: str | None = None

        def on_complete(model_name: str) -> None:
            nonlocal selected_model
            selected_model = model_name
            setup_complete.set()

        def on_skip() -> None:
            setup_complete.set()

        window = ModelSetupWindow(
            self._model_manager,
            Transcriber(),
            on_complete=on_complete,
            on_skip=on_skip,
        )
        window.show()

        # Wait for setup to complete
        setup_complete.wait()

        if selected_model:
            self._settings_service.update(
                model_name=selected_model,
                first_run_completed=True,
            )

        return True

    def run(self) -> int:
        """Run the application.
        
        Returns:
            Exit code.
        """
        logger.info(f"Starting VoxTether v{__version__}")

        # Check first run
        if not self._check_first_run():
            return 0

        # Set up components
        if not self._setup_transcriber():
            logger.error("Failed to set up transcriber")
            return 1

        if not self._setup_hotkeys():
            logger.error("Failed to set up hotkeys")
            return 1

        if not self._setup_tray():
            logger.error("Failed to set up tray")
            return 1

        logger.info(f"VoxTether started. Hotkey: {self.settings.hotkey}")

        if self.settings.show_notifications:
            self._tray_manager.show_notification(
                "VoxTether Started",
                f"Press {self.settings.hotkey} to record.",
            )

        self._is_running = True

        # Main loop
        try:
            while self._is_running and not self._shutdown_event.is_set():
                time.sleep(0.1)
        except KeyboardInterrupt:
            logger.info("Received keyboard interrupt")

        self._cleanup()
        return 0

    def _shutdown(self) -> None:
        """Trigger shutdown."""
        logger.info("Shutdown requested")
        self._is_running = False
        self._shutdown_event.set()

    def _cleanup(self) -> None:
        """Clean up resources."""
        logger.info("Cleaning up...")

        # Stop recording if active
        if self._is_recording:
            self._recorder.stop_recording()

        # Unregister hotkeys
        self._hotkey_listener.unregister_all()

        # Stop recording indicator
        if self._recording_indicator:
            self._recording_indicator.stop()

        # Stop tray
        self._tray_manager.stop()

        # Unload model
        if self._transcriber:
            self._transcriber.unload_model()

        # Clean up temp files
        self._recorder.cleanup_temp_files()

        logger.info("Cleanup complete")


def run_healthcheck() -> int:
    """Run a healthcheck and print diagnostics.
    
    Returns:
        Exit code (0 for success, 1 for issues).
    """
    print("VoxTether Healthcheck")
    print("=" * 50)

    issues = []

    # Check settings
    try:
        settings_service = SettingsService()
        print(f"✓ Settings loaded from: {settings_service.settings_path}")
    except Exception as e:
        print(f"✗ Settings failed: {e}")
        issues.append("settings")

    # Check models
    try:
        model_manager = ModelManager()
        downloaded = model_manager.get_downloaded_models()
        if downloaded:
            print(f"✓ Models available: {', '.join(downloaded)}")
        else:
            print("! No models downloaded yet")
    except Exception as e:
        print(f"✗ Model check failed: {e}")
        issues.append("models")

    # Check audio devices
    try:
        recorder = AudioRecorder()
        devices = recorder.get_input_devices()
        if devices:
            print(f"✓ Audio devices found: {len(devices)}")
            for d in devices[:3]:
                print(f"  - {d['name']}")
        else:
            print("✗ No audio input devices found")
            issues.append("audio")
    except Exception as e:
        print(f"✗ Audio check failed: {e}")
        issues.append("audio")

    # Check GPU
    try:
        transcriber = Transcriber()
        device_info = transcriber.get_device_info()
        if device_info.cuda_available:
            print(f"✓ CUDA available: {device_info.device_name}")
            if device_info.cuda_version:
                print(f"  CUDA version: {device_info.cuda_version}")
        else:
            print("! CUDA not available (CPU mode)")
    except Exception as e:
        print(f"✗ GPU check failed: {e}")

    print("=" * 50)

    if issues:
        print(f"Issues found: {', '.join(issues)}")
        return 1
    else:
        print("All checks passed!")
        return 0


def main() -> int:
    """Main entry point.
    
    Returns:
        Exit code.
    """
    parser = argparse.ArgumentParser(
        description="VoxTether - Push-to-talk dictation for Windows",
    )
    parser.add_argument(
        "--version",
        action="version",
        version=f"VoxTether {__version__}",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug logging",
    )
    parser.add_argument(
        "--healthcheck",
        action="store_true",
        help="Run healthcheck and exit",
    )

    args = parser.parse_args()

    # Set up logging
    setup_logging(debug=args.debug)

    if args.healthcheck:
        return run_healthcheck()

    # Run the app
    app = VoxTetherApp()
    return app.run()


if __name__ == "__main__":
    sys.exit(main())
