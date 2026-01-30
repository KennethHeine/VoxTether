#!/usr/bin/env python3
"""VoxTether Backend CLI - Command line tool for managing the backend server."""

import asyncio
import sys
from pathlib import Path

import click

# Add current directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

from config import settings
from constants import AVAILABLE_MODELS, APP_VERSION
from services.model_manager import ModelManager


def print_banner():
    """Print the VoxTether banner."""
    click.echo("""
╔═══════════════════════════════════════════════════════════════╗
║                    VoxTether Backend CLI                      ║
║           Speech-to-Text Transcription Server                 ║
╚═══════════════════════════════════════════════════════════════╝
""")


@click.group()
@click.version_option(version=APP_VERSION, prog_name="VoxTether Backend")
def cli():
    """VoxTether Backend CLI - Manage the speech-to-text server."""
    pass


@cli.command()
def list():
    """List all available models and their download status."""
    manager = ModelManager(settings.models_path)
    models = manager.list_models()
    
    click.echo(f"\nModels directory: {settings.models_path}\n")
    click.echo("Available Models:")
    click.echo("-" * 80)
    click.echo(f"{'Name':<20} {'Size':<12} {'Downloaded':<12} {'Description'}")
    click.echo("-" * 80)
    
    for model in models:
        status = "✓ Yes" if model["downloaded"] else "✗ No"
        size = f"{model['size_mb']} MB"
        click.echo(f"{model['name']:<20} {size:<12} {status:<12} {model['description']}")
    
    click.echo("-" * 80)
    click.echo(f"\nTotal models: {len(models)}")
    downloaded = sum(1 for m in models if m["downloaded"])
    click.echo(f"Downloaded: {downloaded}")


@cli.command()
@click.argument("model_name")
@click.option("--force", "-f", is_flag=True, help="Re-download even if already exists")
def download(model_name: str, force: bool):
    """Download a model.
    
    MODEL_NAME: Name of the model to download (e.g., 'small', 'base')
    """
    if model_name not in AVAILABLE_MODELS:
        click.echo(f"Error: Unknown model '{model_name}'", err=True)
        click.echo(f"Available models: {', '.join(AVAILABLE_MODELS.keys())}")
        sys.exit(1)
    
    manager = ModelManager(settings.models_path)
    
    if manager.is_model_downloaded(model_name):
        if not force:
            click.echo(f"Model '{model_name}' is already downloaded.")
            click.echo("Use --force to re-download.")
            return
        else:
            click.echo(f"Re-downloading model '{model_name}'...")
            manager.delete_model(model_name)
    
    model_info = AVAILABLE_MODELS[model_name]
    click.echo(f"\nDownloading model: {model_name}")
    click.echo(f"  Display name: {model_info.get('display_name', model_name)}")
    click.echo(f"  Size: ~{model_info.get('size_mb', 'unknown')} MB")
    click.echo(f"  Repository: {model_info.get('repo_id', 'N/A')}")
    click.echo(f"  Target: {settings.models_path}/{model_name}")
    click.echo()
    
    async def download_model():
        last_progress = -1
        async for progress in manager.download_model_async(model_name):
            if progress.status == "downloading":
                pct = int(progress.progress)
                if pct != last_progress:
                    bar_width = 40
                    filled = int(bar_width * pct / 100)
                    bar = "█" * filled + "░" * (bar_width - filled)
                    click.echo(
                        f"\r[{bar}] {pct:3}% ({progress.downloaded_mb:.1f}/{progress.total_mb:.1f} MB)",
                        nl=False,
                    )
                    last_progress = pct
            elif progress.status == "complete":
                click.echo(f"\r[{'█' * 40}] 100%")
                click.echo(f"\n✓ Model '{model_name}' downloaded successfully!")
            elif progress.status == "error":
                click.echo(f"\n✗ Download failed: {progress.error}", err=True)
                sys.exit(1)
    
    asyncio.run(download_model())


