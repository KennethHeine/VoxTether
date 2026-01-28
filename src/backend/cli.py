#!/usr/bin/env python3
"""VoxTether Backend CLI - Command line tool for managing the backend server."""

import argparse
import asyncio
import sys
from pathlib import Path

# Add current directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

from config import settings
from services.model_manager import ModelManager, AVAILABLE_MODELS


def print_banner():
    """Print the VoxTether banner."""
    print("""
╔═══════════════════════════════════════════════════════════════╗
║                    VoxTether Backend CLI                      ║
║           Speech-to-Text Transcription Server                 ║
╚═══════════════════════════════════════════════════════════════╝
""")


def cmd_list_models(args):
    """List all available models and their download status."""
    manager = ModelManager(settings.models_path)
    models = manager.list_models()
    
    print(f"\nModels directory: {settings.models_path}\n")
    print("Available Models:")
    print("-" * 80)
    print(f"{'Name':<20} {'Size':<12} {'Downloaded':<12} {'Description'}")
    print("-" * 80)
    
    for model in models:
        status = "✓ Yes" if model["downloaded"] else "✗ No"
        size = f"{model['size_mb']} MB"
        print(f"{model['name']:<20} {size:<12} {status:<12} {model['description']}")
    
    print("-" * 80)
    print(f"\nTotal models: {len(models)}")
    downloaded = sum(1 for m in models if m["downloaded"])
    print(f"Downloaded: {downloaded}")


def cmd_download_model(args):
    """Download a model."""
    model_name = args.model_name
    
    if model_name not in AVAILABLE_MODELS:
        print(f"Error: Unknown model '{model_name}'")
        print(f"Available models: {', '.join(AVAILABLE_MODELS.keys())}")
        sys.exit(1)
    
    manager = ModelManager(settings.models_path)
    
    if manager.is_model_downloaded(model_name):
        if not args.force:
            print(f"Model '{model_name}' is already downloaded.")
            print("Use --force to re-download.")
            return
        else:
            print(f"Re-downloading model '{model_name}'...")
            manager.delete_model(model_name)
    
    model_info = AVAILABLE_MODELS[model_name]
    print(f"\nDownloading model: {model_name}")
    print(f"  Display name: {model_info.get('display_name', model_name)}")
    print(f"  Size: ~{model_info.get('size_mb', 'unknown')} MB")
    print(f"  Repository: {model_info.get('repo_id', 'N/A')}")
    print(f"  Target: {settings.models_path}/{model_name}")
    print()
    
    async def download():
        last_progress = -1
        async for progress in manager.download_model_async(model_name):
            if progress.status == "downloading":
                pct = int(progress.progress)
                if pct != last_progress:
                    bar_width = 40
                    filled = int(bar_width * pct / 100)
                    bar = "█" * filled + "░" * (bar_width - filled)
                    print(f"\r[{bar}] {pct:3}% ({progress.downloaded_mb:.1f}/{progress.total_mb:.1f} MB)", end="", flush=True)
                    last_progress = pct
            elif progress.status == "complete":
                print(f"\r[{'█' * 40}] 100%")
                print(f"\n✓ Model '{model_name}' downloaded successfully!")
            elif progress.status == "error":
                print(f"\n✗ Download failed: {progress.error}")
                sys.exit(1)
    
    asyncio.run(download())


def cmd_delete_model(args):
    """Delete a downloaded model."""
    model_name = args.model_name
    manager = ModelManager(settings.models_path)
    
    if not manager.is_model_downloaded(model_name):
        print(f"Model '{model_name}' is not downloaded.")
        sys.exit(1)
    
    if not args.yes:
        confirm = input(f"Are you sure you want to delete model '{model_name}'? [y/N]: ")
        if confirm.lower() != 'y':
            print("Cancelled.")
            return
    
    if manager.delete_model(model_name):
        print(f"✓ Model '{model_name}' deleted successfully.")
    else:
        print(f"✗ Failed to delete model '{model_name}'.")
        sys.exit(1)


