"""Download all models via the VoxTether backend API.

This script downloads all available Whisper models using the backend API.

Usage:
    python -m tests.download_all_models [--base-url URL]
    
Examples:
    python -m tests.download_all_models
    python -m tests.download_all_models --base-url http://localhost:5678
"""

import argparse
import json
import sys
import time

import requests


# Default configuration
DEFAULT_BASE_URL = "http://127.0.0.1:5678"


def check_health(base_url: str) -> bool:
    """Check if the backend is healthy."""
    print("\n" + "=" * 60)
    print("Checking backend health...")
    print("=" * 60)
    
    try:
        response = requests.get(f"{base_url}/api/health", timeout=5)
        response.raise_for_status()
        data = response.json()
        print(f"✓ Backend is healthy: {json.dumps(data, indent=2)}")
        return True
    except requests.exceptions.ConnectionError:
        print(f"✗ Cannot connect to backend at {base_url}")
        print("  Make sure the backend is running: python -m src.backend.main")
        return False
    except Exception as e:
        print(f"✗ Health check failed: {e}")
        return False


def list_models(base_url: str) -> list:
    """List all available models."""
    print("\n" + "=" * 60)
    print("Listing available models...")
    print("=" * 60)
    
    try:
        response = requests.get(f"{base_url}/api/models", timeout=10)
        response.raise_for_status()
        data = response.json()
        
        print(f"\n{'Model':<15} {'Size (MB)':<12} {'Downloaded':<12} {'Description'}")
        print("-" * 70)
        
        for model in data["models"]:
            status = "✓ Yes" if model["downloaded"] else "✗ No"
            print(f"{model['name']:<15} {model['size_mb']:<12} {status:<12} {model.get('description', '')[:30]}")
        
        downloaded = sum(1 for m in data["models"] if m["downloaded"])
        print(f"\nTotal: {len(data['models'])} models, {downloaded} downloaded")
        
        return data["models"]
        
    except Exception as e:
        print(f"✗ Failed to list models: {e}")
        return []


def download_model(base_url: str, model_name: str, size_mb: int) -> bool:
    """Download a model with progress updates."""
    print(f"\n{'─' * 60}")
    print(f"Downloading model: {model_name} ({size_mb} MB)")
    print(f"{'─' * 60}")
    
    try:
        start_time = time.time()
        
        # Make SSE request for progress updates
        response = requests.post(
            f"{base_url}/api/models/{model_name}/download",
            stream=True,
            timeout=3600  # 1 hour timeout for large models
        )
        response.raise_for_status()
        
        last_progress = -1
        for line in response.iter_lines():
            if line:
                line = line.decode('utf-8')
                if line.startswith('data: '):
                    try:
                        data = json.loads(line[6:])
                        status = data.get('status', '')
                        
                        if status == 'downloading':
                            progress = data.get('progress', 0)
                            # Only print every 10%
                            progress_10 = int(progress / 10) * 10
                            if progress_10 > last_progress:
                                last_progress = progress_10
                                downloaded_mb = data.get('downloaded_bytes', 0) / (1024 * 1024)
                                total_mb = data.get('total_bytes', 0) / (1024 * 1024)
                                print(f"  Progress: {progress:.0f}% ({downloaded_mb:.1f}/{total_mb:.1f} MB)")
                        
                        elif status == 'completed':
                            elapsed = time.time() - start_time
                            print(f"✓ Downloaded successfully in {elapsed:.1f}s")
                            return True
                        
                        elif status == 'error':
                            error = data.get('error', 'Unknown error')
                            print(f"✗ Download failed: {error}")
                            return False
                        
                        elif status == 'already_downloaded':
                            print(f"✓ Model already downloaded")
                            return True
                            
                    except json.JSONDecodeError:
                        pass
        
        # If we get here, something went wrong
        elapsed = time.time() - start_time
        print(f"? Download ended after {elapsed:.1f}s (status unclear)")
        return True  # Assume success if no error
        
    except Exception as e:
        print(f"✗ Download failed: {e}")
        return False


def main():
    """Download all models."""
    parser = argparse.ArgumentParser(description="Download all VoxTether models")
    parser.add_argument(
        "--base-url",
        default=DEFAULT_BASE_URL,
        help=f"Backend API base URL (default: {DEFAULT_BASE_URL})"
    )
    parser.add_argument(
        "--skip-downloaded",
        action="store_true",
        default=True,
        help="Skip already downloaded models (default: True)"
    )
    parser.add_argument(
        "--model",
        help="Download only a specific model"
    )
    args = parser.parse_args()
    
    print("=" * 60)
    print("VoxTether Model Downloader")
    print("=" * 60)
    print(f"Backend URL: {args.base_url}")
    
    # Check health
    if not check_health(args.base_url):
        sys.exit(1)
    
    # List models
    models = list_models(args.base_url)
    
    if not models:
        print("\n✗ No models found.")
        sys.exit(1)
    
    # Filter to specific model if requested
    if args.model:
        models = [m for m in models if m["name"] == args.model]
        if not models:
            print(f"\n✗ Model '{args.model}' not found.")
            sys.exit(1)
    
    # Filter out already downloaded if requested
    if args.skip_downloaded:
        models_to_download = [m for m in models if not m["downloaded"]]
    else:
        models_to_download = models
    
    if not models_to_download:
        print("\n✓ All models are already downloaded!")
        sys.exit(0)
    
    # Calculate total size
    total_size = sum(m["size_mb"] for m in models_to_download)
    print(f"\n{'=' * 60}")
    print(f"Models to download: {len(models_to_download)}")
    print(f"Total size: {total_size:,} MB ({total_size / 1024:.1f} GB)")
    print(f"{'=' * 60}")
    
    # Confirm
    print("\nModels to download:")
    for m in models_to_download:
        print(f"  - {m['name']} ({m['size_mb']} MB)")
    
    # Download each model
    results = []
    for model in models_to_download:
        success = download_model(args.base_url, model["name"], model["size_mb"])
        results.append({"model": model["name"], "success": success})
    
    # Print summary
    print("\n" + "=" * 60)
    print("DOWNLOAD SUMMARY")
    print("=" * 60)
    
    success_count = sum(1 for r in results if r["success"])
    for r in results:
        status = "✓" if r["success"] else "✗"
        print(f"  {status} {r['model']}")
    
    print(f"\nTotal: {len(results)} models, {success_count} successful")
    
    if success_count < len(results):
        sys.exit(1)


if __name__ == "__main__":
    main()