@cli.command()
@click.argument("model_name")
@click.option("--yes", "-y", is_flag=True, help="Skip confirmation")
def delete(model_name: str, yes: bool):
    """Delete a downloaded model.
    
    MODEL_NAME: Name of the model to delete
    """
    manager = ModelManager(settings.models_path)
    
    if not manager.is_model_downloaded(model_name):
        click.echo(f"Model '{model_name}' is not downloaded.", err=True)
        sys.exit(1)
    
    if not yes:
        if not click.confirm(f"Are you sure you want to delete model '{model_name}'?"):
            click.echo("Cancelled.")
            return
    
    if manager.delete_model(model_name):
        click.echo(f"✓ Model '{model_name}' deleted successfully.")
    else:
        click.echo(f"✗ Failed to delete model '{model_name}'.", err=True)
        sys.exit(1)


@cli.command()
def config():
    """Show current configuration."""
    click.echo("\nCurrent Configuration:")
    click.echo("-" * 50)
    click.echo(f"Host:           {settings.host}")
    click.echo(f"Port:           {settings.port}")
    click.echo(f"Debug:          {settings.debug}")
    click.echo(f"Models path:    {settings.models_path}")
    click.echo(f"Default model:  {settings.default_model}")
    click.echo(f"Preload model:  {settings.preload_model}")
    click.echo(f"Device:         {settings.device}")
    click.echo(f"Compute type:   {settings.compute_type}")
    click.echo(f"Language:       {settings.default_language}")
    click.echo("-" * 50)
    click.echo("\nEnvironment variables (prefix: VOXTETHER_):")
    click.echo("  VOXTETHER_HOST, VOXTETHER_PORT, VOXTETHER_DEBUG")
    click.echo("  VOXTETHER_MODELS_PATH, VOXTETHER_DEFAULT_MODEL")
    click.echo("  VOXTETHER_PRELOAD_MODEL, VOXTETHER_DEVICE")
    click.echo("  VOXTETHER_COMPUTE_TYPE, VOXTETHER_DEFAULT_LANGUAGE")


@cli.command()
@click.option("--host", "-H", help="Host to bind to")
@click.option("--port", "-p", type=int, help="Port to bind to")
@click.option("--reload", "-r", is_flag=True, help="Enable auto-reload")
@click.option("--debug", "-d", is_flag=True, help="Enable debug mode")
def serve(host: str, port: int, reload: bool, debug: bool):
    """Start the backend server."""
    import uvicorn
    
    host = host or settings.host
    port = port or settings.port
    
    click.echo("\nStarting VoxTether backend server...")
    click.echo(f"  URL: http://{host}:{port}")
    click.echo(f"  API docs: http://{host}:{port}/docs")
    click.echo(f"  Models path: {settings.models_path}")
    click.echo()
    
    uvicorn.run(
        "main:app",
        host=host,
        port=port,
        reload=reload,
        log_level="debug" if debug else "info",
    )


@cli.command()
def info():
    """Show system and GPU information."""
    click.echo("\nSystem Information:")
    click.echo("-" * 50)
    
    # Check for CUDA
    try:
        import torch
        cuda_available = torch.cuda.is_available()
        click.echo(f"PyTorch version: {torch.__version__}")
        click.echo(f"CUDA available:  {cuda_available}")
        if cuda_available:
            click.echo(f"CUDA version:    {torch.version.cuda}")
            click.echo(f"GPU count:       {torch.cuda.device_count()}")
            for i in range(torch.cuda.device_count()):
                click.echo(f"  GPU {i}: {torch.cuda.get_device_name(i)}")
    except ImportError:
        click.echo("PyTorch: Not installed")
    
    # Check for faster-whisper
    import importlib.util
    if importlib.util.find_spec("faster_whisper"):
        click.echo("\nfaster-whisper: Available")
    else:
        click.echo("\nfaster-whisper: Not installed")
    
    click.echo("-" * 50)


def main():
    """Main entry point for CLI."""
    # Print banner if no arguments provided
    if len(sys.argv) == 1:
        print_banner()
    
    cli()


if __name__ == "__main__":
    main()
