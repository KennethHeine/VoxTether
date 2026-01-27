#!/usr/bin/env python3
"""Build script for VoxTether using PyInstaller."""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path


def run_command(cmd: list[str], cwd: Path | None = None) -> int:
    """Run a command and return the exit code."""
    print(f"Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd)
    return result.returncode


def build(
    output_dir: Path,
    one_file: bool = True,
    debug: bool = False,
    icon_path: Path | None = None,
) -> int:
    """Build the VoxTether executable.
    
    Args:
        output_dir: Directory for build output.
        one_file: Whether to create a single .exe file.
        debug: Whether to include debug console.
        icon_path: Path to the icon file.
    
    Returns:
        Exit code (0 for success).
    """
    script_dir = Path(__file__).parent
    assets_dir = script_dir / "assets"
    launcher_script = script_dir / "launcher.py"
    
    # Ensure output directory exists
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Build command
    cmd = [
        sys.executable, "-m", "PyInstaller",
        "--name", "VoxTether",
        "--noconfirm",
        "--clean",
    ]
    
    # One file or directory
    if one_file:
        cmd.append("--onefile")
    else:
        cmd.append("--onedir")
    
    # Console or windowed
    if debug:
        cmd.append("--console")
    else:
        cmd.append("--windowed")
    
    # Icon
    if icon_path and icon_path.exists():
        cmd.extend(["--icon", str(icon_path)])
    elif (assets_dir / "icon.ico").exists():
        cmd.extend(["--icon", str(assets_dir / "icon.ico")])
    
    # Add data files
    if assets_dir.exists():
        cmd.extend(["--add-data", f"{assets_dir}{os.pathsep}assets"])
    
    # Hidden imports for PyInstaller to find
    # NOTE: When adding new modules to src/, remember to add them here
    hidden_imports = [
        # Application modules
        "src",
        "src.main",
        "src.settings",
        "src.model_manager",
        "src.recorder",
        "src.transcriber",
        "src.injector",
        "src.hotkey",
        "src.tray",
        "src.ui",
        "src.ui.settings_window",
        "src.ui.model_setup",
        # Third-party dependencies
        "faster_whisper",
        "ctranslate2",
        "sounddevice",
        "soundfile",
        "pystray",
        "keyboard",
        "pyperclip",
        "PIL",
        "PIL.Image",
        "PIL.ImageDraw",
        "numpy",
        "huggingface_hub",
        "tqdm",
        "tkinter",
        "tkinter.ttk",
        "tkinter.messagebox",
    ]
    
    for imp in hidden_imports:
        cmd.extend(["--hidden-import", imp])
    
    # Collect packages that have data files
    collect_packages = [
        "faster_whisper",
        "ctranslate2",
    ]
    
    for pkg in collect_packages:
        cmd.extend(["--collect-all", pkg])
    
    # Output directory
    cmd.extend(["--distpath", str(output_dir)])
    cmd.extend(["--workpath", str(output_dir / "build")])
    cmd.extend(["--specpath", str(output_dir)])
    
    # Entry point - use launcher script to avoid relative import issues
    cmd.append(str(launcher_script))
    
    # Run PyInstaller
    print("\n" + "=" * 60)
    print("Building VoxTether with PyInstaller")
    print("=" * 60 + "\n")
    
    result = run_command(cmd, cwd=script_dir)
    
    if result != 0:
        print("\n[ERROR] Build failed!")
        return result
    
    # Clean up build artifacts
    build_dir = output_dir / "build"
    
    if build_dir.exists():
        shutil.rmtree(build_dir)
    
    print("\n" + "=" * 60)
    print("[SUCCESS] Build complete!")
    print("=" * 60)
    
    if one_file:
        exe_path = output_dir / "VoxTether.exe"
        if exe_path.exists():
            size_mb = exe_path.stat().st_size / (1024 * 1024)
            print(f"\nExecutable: {exe_path}")
            print(f"Size: {size_mb:.1f} MB")
    else:
        dist_dir = output_dir / "VoxTether"
        if dist_dir.exists():
            print(f"\nOutput directory: {dist_dir}")
    
    return 0


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Build VoxTether executable",
    )
    parser.add_argument(
        "--output",
        "-o",
        type=Path,
        default=Path("dist"),
        help="Output directory (default: dist)",
    )
    parser.add_argument(
        "--onedir",
        action="store_true",
        help="Create a directory instead of a single .exe",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Include debug console",
    )
    parser.add_argument(
        "--icon",
        type=Path,
        help="Path to icon file",
    )
    
    args = parser.parse_args()
    
    return build(
        output_dir=args.output.resolve(),
        one_file=not args.onedir,
        debug=args.debug,
        icon_path=args.icon,
    )


if __name__ == "__main__":
    sys.exit(main())
