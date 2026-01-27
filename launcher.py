#!/usr/bin/env python3
"""Launcher script for VoxTether.

This script serves as the entry point for PyInstaller builds.
It properly imports and runs the main function from the src package,
avoiding relative import issues when running as a bundled executable.
"""

import sys

# Import and run main from the src package
from src.main import main

if __name__ == "__main__":
    sys.exit(main())
