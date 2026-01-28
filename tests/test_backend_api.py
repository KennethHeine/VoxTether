"""Test script for the VoxTether backend API.

This script tests the backend API by:
1. Checking backend health
2. Listing available models
3. Testing transcription with each downloaded model

Usage:
    python -m tests.test_backend_api [--base-url URL] [--audio-file PATH]
    
Examples:
    python -m tests.test_backend_api
    python -m tests.test_backend_api --base-url http://localhost:5678
    python -m tests.test_backend_api --audio-file tests/test-recoarding.wav
"""

import argparse
import json
import sys
import time
from pathlib import Path

import requests


# Default configuration
DEFAULT_BASE_URL = "http://127.0.0.1:5678"
DEFAULT_AUDIO_FILE = Path(__file__).parent / "test-recoarding.wav"


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
    """List available models and return downloaded ones."""
    print("\n" + "=" * 60)
    print("Listing available models...")
    print("=" * 60)
    
    try:
        response = requests.get(f"{base_url}/api/models", timeout=10)
        response.raise_for_status()
        data = response.json()
        
        print(f"\nCurrent loaded model: {data.get('current_model', 'None')}\n")
        print(f"{'Model':<15} {'Size (MB)':<12} {'Downloaded':<12} {'Description'}")
        print("-" * 70)
        
        downloaded_models = []
        for model in data["models"]:
            status = "✓ Yes" if model["downloaded"] else "✗ No"
            print(f"{model['name']:<15} {model['size_mb']:<12} {status:<12} {model.get('description', '')[:30]}")
            if model["downloaded"]:
                downloaded_models.append(model)
        
        print(f"\nTotal models: {len(data['models'])}, Downloaded: {len(downloaded_models)}")
        return downloaded_models
        
    except Exception as e:
        print(f"✗ Failed to list models: {e}")
        return []


def load_model(base_url: str, model_name: str) -> bool:
    """Load a model."""
    print(f"\n  Loading model '{model_name}'...", end=" ", flush=True)
    
    try:
        start_time = time.time()
        response = requests.post(f"{base_url}/api/models/{model_name}/load", timeout=120)
        response.raise_for_status()
        elapsed = time.time() - start_time
        print(f"✓ Loaded in {elapsed:.1f}s")
        return True
    except Exception as e:
        print(f"✗ Failed: {e}")
        return False


def transcribe_audio(base_url: str, audio_file: Path, language: str = "auto") -> dict:
    """Transcribe an audio file."""
    print(f"\n  Transcribing audio file...", end=" ", flush=True)
    
    try:
        start_time = time.time()
        with open(audio_file, "rb") as f:
            files = {"file": (audio_file.name, f, "audio/wav")}
            data = {"language": language}
            response = requests.post(
                f"{base_url}/api/transcribe",
                files=files,
                data=data,
                timeout=120
            )
        response.raise_for_status()
        elapsed = time.time() - start_time
        result = response.json()
        
        if result.get("success"):
            print(f"✓ Done in {elapsed:.1f}s")
            return result
        else:
            print(f"✗ Failed: {result.get('error', 'Unknown error')}")
            return result
            
    except Exception as e:
        print(f"✗ Failed: {e}")
        return {"success": False, "error": str(e)}


def test_model(base_url: str, model_name: str, audio_file: Path) -> dict:
    """Test a single model by loading it and transcribing audio."""
    print(f"\n{'─' * 60}")
    print(f"Testing model: {model_name}")
    print(f"{'─' * 60}")
    
    result = {
        "model": model_name,
        "load_success": False,
        "transcribe_success": False,
        "text": None,
        "language": None,
        "duration": None,
        "error": None,
    }
    
    # Load the model
    if not load_model(base_url, model_name):
        result["error"] = "Failed to load model"
        return result
    result["load_success"] = True
    
    # Give the model a moment to initialize
    time.sleep(0.5)
    
    # Transcribe
    transcription = transcribe_audio(base_url, audio_file)
    
    if transcription.get("success"):
        result["transcribe_success"] = True
        result["text"] = transcription.get("text")
        result["language"] = transcription.get("language")
        result["duration"] = transcription.get("duration")
        
        print(f"\n  Result:")
        print(f"    Language: {result['language']}")
        print(f"    Duration: {result['duration']:.2f}s")
        print(f"    Text: \"{result['text']}\"")
    else:
        result["error"] = transcription.get("error", "Transcription failed")
    
    return result


def print_summary(results: list) -> None:
    """Print a summary of all test results."""
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    
    print(f"\n{'Model':<15} {'Load':<10} {'Transcribe':<12} {'Language':<10} {'Text'}")
    print("-" * 80)
    
    success_count = 0
    for r in results:
        load_status = "✓" if r["load_success"] else "✗"
        trans_status = "✓" if r["transcribe_success"] else "✗"
        language = r.get("language") or "-"
        text = (r.get("text") or r.get("error") or "-")[:40]
        
        print(f"{r['model']:<15} {load_status:<10} {trans_status:<12} {language:<10} {text}")
        
        if r["transcribe_success"]:
            success_count += 1
    
    print("-" * 80)
    print(f"\nTotal: {len(results)} models tested, {success_count} successful")


def main():
    """Run the backend API tests."""
    parser = argparse.ArgumentParser(description="Test VoxTether backend API")
    parser.add_argument(
        "--base-url",
        default=DEFAULT_BASE_URL,
        help=f"Backend API base URL (default: {DEFAULT_BASE_URL})"
    )
    parser.add_argument(
        "--audio-file",
        type=Path,
        default=DEFAULT_AUDIO_FILE,
        help=f"Path to test audio file (default: {DEFAULT_AUDIO_FILE})"
    )
    parser.add_argument(
        "--model",
        help="Test only a specific model (default: test all downloaded models)"
    )
    args = parser.parse_args()
    
    print("=" * 60)
    print("VoxTether Backend API Test")
    print("=" * 60)
    print(f"Backend URL: {args.base_url}")
    print(f"Audio file: {args.audio_file}")
    
    # Validate audio file
    if not args.audio_file.exists():
        print(f"\n✗ Audio file not found: {args.audio_file}")
        sys.exit(1)
    
    print(f"Audio file size: {args.audio_file.stat().st_size:,} bytes")
    
    # Check health
    if not check_health(args.base_url):
        sys.exit(1)
    
    # List models
    downloaded_models = list_models(args.base_url)
    
    if not downloaded_models:
        print("\n✗ No downloaded models found. Please download a model first.")
        sys.exit(1)
    
    # Filter to specific model if requested
    if args.model:
        downloaded_models = [m for m in downloaded_models if m["name"] == args.model]
        if not downloaded_models:
            print(f"\n✗ Model '{args.model}' not found or not downloaded.")
            sys.exit(1)
    
    # Test each model
    print("\n" + "=" * 60)
    print(f"Testing {len(downloaded_models)} model(s)...")
    print("=" * 60)
    
    results = []
    for model in downloaded_models:
        result = test_model(args.base_url, model["name"], args.audio_file)
        results.append(result)
    
    # Print summary
    print_summary(results)
    
    # Exit with error if any test failed
    if not all(r["transcribe_success"] for r in results):
        sys.exit(1)


if __name__ == "__main__":
    main()
