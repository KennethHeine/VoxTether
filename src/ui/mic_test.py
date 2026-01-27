"""Microphone test window for VoxTether.

Provides visual feedback for microphone testing with:
- Real-time volume visualization
- Device selection dropdown
- Client-side only (no backend required)
"""

import logging
import threading
import tkinter as tk
from collections.abc import Callable
from tkinter import ttk
from typing import Any

import numpy as np

logger = logging.getLogger(__name__)


class MicTestWindow:
    """Window for testing microphone with visual feedback.
    
    Features:
    - Real-time volume level visualization
    - Microphone device selection
    - Visual waveform display
    - No backend/transcription required
    """

    def __init__(
        self,
        on_close: Callable[[], None] | None = None,
        on_device_change: Callable[[int], None] | None = None,
    ):
        """Initialize the mic test window.
        
        Args:
            on_close: Callback when window is closed.
            on_device_change: Callback when device is changed.
        """
        self._on_close = on_close
        self._on_device_change = on_device_change

        self._root: tk.Tk | None = None
        self._window: tk.Toplevel | None = None
        self._canvas: tk.Canvas | None = None
        self._device_combo: ttk.Combobox | None = None
        self._volume_bar: tk.Canvas | None = None

        self._stream = None
        self._is_running = False
        self._audio_buffer: list[float] = []
        self._peak_level = 0.0
        self._current_device: int | None = None
        self._devices: list[dict[str, Any]] = []

        # Import sounddevice here to avoid import errors on systems without audio
        try:
            import sounddevice as sd
            self._sd = sd
        except (ImportError, OSError) as e:
            logger.error(f"Audio system not available: {e}")
            self._sd = None

    def show(self, parent: tk.Tk | None = None) -> None:
        """Show the mic test window.
        
        Args:
            parent: Optional parent window.
        """
        if self._window and self._window.winfo_exists():
            self._window.lift()
            self._window.focus_force()
            return

        # Create root window if needed
        if parent:
            self._root = parent
            self._window = tk.Toplevel(parent)
        else:
            self._root = tk.Tk()
            self._window = self._root

        self._setup_window()
        self._create_widgets()
        self._load_devices()

        if parent:
            self._window.transient(parent)
            self._window.grab_set()

        # Start audio monitoring
        self._start_monitoring()

        # Start animation loop
        self._animate()

        if not parent:
            self._window.mainloop()

    def _setup_window(self) -> None:
        """Set up the window properties."""
        self._window.title("Microphone Test")
        self._window.geometry("500x400")
        self._window.resizable(True, True)
        self._window.minsize(400, 300)

        # Center on screen
        self._window.update_idletasks()
        x = (self._window.winfo_screenwidth() - 500) // 2
        y = (self._window.winfo_screenheight() - 400) // 2
        self._window.geometry(f"+{x}+{y}")

        # Handle close
        self._window.protocol("WM_DELETE_WINDOW", self._close)

    def _create_widgets(self) -> None:
        """Create the window widgets."""
        main_frame = ttk.Frame(self._window, padding=15)
        main_frame.pack(fill=tk.BOTH, expand=True)

        # Title
        title_label = ttk.Label(
            main_frame,
            text="Microphone Test",
            font=("Segoe UI", 14, "bold"),
        )
        title_label.pack(pady=(0, 10))

        # Description
        desc_label = ttk.Label(
            main_frame,
            text="Speak into your microphone to see visual feedback.\n"
                 "The level meter shows your voice volume in real-time.",
            justify=tk.CENTER,
            foreground="gray",
        )
        desc_label.pack(pady=(0, 15))

        # Device selection frame
        device_frame = ttk.LabelFrame(main_frame, text="Microphone Device", padding=10)
        device_frame.pack(fill=tk.X, pady=(0, 15))

        self._device_var = tk.StringVar()
        self._device_combo = ttk.Combobox(
            device_frame,
            textvariable=self._device_var,
            state="readonly",
            width=50,
        )
        self._device_combo.pack(fill=tk.X)
        self._device_combo.bind("<<ComboboxSelected>>", self._on_device_selected)

        # Volume meter frame
        volume_frame = ttk.LabelFrame(main_frame, text="Volume Level", padding=10)
        volume_frame.pack(fill=tk.X, pady=(0, 15))

        # Volume bar
        self._volume_bar = tk.Canvas(
            volume_frame,
            width=460,
            height=30,
            bg="#2d2d2d",
            highlightthickness=1,
            highlightbackground="#555",
        )
        self._volume_bar.pack(fill=tk.X, pady=5)

        # Peak indicator label
        self._peak_label = ttk.Label(volume_frame, text="Peak: 0%")
        self._peak_label.pack(anchor=tk.W)

        # Waveform display frame
        waveform_frame = ttk.LabelFrame(main_frame, text="Audio Waveform", padding=10)
        waveform_frame.pack(fill=tk.BOTH, expand=True, pady=(0, 15))

        self._canvas = tk.Canvas(
            waveform_frame,
            bg="#1a1a2e",
            highlightthickness=1,
            highlightbackground="#555",
        )
        self._canvas.pack(fill=tk.BOTH, expand=True)

        # Status label
        self._status_label = ttk.Label(
            main_frame,
            text="Status: Initializing...",
            foreground="gray",
        )
        self._status_label.pack(pady=(0, 10))

        # Close button
        close_btn = ttk.Button(
            main_frame,
            text="Close",
            command=self._close,
        )
        close_btn.pack()

    def _load_devices(self) -> None:
        """Load available audio input devices."""
        if not self._sd:
            self._device_combo.set("Audio system not available")
            return

        self._devices = []
        try:
            for i, device in enumerate(self._sd.query_devices()):
                if device["max_input_channels"] > 0:
                    self._devices.append({
                        "index": i,
                        "name": device["name"],
                        "channels": device["max_input_channels"],
                    })
        except Exception as e:
            logger.error(f"Failed to query audio devices: {e}")
            self._status_label.config(text=f"Error: {e}")
            return

        if not self._devices:
            self._device_combo.set("No audio input devices found")
            self._status_label.config(text="Status: No microphones detected")
            return

        # Populate combo box
        device_names = [f"{d['index']}: {d['name']}" for d in self._devices]
        self._device_combo["values"] = device_names

        # Select first device
        if device_names:
            self._device_combo.current(0)
            self._current_device = self._devices[0]["index"]

    def _on_device_selected(self, event=None) -> None:
        """Handle device selection change."""
        selection = self._device_combo.current()
        if 0 <= selection < len(self._devices):
            new_device = self._devices[selection]["index"]
            if new_device != self._current_device:
                self._current_device = new_device
                self._restart_monitoring()

                if self._on_device_change:
                    self._on_device_change(new_device)

    def _start_monitoring(self) -> None:
        """Start audio monitoring."""
        if not self._sd:
            return

        self._is_running = True
        self._audio_buffer = [0.0] * 200  # Buffer for waveform display

        try:
            def audio_callback(indata: np.ndarray, frames: int, time_info, status) -> None:
                if status:
                    logger.debug(f"Audio status: {status}")

                # Calculate RMS level
                rms = np.sqrt(np.mean(indata ** 2))
                level = min(1.0, rms * 10)  # Scale for visibility

                # Update peak
                if level > self._peak_level:
                    self._peak_level = level
                else:
                    # Decay peak slowly
                    self._peak_level = max(level, self._peak_level * 0.95)

                # Add to buffer (taking average of the frame)
                self._audio_buffer.append(float(np.mean(np.abs(indata))))
                if len(self._audio_buffer) > 200:
                    self._audio_buffer.pop(0)

            self._stream = self._sd.InputStream(
                device=self._current_device,
                channels=1,
                samplerate=16000,
                callback=audio_callback,
                dtype=np.float32,
                blocksize=512,
            )
            self._stream.start()

            self._status_label.config(
                text=f"Status: Listening on device {self._current_device}",
                foreground="green",
            )

        except Exception as e:
            logger.error(f"Failed to start audio monitoring: {e}")
            self._status_label.config(
                text=f"Error: {e}",
                foreground="red",
            )

    def _stop_monitoring(self) -> None:
        """Stop audio monitoring."""
        self._is_running = False
        if self._stream:
            try:
                self._stream.stop()
                self._stream.close()
            except Exception as e:
                logger.debug(f"Error stopping stream: {e}")
            self._stream = None

    def _restart_monitoring(self) -> None:
        """Restart audio monitoring with new device."""
        self._stop_monitoring()
        self._start_monitoring()

    def _animate(self) -> None:
        """Animation loop for visualizations."""
        if not self._is_running or not self._window:
            return

        try:
            self._draw_volume_bar()
            self._draw_waveform()

            # Update peak label
            peak_pct = int(self._peak_level * 100)
            self._peak_label.config(text=f"Peak: {peak_pct}%")

            # Schedule next frame
            self._window.after(33, self._animate)  # ~30 FPS

        except tk.TclError:
            pass  # Window closed

    def _draw_volume_bar(self) -> None:
        """Draw the volume level bar."""
        if not self._volume_bar:
            return

        self._volume_bar.delete("all")

        width = self._volume_bar.winfo_width()
        height = self._volume_bar.winfo_height()

        if width <= 1:
            return

        # Calculate current level from recent buffer
        if self._audio_buffer:
            # Average of last 10 samples, scaled by 10 for visibility
            avg = sum(self._audio_buffer[-10:]) / len(self._audio_buffer[-10:])
            level = min(1.0, avg * 10)
        else:
            level = 0.0

        bar_width = int(width * level)

        # Draw background gradient segments
        segment_width = width // 20
        for i in range(20):
            x = i * segment_width
            # Color gradient: green -> yellow -> red
            if i < 10:
                r = int(50 + i * 15)
                g = 180
                b = 50
            elif i < 15:
                r = 200
                g = int(180 - (i - 10) * 30)
                b = 50
            else:
                r = 220
                g = int(60 - (i - 15) * 10)
                b = 50

            # Only show lit segments up to current level
            if x < bar_width:
                color = f"#{r:02x}{g:02x}{b:02x}"
            else:
                color = "#3d3d3d"

            self._volume_bar.create_rectangle(
                x + 2, 2, x + segment_width - 2, height - 2,
                fill=color,
                outline="",
            )

        # Draw peak indicator
        peak_x = int(width * self._peak_level)
        if peak_x > 0:
            self._volume_bar.create_line(
                peak_x, 0, peak_x, height,
                fill="#ffffff",
                width=2,
            )

    def _draw_waveform(self) -> None:
        """Draw the audio waveform."""
        if not self._canvas:
            return

        self._canvas.delete("all")

        width = self._canvas.winfo_width()
        height = self._canvas.winfo_height()

        if width <= 1 or not self._audio_buffer:
            return

        center_y = height // 2

        # Draw center line
        self._canvas.create_line(
            0, center_y, width, center_y,
            fill="#333355",
            width=1,
        )

        # Draw waveform
        points = []
        buffer_len = len(self._audio_buffer)

        for i, amplitude in enumerate(self._audio_buffer):
            x = int(i / buffer_len * width)
            # Scale amplitude for visibility
            scaled = min(1.0, amplitude * 10)
            y = center_y - int(scaled * (height // 2 - 10))
            points.append((x, y))

        # Draw as a smooth line
        if len(points) >= 2:
            flat_points = [coord for point in points for coord in point]
            self._canvas.create_line(
                flat_points,
                fill="#4682B4",
                width=2,
                smooth=True,
            )

            # Draw mirrored waveform
            mirror_points = [(p[0], center_y + (center_y - p[1])) for p in points]
            flat_mirror = [coord for point in mirror_points for coord in point]
            self._canvas.create_line(
                flat_mirror,
                fill="#4682B4",
                width=2,
                smooth=True,
            )

    def _close(self) -> None:
        """Close the window."""
        self._stop_monitoring()
        self._is_running = False

        if self._on_close:
            self._on_close()

        if self._window:
            self._window.destroy()
            self._window = None


def show_mic_test(parent: tk.Tk | None = None) -> MicTestWindow:
    """Show the microphone test window.
    
    Args:
        parent: Optional parent window.
        
    Returns:
        MicTestWindow instance.
    """
    window = MicTestWindow()

    # Run in thread if no parent
    if parent:
        window.show(parent)
    else:
        thread = threading.Thread(target=lambda: window.show(), daemon=True)
        thread.start()

    return window
