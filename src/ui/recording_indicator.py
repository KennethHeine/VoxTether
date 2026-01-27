"""Recording and transcribing overlay indicator for VoxTether.

Provides a visual feedback bar at the top of the screen showing:
- Recording state (red pulsing bar)
- Transcribing state (blue animated bar)
"""

import logging
import threading
import time
import tkinter as tk

logger = logging.getLogger(__name__)


class RecordingIndicator:
    """A visual overlay that shows recording/transcribing state.
    
    Shows a thin bar at the top of the screen with different states:
    - Recording: Red bar with pulsing animation
    - Transcribing: Blue bar with loading animation
    - Hidden when idle
    """

    # Bar dimensions
    BAR_HEIGHT = 6

    # Colors
    COLOR_RECORDING = "#DC3232"       # Red for recording
    COLOR_RECORDING_LIGHT = "#FF6666"  # Light red for pulse
    COLOR_TRANSCRIBING = "#4682B4"    # Steel blue for transcribing
    COLOR_TRANSCRIBING_LIGHT = "#6FA8DC"  # Light blue for animation

    def __init__(self):
        """Initialize the recording indicator."""
        self._root: tk.Tk | None = None
        self._canvas: tk.Canvas | None = None
        self._thread: threading.Thread | None = None
        self._is_running = False
        self._state = "hidden"  # hidden, recording, transcribing
        self._animation_phase = 0.0
        self._lock = threading.Lock()

    def start(self) -> bool:
        """Start the indicator window.
        
        Returns:
            True if started successfully.
        """
        if self._is_running:
            return True

        self._is_running = True
        self._thread = threading.Thread(target=self._run_window, daemon=True)
        self._thread.start()

        # Wait for window to be created
        for _ in range(50):  # Wait up to 0.5 seconds
            if self._root is not None:
                return True
            time.sleep(0.01)

        return self._root is not None

    def stop(self) -> None:
        """Stop and close the indicator window."""
        self._is_running = False
        if self._root:
            try:
                self._root.after(0, self._root.destroy)
            except tk.TclError:
                pass
        self._root = None
        self._canvas = None

    def show_recording(self) -> None:
        """Show the recording indicator."""
        with self._lock:
            self._state = "recording"
            self._animation_phase = 0.0
        self._update_visibility()

    def show_transcribing(self) -> None:
        """Show the transcribing/loading indicator."""
        with self._lock:
            self._state = "transcribing"
            self._animation_phase = 0.0
        self._update_visibility()

    def hide(self) -> None:
        """Hide the indicator."""
        with self._lock:
            self._state = "hidden"
        self._update_visibility()

    def _update_visibility(self) -> None:
        """Update window visibility based on state."""
        if not self._root:
            return

        def update():
            try:
                if self._state == "hidden":
                    self._root.withdraw()
                else:
                    self._root.deiconify()
                    self._root.lift()
                    # Keep window on top
                    self._root.attributes("-topmost", True)
            except tk.TclError:
                pass

        try:
            self._root.after(0, update)
        except (tk.TclError, AttributeError):
            pass

    def _run_window(self) -> None:
        """Run the tkinter window in its own thread."""
        try:
            self._root = tk.Tk()
            self._setup_window()
            self._create_canvas()

            # Start hidden
            self._root.withdraw()

            # Animation loop
            self._animate()

            self._root.mainloop()
        except Exception as e:
            logger.error(f"Recording indicator window error: {e}")
        finally:
            self._root = None
            self._canvas = None

    def _setup_window(self) -> None:
        """Set up the overlay window properties."""
        root = self._root

        # Get screen dimensions
        screen_width = root.winfo_screenwidth()

        # Remove window decorations
        root.overrideredirect(True)

        # Set window geometry at top of screen
        root.geometry(f"{screen_width}x{self.BAR_HEIGHT}+0+0")

        # Make window stay on top
        root.attributes("-topmost", True)

        # Make window transparent (Windows)
        try:
            root.attributes("-transparentcolor", "black")
        except tk.TclError:
            pass

        # Disable window focus
        root.attributes("-toolwindow", True)

        # Make window click-through (Windows)
        try:
            # This makes the window not receive mouse events
            root.wm_attributes("-disabled", True)
        except tk.TclError:
            pass

    def _create_canvas(self) -> None:
        """Create the drawing canvas."""
        self._canvas = tk.Canvas(
            self._root,
            width=self._root.winfo_screenwidth(),
            height=self.BAR_HEIGHT,
            highlightthickness=0,
            bg="black",
        )
        self._canvas.pack(fill=tk.BOTH, expand=True)

    def _animate(self) -> None:
        """Animation loop for the indicator."""
        if not self._is_running or not self._root:
            return

        try:
            with self._lock:
                state = self._state
                self._animation_phase += 0.1
                if self._animation_phase > 2 * 3.14159:
                    self._animation_phase = 0
                phase = self._animation_phase

            if state != "hidden" and self._canvas:
                self._draw_bar(state, phase)

            # Schedule next frame (roughly 30 FPS)
            self._root.after(33, self._animate)
        except tk.TclError:
            pass

    def _draw_bar(self, state: str, phase: float) -> None:
        """Draw the indicator bar.
        
        Args:
            state: Current state (recording or transcribing).
            phase: Animation phase (0 to 2*pi).
        """
        if not self._canvas:
            return

        self._canvas.delete("all")

        width = self._canvas.winfo_width()
        height = self.BAR_HEIGHT

        if state == "recording":
            # Pulsing red bar
            import math
            pulse = (math.sin(phase * 3) + 1) / 2  # 0 to 1

            # Draw gradient-like effect with pulsing
            for i in range(width):
                # Create a wave pattern
                wave = math.sin(phase * 2 + i * 0.02) * 0.3 + 0.7
                brightness = int(220 * wave * (0.6 + 0.4 * pulse))
                color = f"#{brightness:02x}3232"
                self._canvas.create_line(i, 0, i, height, fill=color)

        elif state == "transcribing":
            # Moving blue bar (loading animation)
            import math

            # Draw base blue
            self._canvas.create_rectangle(
                0, 0, width, height,
                fill=self.COLOR_TRANSCRIBING,
                outline="",
            )

            # Draw moving highlight
            highlight_width = width // 3
            highlight_pos = (math.sin(phase) + 1) / 2 * (width + highlight_width) - highlight_width

            # Draw gradient highlight
            for i in range(highlight_width):
                # Calculate gradient intensity
                gradient = 1 - abs(i - highlight_width / 2) / (highlight_width / 2)
                gradient = gradient ** 2  # Smooth falloff

                x = int(highlight_pos + i)
                if 0 <= x < width:
                    # Blend colors
                    r1, g1, b1 = 70, 130, 180  # Steel blue
                    r2, g2, b2 = 111, 168, 220  # Light blue

                    r = int(r1 + (r2 - r1) * gradient)
                    g = int(g1 + (g2 - g1) * gradient)
                    b = int(b1 + (b2 - b1) * gradient)

                    color = f"#{r:02x}{g:02x}{b:02x}"
                    self._canvas.create_line(x, 0, x, height, fill=color)

    @property
    def is_running(self) -> bool:
        """Check if the indicator is running."""
        return self._is_running and self._root is not None
