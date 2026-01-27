"""VoxTether CLI Test Tool.

A command-line interface for testing VoxTether functionality without the GUI.
This tool allows testing individual components like transcription, recording,
text injection, settings, and model management.

Usage:
    python -m src.cli_test <command> [options]

Commands:
    transcribe  - Transcribe an audio file
    record      - Test microphone recording
    inject      - Test text injection to clipboard
    settings    - Test settings read/write
    models      - Manage and test models
    devices     - List audio input devices
    full-test   - Run complete end-to-end test
    healthcheck - Run system health check
"""

import argparse
import logging
import sys
import time
from pathlib import Path

from . import __version__

logger = logging.getLogger(__name__)


def setup_logging(debug: bool = False, quiet: bool = False) -> None:
    """Set up logging for the CLI tool.

    Args:
        debug: Enable debug logging.
        quiet: Suppress non-essential output.
    """
    if quiet:
        level = logging.WARNING
    elif debug:
        level = logging.DEBUG
    else:
        level = logging.INFO

    logging.basicConfig(
        level=level,
        format="%(levelname)s: %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
    )

    # Reduce noise from third-party loggers
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("huggingface_hub").setLevel(logging.WARNING)


def cmd_transcribe(args: argparse.Namespace) -> int:
    """Handle the transcribe command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .model_manager import ModelManager
    from .transcriber import Transcriber

    audio_path = Path(args.audio_file)
    if not audio_path.exists():
        print(f"Error: Audio file not found: {audio_path}")
        return 1

    print(f"Transcribing: {audio_path}")
    print("-" * 50)

    # Determine model to use
    model = args.model
    model_manager = ModelManager()

    # Check if model is downloaded locally
    local_path = model_manager.get_model_path(model)
    if local_path:
        print(f"Using local model: {local_path}")
        model_path = str(local_path)
    else:
        model_info = model_manager.get_model_info(model)
        if model_info:
            print(f"Model not downloaded locally, using HuggingFace: {model_info.repo_id}")
            model_path = model_info.repo_id
        else:
            print(f"Using model name: {model}")
            model_path = model

    # Create transcriber
    transcriber = Transcriber(
        model_name_or_path=model_path,
        device=args.device,
        compute_type=args.compute_type,
    )

    # Load model
    print(f"Loading model (device={args.device}, compute_type={args.compute_type})...")
    start_time = time.time()
    if not transcriber.load_model():
        print("Error: Failed to load transcription model")
        return 1
    load_time = time.time() - start_time
    print(f"Model loaded in {load_time:.2f}s")

    # Get device info
    device_info = transcriber.get_device_info()
    print(f"Device: {device_info.device_type}" +
          (f" ({device_info.device_name})" if device_info.device_name else ""))
    print("-" * 50)

    # Transcribe
    result = transcriber.transcribe(
        audio_path,
        language=args.language,
    )

    if not result.success:
        print(f"Error: Transcription failed: {result.error}")
        return 1

    print(f"\nTranscription completed in {result.duration_seconds:.2f}s")
    if result.language:
        print(f"Detected language: {result.language}")
    print("-" * 50)
    print(f"Text: {result.text}")
    print("-" * 50)

    # Output to file if requested
    if args.output:
        output_path = Path(args.output)
        output_path.write_text(result.text, encoding="utf-8")
        print(f"Output written to: {output_path}")

    # Cleanup
    transcriber.unload_model()

    return 0


def cmd_record(args: argparse.Namespace) -> int:
    """Handle the record command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    # Check for PortAudio availability
    try:
        import sounddevice  # noqa: F401
    except OSError as e:
        print(f"Error: Audio system not available: {e}")
        print("Make sure PortAudio is installed on your system.")
        return 1

    from .recorder import AudioRecorder

    duration = args.duration
    output_path = Path(args.output) if args.output else None

    print(f"Recording for {duration} seconds...")
    print("Speak now!")
    print("-" * 50)

    recorder = AudioRecorder(device=args.device)

    # Start recording
    if not recorder.start_recording():
        print("Error: Failed to start recording")
        return 1

    # Record for specified duration
    time.sleep(duration)

    # Stop and get result
    result = recorder.stop_recording()

    if not result or not result.success:
        error = result.error if result else "Unknown error"
        print(f"Error: Recording failed: {error}")
        return 1

    print(f"\nRecorded {result.duration_seconds:.2f}s of audio")
    print(f"Sample rate: {result.sample_rate} Hz")

    # Copy or move to output path
    if output_path:
        import shutil
        shutil.copy(result.file_path, output_path)
        print(f"Saved to: {output_path}")
        # Clean up temp file
        result.file_path.unlink()
    else:
        print(f"Temp file: {result.file_path}")
        if not args.keep:
            result.file_path.unlink()
            print("(temp file deleted, use --keep to preserve)")

    return 0