def cmd_config_show(args):
    """Show current configuration."""
    print("\nCurrent Configuration:")
    print("-" * 50)
    print(f"Host:           {settings.host}")
    print(f"Port:           {settings.port}")
    print(f"Debug:          {settings.debug}")
    print(f"Models path:    {settings.models_path}")
    print(f"Default model:  {settings.default_model}")
    print(f"Preload model:  {settings.preload_model}")
    print(f"Device:         {settings.device}")
    print(f"Compute type:   {settings.compute_type}")
    print(f"Language:       {settings.default_language}")
    print("-" * 50)
    print("\nEnvironment variables (prefix: VOXTETHER_):")
    print("  VOXTETHER_HOST, VOXTETHER_PORT, VOXTETHER_DEBUG")
    print("  VOXTETHER_MODELS_PATH, VOXTETHER_DEFAULT_MODEL")
    print("  VOXTETHER_PRELOAD_MODEL, VOXTETHER_DEVICE")
    print("  VOXTETHER_COMPUTE_TYPE, VOXTETHER_DEFAULT_LANGUAGE")


def cmd_start_server(args):
    """Start the backend server."""
    import uvicorn
    
    host = args.host or settings.host
    port = args.port or settings.port
    
    print("\nStarting VoxTether backend server...")
    print(f"  URL: http://{host}:{port}")
    print(f"  API docs: http://{host}:{port}/docs")
    print(f"  Models path: {settings.models_path}")
    print()
    
    uvicorn.run(
        "main:app",
        host=host,
        port=port,
        reload=args.reload,
        log_level="debug" if args.debug else "info",
    )


def cmd_info(args):
    """Show system and GPU information."""
    print("\nSystem Information:")
    print("-" * 50)
    
    # Check for CUDA
    try:
        import torch
        cuda_available = torch.cuda.is_available()
        print(f"PyTorch version: {torch.__version__}")
        print(f"CUDA available:  {cuda_available}")
        if cuda_available:
            print(f"CUDA version:    {torch.version.cuda}")
            print(f"GPU count:       {torch.cuda.device_count()}")
            for i in range(torch.cuda.device_count()):
                print(f"  GPU {i}: {torch.cuda.get_device_name(i)}")
    except ImportError:
        print("PyTorch: Not installed")
    
    # Check for faster-whisper
    try:
        import importlib.util
        if importlib.util.find_spec("faster_whisper"):
            print("\nfaster-whisper: Available")
        else:
            print("\nfaster-whisper: Not installed")
    except ImportError:
        print("\nfaster-whisper: Not installed")
    
    print("-" * 50)


def main():
    """Main entry point for CLI."""
    parser = argparse.ArgumentParser(
        prog="voxtether-cli",
        description="VoxTether Backend CLI - Manage the speech-to-text server",
    )
    parser.add_argument("--version", action="version", version="VoxTether Backend 2.0.0")
    
    subparsers = parser.add_subparsers(dest="command", help="Available commands")
    
    # models list
    list_parser = subparsers.add_parser("list", help="List available models")
    list_parser.set_defaults(func=cmd_list_models)
    
    # models download
    download_parser = subparsers.add_parser("download", help="Download a model")
    download_parser.add_argument("model_name", help="Name of the model to download")
    download_parser.add_argument("--force", "-f", action="store_true", help="Re-download even if already exists")
    download_parser.set_defaults(func=cmd_download_model)
    
    # models delete
    delete_parser = subparsers.add_parser("delete", help="Delete a downloaded model")
    delete_parser.add_argument("model_name", help="Name of the model to delete")
    delete_parser.add_argument("--yes", "-y", action="store_true", help="Skip confirmation")
    delete_parser.set_defaults(func=cmd_delete_model)
    
    # config
    config_parser = subparsers.add_parser("config", help="Show configuration")
    config_parser.set_defaults(func=cmd_config_show)
    
    # serve
    serve_parser = subparsers.add_parser("serve", help="Start the backend server")
    serve_parser.add_argument("--host", "-H", help="Host to bind to")
    serve_parser.add_argument("--port", "-p", type=int, help="Port to bind to")
    serve_parser.add_argument("--reload", "-r", action="store_true", help="Enable auto-reload")
    serve_parser.add_argument("--debug", "-d", action="store_true", help="Enable debug mode")
    serve_parser.set_defaults(func=cmd_start_server)
    
    # info
    info_parser = subparsers.add_parser("info", help="Show system information")
    info_parser.set_defaults(func=cmd_info)
    
    args = parser.parse_args()
    
    if args.command is None:
        print_banner()
        parser.print_help()
        sys.exit(0)
    
    args.func(args)


if __name__ == "__main__":
    main()
