"""System tray management for VoxTether."""

import logging
import threading
from collections.abc import Callable
from pathlib import Path

try:
    from PIL import Image, ImageDraw
    from pystray import Icon, Menu, MenuItem
except ImportError as e:
    raise ImportError(
        "Required packages not installed. Run: pip install pystray Pillow"
    ) from e

logger = logging.getLogger(__name__)


MenuCallback = Callable[[], None]


def create_default_icon(size: int = 64, recording: bool = False) -> Image.Image:
    """Create a default tray icon.
    
    Args:
        size: Size of the icon in pixels.
        recording: Whether to show recording state (red).
        
    Returns:
        PIL Image object.
    """
    # Create a simple microphone-like icon
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    # Choose color based on state
    if recording:
        color = (220, 50, 50, 255)  # Red for recording
    else:
        color = (70, 130, 180, 255)  # Steel blue for idle

    # Draw a simple circle with a mic shape
    margin = size // 8
    draw.ellipse(
        [margin, margin, size - margin, size - margin],
        fill=color,
        outline=(255, 255, 255, 200),
        width=2,
    )

    # Draw a simple "V" for VoxTether
    center = size // 2
    v_size = size // 4
    draw.line(
        [center - v_size, center - v_size // 2, center, center + v_size // 2],
        fill=(255, 255, 255, 255),
        width=max(2, size // 16),
    )
    draw.line(
        [center, center + v_size // 2, center + v_size, center - v_size // 2],
        fill=(255, 255, 255, 255),
        width=max(2, size // 16),
    )

    return image


def load_icon(icon_path: Path | None, size: int = 64) -> Image.Image:
    """Load an icon from file or create a default one.
    
    Args:
        icon_path: Path to the icon file, or None for default.
        size: Size of the icon in pixels.
        
    Returns:
        PIL Image object.
    """
    if icon_path and icon_path.exists():
        try:
            img = Image.open(icon_path)
            return img.resize((size, size), Image.Resampling.LANCZOS)
        except Exception as e:
            logger.warning(f"Failed to load icon from {icon_path}: {e}")

    return create_default_icon(size)


class TrayManager:
    """Manages the system tray icon and menu."""

    def __init__(
        self,
        icon_path: Path | None = None,
        tooltip: str = "VoxTether",
    ):
        """Initialize the tray manager.
        
        Args:
            icon_path: Path to the tray icon file.
            tooltip: Tooltip text for the tray icon.
        """
        self._icon_path = icon_path
        self._tooltip = tooltip
        self._icon: Icon | None = None
        self._thread: threading.Thread | None = None
        self._is_running = False
        self._is_recording = False

        # Menu callbacks
        self._on_settings: MenuCallback | None = None
        self._on_test_mic: MenuCallback | None = None
        self._on_about: MenuCallback | None = None
        self._on_exit: MenuCallback | None = None
        self._on_open_models: MenuCallback | None = None
        self._on_open_logs: MenuCallback | None = None
        self._on_check_updates: MenuCallback | None = None

        # Status text
        self._status_text = "Ready"

    def set_callbacks(
        self,
        on_settings: MenuCallback | None = None,
        on_test_mic: MenuCallback | None = None,
        on_about: MenuCallback | None = None,
        on_exit: MenuCallback | None = None,
        on_open_models: MenuCallback | None = None,
        on_open_logs: MenuCallback | None = None,
        on_check_updates: MenuCallback | None = None,
    ) -> None:
        """Set menu item callbacks.
        
        Args:
            on_settings: Callback for Settings menu item.
            on_test_mic: Callback for Test Microphone menu item.
            on_about: Callback for About menu item.
            on_exit: Callback for Exit menu item.
            on_open_models: Callback for Open Models Folder menu item.
            on_open_logs: Callback for Open Logs menu item.
            on_check_updates: Callback for Check for Updates menu item.
        """
        self._on_settings = on_settings
        self._on_test_mic = on_test_mic
        self._on_about = on_about
        self._on_exit = on_exit
        self._on_open_models = on_open_models
        self._on_open_logs = on_open_logs
        self._on_check_updates = on_check_updates

    def _create_menu(self) -> Menu:
        """Create the tray menu."""
        items = [
            MenuItem(
                lambda _: f"Status: {self._status_text}",
                action=None,
                enabled=False,
            ),
            Menu.SEPARATOR,
            MenuItem(
                "Settings...",
                action=lambda: self._on_settings() if self._on_settings else None,
            ),
            Menu.SEPARATOR,
            MenuItem(
                "Test Microphone",
                action=lambda: self._on_test_mic() if self._on_test_mic else None,
            ),
            MenuItem(
                "Open Models Folder",
                action=lambda: self._on_open_models() if self._on_open_models else None,
            ),
            MenuItem(
                "Open Logs",
                action=lambda: self._on_open_logs() if self._on_open_logs else None,
            ),
            Menu.SEPARATOR,
            MenuItem(
                "Check for Updates...",
                action=lambda: self._on_check_updates() if self._on_check_updates else None,
            ),
            MenuItem(
                "About",
                action=lambda: self._on_about() if self._on_about else None,
            ),
            Menu.SEPARATOR,
            MenuItem(
                "Exit",
                action=self._exit,
            ),
        ]

        return Menu(*items)

    def _exit(self) -> None:
        """Handle exit from tray menu."""
        if self._on_exit:
            self._on_exit()
        self.stop()

    def start(self) -> bool:
        """Start the system tray icon.
        
        Returns:
            True if started successfully, False otherwise.
        """
        if self._is_running:
            return True

        try:
            # Load or create icon
            icon_image = load_icon(self._icon_path)

            # Create the tray icon
            self._icon = Icon(
                name="VoxTether",
                icon=icon_image,
                title=self._tooltip,
                menu=self._create_menu(),
            )

            # Run in a separate thread
            self._is_running = True
            self._thread = threading.Thread(
                target=self._icon.run,
                daemon=True,
            )
            self._thread.start()

            logger.info("Tray icon started")
            return True

        except Exception as e:
            logger.error(f"Failed to start tray icon: {e}")
            return False

    def stop(self) -> None:
        """Stop the system tray icon."""
        if self._icon:
            try:
                self._icon.stop()
            except Exception as e:
                logger.warning(f"Error stopping tray icon: {e}")

        self._is_running = False
        logger.info("Tray icon stopped")

    def set_recording(self, recording: bool) -> None:
        """Update the tray icon to show recording state.
        
        Args:
            recording: Whether currently recording.
        """
        self._is_recording = recording

        if recording:
            self._status_text = "Recording..."
        else:
            self._status_text = "Ready"

        if self._icon:
            try:
                new_icon = create_default_icon(64, recording)
                self._icon.icon = new_icon
            except Exception as e:
                logger.warning(f"Failed to update tray icon: {e}")

    def set_status(self, status: str) -> None:
        """Update the status text shown in the menu.
        
        Args:
            status: Status text to display.
        """
        self._status_text = status

    def show_notification(self, title: str, message: str) -> None:
        """Show a system notification.
        
        Args:
            title: Notification title.
            message: Notification message.
        """
        if self._icon:
            try:
                self._icon.notify(message, title)
            except Exception as e:
                logger.warning(f"Failed to show notification: {e}")

    @property
    def is_running(self) -> bool:
        """Check if the tray icon is running."""
        return self._is_running