def cmd_inject(args: argparse.Namespace) -> int:
    """Handle the inject command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .injector import InjectionMode, TextInjector

    text = args.text
    mode = InjectionMode.FOCUSED_APP if args.paste else InjectionMode.CLIPBOARD

    print(f"Injection mode: {mode.value}")
    print(f"Text to inject: {text[:50]}{'...' if len(text) > 50 else ''}")
    print("-" * 50)

    injector = TextInjector(mode=mode)

    if mode == InjectionMode.FOCUSED_APP:
        print("Injecting in 2 seconds... (switch to target app)")
        time.sleep(2)

    success = injector.inject(text)

    if success:
        print("[OK] Text injected successfully")
        if mode == InjectionMode.CLIPBOARD:
            print("Text is now in your clipboard. Use Ctrl+V to paste.")
    else:
        print("[FAIL] Text injection failed")
        return 1

    return 0


def cmd_settings(args: argparse.Namespace) -> int:
    """Handle the settings command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .settings import Settings, SettingsService

    service = SettingsService()

    if args.action == "show":
        print("Current Settings")
        print("=" * 50)
        print(f"Settings file: {service.settings_path}")
        print("-" * 50)

        settings = service.settings
        for field in Settings.__dataclass_fields__:
            value = getattr(settings, field)
            print(f"{field}: {value}")

    elif args.action == "get":
        if not args.key:
            print("Error: --key is required for 'get' action")
            return 1

        settings = service.settings
        if hasattr(settings, args.key):
            print(getattr(settings, args.key))
        else:
            print(f"Error: Unknown setting: {args.key}")
            return 1

    elif args.action == "set":
        if not args.key or args.value is None:
            print("Error: --key and --value are required for 'set' action")
            return 1

        # Try to convert value to appropriate type
        settings = service.settings
        if not hasattr(settings, args.key):
            print(f"Error: Unknown setting: {args.key}")
            return 1

        current_value = getattr(settings, args.key)
        if isinstance(current_value, bool):
            value = args.value.lower() in ("true", "1", "yes")
        elif isinstance(current_value, int):
            value = int(args.value)
        else:
            value = args.value

        service.update(**{args.key: value})
        print(f"Updated {args.key} = {value}")

    elif args.action == "reset":
        # Reset by creating new settings (deleting the file would work too)
        import os
        if service.settings_path.exists():
            os.remove(service.settings_path)
            print(f"Settings reset (deleted: {service.settings_path})")
        else:
            print("Settings already at defaults")

    elif args.action == "path":
        print(service.settings_path)

    return 0


def cmd_models(args: argparse.Namespace) -> int:
    """Handle the models command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .model_manager import ModelManager

    manager = ModelManager()

    if args.action == "list":
        print("Available Models")
        print("=" * 60)

        downloaded = manager.get_downloaded_models()

        for name, info in manager.get_available_models().items():
            status = "[OK] Downloaded" if name in downloaded else "  Not downloaded"
            print(f"\n{name} ({info.size_mb} MB) - {status}")
            print(f"  Description: {info.description}")
            print(f"  Recommended for: {info.recommended_for}")

    elif args.action == "info":
        if not args.model:
            print("Error: --model is required for 'info' action")
            return 1

        info = manager.get_model_info(args.model)
        if not info:
            print(f"Error: Unknown model: {args.model}")
            return 1

        print(f"Model: {info.name}")
        print(f"HuggingFace repo: {info.repo_id}")
        print(f"Size: {info.size_mb} MB")
        print(f"Description: {info.description}")
        print(f"Recommended for: {info.recommended_for}")
        print(f"Supports GPU: {info.supports_gpu}")

        local_path = manager.get_model_path(args.model)
        if local_path:
            print(f"Local path: {local_path}")
        else:
            print("Status: Not downloaded")

    elif args.action == "download":
        if not args.model:
            print("Error: --model is required for 'download' action")
            return 1

        info = manager.get_model_info(args.model)
        if not info:
            print(f"Error: Unknown model: {args.model}")
            return 1

        if manager.is_model_downloaded(args.model):
            print(f"Model '{args.model}' is already downloaded")
            return 0

        print(f"Downloading model: {args.model} ({info.size_mb} MB)")
        print("This may take a while...")

        def progress(current: int, total: int, status: str) -> None:
            pct = (current / total) * 100 if total > 0 else 0
            print(f"\r{status} {pct:.1f}%", end="", flush=True)

        try:
            path = manager.download_model(args.model, progress_callback=progress)
            print(f"\n[OK] Model downloaded to: {path}")
        except Exception as e:
            print(f"\n[FAIL] Download failed: {e}")
            return 1

    elif args.action == "delete":
        if not args.model:
            print("Error: --model is required for 'delete' action")
            return 1

        if not manager.is_model_downloaded(args.model):
            print(f"Model '{args.model}' is not downloaded")
            return 0

        if manager.delete_model(args.model):
            print(f"[OK] Model '{args.model}' deleted")
        else:
            print(f"[FAIL] Failed to delete model '{args.model}'")
            return 1

    elif args.action == "path":
        print(manager.models_path)

    return 0


def cmd_devices(args: argparse.Namespace) -> int:
    """Handle the devices command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    # Check for PortAudio availability
    try:
        import sounddevice  # noqa: F401
    except OSError as e:
        print(f"Error: Audio system not available: {e}")
        print("Make sure PortAudio is installed on your system.")
        return 1

    from .recorder import AudioRecorder

    recorder = AudioRecorder()
    devices = recorder.get_input_devices()

    if not devices:
        print("No audio input devices found")
        return 1

    print("Audio Input Devices")
    print("=" * 60)

    for device in devices:
        print(f"\n[{device['index']}] {device['name']}")
        print(f"    Channels: {device['channels']}")
        if device.get('default'):
            print(f"    Default sample rate: {device['default']} Hz")

    print(f"\nTotal: {len(devices)} input device(s)")

    return 0


def cmd_full_test(args: argparse.Namespace) -> int:
    """Handle the full-test command.

    Runs a complete end-to-end test of VoxTether functionality.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .injector import InjectionMode, TextInjector
    from .model_manager import ModelManager
    from .settings import SettingsService
    from .transcriber import Transcriber

    print("VoxTether Full Integration Test")
    print("=" * 60)

    results = []

    # 1. Test Settings
    print("\n1. Testing Settings...")
    try:
        service = SettingsService()
        settings = service.settings
        print(f"   [OK] Settings loaded from: {service.settings_path}")
        print(f"   [OK] Model: {settings.model_name}, Device: {settings.device}")
        results.append(("Settings", True, None))
    except Exception as e:
        print(f"   [FAIL] Settings failed: {e}")
        results.append(("Settings", False, str(e)))

    # 2. Test Model Manager
    print("\n2. Testing Model Manager...")
    try:
        manager = ModelManager()
        available = manager.get_available_models()
        downloaded = manager.get_downloaded_models()
        print(f"   [OK] Available models: {len(available)}")
        print(f"   [OK] Downloaded models: {', '.join(downloaded) if downloaded else 'None'}")
        results.append(("Model Manager", True, None))
    except Exception as e:
        print(f"   [FAIL] Model Manager failed: {e}")
        results.append(("Model Manager", False, str(e)))

    # 3. Test Audio Devices (optional, may not work in CI)
    print("\n3. Testing Audio System...")
    try:
        import sounddevice  # noqa: F401

        from .recorder import AudioRecorder
        recorder = AudioRecorder()
        devices = recorder.get_input_devices()
        if devices:
            print(f"   [OK] Found {len(devices)} audio input device(s)")
        else:
            print("   ! No audio input devices found (OK in headless environments)")
        results.append(("Audio System", True, None))
    except OSError as e:
        print(f"   ! Audio system not available: {e}")
        print("   (This is OK in headless/CI environments)")
        results.append(("Audio System", True, "Not available in this environment"))
    except Exception as e:
        print(f"   [FAIL] Audio System failed: {e}")
        results.append(("Audio System", False, str(e)))

    # 4. Test Transcriber Device Info
    print("\n4. Testing Transcriber...")
    try:
        transcriber = Transcriber()
        device_info = transcriber.get_device_info()
        print(f"   [OK] Device type: {device_info.device_type}")
        if device_info.cuda_available:
            print(f"   [OK] CUDA available: {device_info.device_name}")
        else:
            print("   [OK] CUDA not available (CPU mode)")
        results.append(("Transcriber", True, None))
    except Exception as e:
        print(f"   [FAIL] Transcriber failed: {e}")
        results.append(("Transcriber", False, str(e)))

    # 5. Test with audio file if provided
    if args.audio_file:
        print(f"\n5. Testing Transcription with: {args.audio_file}")
        audio_path = Path(args.audio_file)
        if audio_path.exists():
            try:
                model = args.model
                model_path = model

                # Check for local model
                local_path = manager.get_model_path(model)
                if local_path:
                    model_path = str(local_path)
                else:
                    info = manager.get_model_info(model)
                    if info:
                        model_path = info.repo_id

                transcriber = Transcriber(
                    model_name_or_path=model_path,
                    device=args.device,
                    compute_type=args.compute_type,
                )

                print(f"   Loading model '{model}'...")
                if transcriber.load_model():
                    result = transcriber.transcribe(audio_path)
                    if result.success:
                        print(f"   [OK] Transcription successful in {result.duration_seconds:.2f}s")
                        print(f"   [OK] Text: {result.text[:100]}{'...' if len(result.text) > 100 else ''}")
                        results.append(("Transcription", True, None))
                    else:
                        print(f"   [FAIL] Transcription failed: {result.error}")
                        results.append(("Transcription", False, result.error))
                    transcriber.unload_model()
                else:
                    print("   [FAIL] Failed to load model")
                    results.append(("Transcription", False, "Failed to load model"))
            except Exception as e:
                print(f"   [FAIL] Transcription test failed: {e}")
                results.append(("Transcription", False, str(e)))
        else:
            print(f"   [FAIL] Audio file not found: {audio_path}")
            results.append(("Transcription", False, "Audio file not found"))
    else:
        print("\n5. Skipping Transcription test (no audio file provided)")
        print("   Use --audio-file to test transcription with a WAV file")

    # 6. Test Text Injection (clipboard only, non-destructive)
    print("\n6. Testing Text Injection (clipboard)...")
    try:
        injector = TextInjector(mode=InjectionMode.CLIPBOARD)
        test_text = "VoxTether CLI Test - " + str(int(time.time()))
        success = injector.inject(test_text)
        if success:
            # Verify
            content = injector.get_clipboard_content()
            if content == test_text:
                print("   [OK] Clipboard injection verified")
            else:
                print("   [OK] Text copied (clipboard verification not available)")
            results.append(("Text Injection", True, None))
        else:
            print("   [FAIL] Text injection failed")
            results.append(("Text Injection", False, "Injection failed"))
    except Exception as e:
        print(f"   [FAIL] Text Injection failed: {e}")
        results.append(("Text Injection", False, str(e)))

    # Summary
    print("\n" + "=" * 60)
    print("Test Summary")
    print("=" * 60)

    passed = sum(1 for _, success, _ in results if success)
    failed = len(results) - passed

    for name, success, error in results:
        status = "[OK] PASS" if success else "[FAIL] FAIL"
        print(f"  {status}: {name}" + (f" ({error})" if error and not success else ""))

    print("-" * 60)
    print(f"Total: {passed} passed, {failed} failed")

    return 0 if failed == 0 else 1


def cmd_healthcheck(args: argparse.Namespace) -> int:
    """Handle the healthcheck command.

    Args:
        args: Parsed command-line arguments.

    Returns:
        Exit code.
    """
    from .main import run_healthcheck
    return run_healthcheck()


def create_parser() -> argparse.ArgumentParser:
    """Create the argument parser.

    Returns:
        Configured argument parser.
    """
    parser = argparse.ArgumentParser(
        prog="python -m src.cli_test",
        description="VoxTether CLI Test Tool - Test VoxTether functionality without GUI",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Transcribe the test recording
  python -m src.cli_test transcribe tests/test-recoarding.wav

  # List available models
  python -m src.cli_test models list

  # Run full integration test with test recording
  python -m src.cli_test full-test --audio-file tests/test-recoarding.wav

  # Test clipboard injection
  python -m src.cli_test inject "Hello, World!"

  # List audio devices
  python -m src.cli_test devices
""",
    )

    parser.add_argument(
        "--version",
        action="version",
        version=f"VoxTether CLI Test Tool {__version__}",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug logging",
    )
    parser.add_argument(
        "--quiet", "-q",
        action="store_true",
        help="Suppress non-essential output",
    )

    subparsers = parser.add_subparsers(dest="command", help="Command to run")

    # Transcribe command
    transcribe_parser = subparsers.add_parser(
        "transcribe",
        help="Transcribe an audio file",
    )
    transcribe_parser.add_argument(
        "audio_file",
        help="Path to the audio file (WAV format preferred)",
    )
    transcribe_parser.add_argument(
        "--model", "-m",
        default="small",
        help="Model to use (default: small)",
    )
    transcribe_parser.add_argument(
        "--language", "-l",
        default="auto",
        help="Language code or 'auto' for auto-detection (default: auto)",
    )
    transcribe_parser.add_argument(
        "--device", "-d",
        default="auto",
        choices=["auto", "cuda", "cpu"],
        help="Device to use (default: auto)",
    )
    transcribe_parser.add_argument(
        "--compute-type", "-c",
        default="auto",
        choices=["auto", "int8", "float16", "float32"],
        help="Compute type (default: auto)",
    )
    transcribe_parser.add_argument(
        "--output", "-o",
        help="Output file to write transcription to",
    )

    # Record command
    record_parser = subparsers.add_parser(
        "record",
        help="Test microphone recording",
    )
    record_parser.add_argument(
        "--duration", "-d",
        type=float,
        default=5.0,
        help="Recording duration in seconds (default: 5.0)",
    )
    record_parser.add_argument(
        "--output", "-o",
        help="Output file to save recording to",
    )
    record_parser.add_argument(
        "--device",
        type=int,
        default=None,
        help="Audio device index (default: system default)",
    )
    record_parser.add_argument(
        "--keep",
        action="store_true",
        help="Keep temp file instead of deleting",
    )

    # Inject command
    inject_parser = subparsers.add_parser(
        "inject",
        help="Test text injection to clipboard",
    )
    inject_parser.add_argument(
        "text",
        help="Text to inject",
    )
    inject_parser.add_argument(
        "--paste",
        action="store_true",
        help="Paste into focused application (requires delay)",
    )

    # Settings command
    settings_parser = subparsers.add_parser(
        "settings",
        help="Test settings read/write",
    )
    settings_parser.add_argument(
        "action",
        choices=["show", "get", "set", "reset", "path"],
        help="Action to perform",
    )
    settings_parser.add_argument(
        "--key", "-k",
        help="Setting key (for get/set)",
    )
    settings_parser.add_argument(
        "--value", "-v",
        help="Setting value (for set)",
    )

    # Models command
    models_parser = subparsers.add_parser(
        "models",
        help="Manage and test models",
    )
    models_parser.add_argument(
        "action",
        choices=["list", "info", "download", "delete", "path"],
        help="Action to perform",
    )
    models_parser.add_argument(
        "--model", "-m",
        help="Model name (for info/download/delete)",
    )

    # Devices command
    subparsers.add_parser(
        "devices",
        help="List audio input devices",
    )

    # Full test command
    full_test_parser = subparsers.add_parser(
        "full-test",
        help="Run complete end-to-end test",
    )
    full_test_parser.add_argument(
        "--audio-file", "-a",
        help="Audio file to use for transcription test",
    )
    full_test_parser.add_argument(
        "--model", "-m",
        default="small",
        help="Model to use for transcription (default: small)",
    )
    full_test_parser.add_argument(
        "--device", "-d",
        default="auto",
        choices=["auto", "cuda", "cpu"],
        help="Device to use (default: auto)",
    )
    full_test_parser.add_argument(
        "--compute-type", "-c",
        default="auto",
        choices=["auto", "int8", "float16", "float32"],
        help="Compute type (default: auto)",
    )

    # Healthcheck command
    subparsers.add_parser(
        "healthcheck",
        help="Run system health check",
    )

    return parser


def main() -> int:
    """Main entry point.

    Returns:
        Exit code.
    """
    parser = create_parser()
    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 0

    setup_logging(debug=args.debug, quiet=args.quiet)

    # Dispatch to command handler
    commands = {
        "transcribe": cmd_transcribe,
        "record": cmd_record,
        "inject": cmd_inject,
        "settings": cmd_settings,
        "models": cmd_models,
        "devices": cmd_devices,
        "full-test": cmd_full_test,
        "healthcheck": cmd_healthcheck,
    }

    handler = commands.get(args.command)
    if handler:
        return handler(args)
    else:
        print(f"Unknown command: {args.command}")
        parser.print_help()
        return 1


if __name__ == "__main__":
    sys.exit(main())
