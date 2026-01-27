"""Text injection for VoxTether using clipboard."""

import logging
import time
from enum import Enum
from typing import Optional

import pyperclip

logger = logging.getLogger(__name__)


class InjectionMode(Enum):
    """Mode for text injection."""
    CLIPBOARD = "clipboard"  # Copy to clipboard only
    FOCUSED_APP = "focused_app"  # Paste into focused application


class TextInjector:
    """Injects text into applications using clipboard."""
    
    def __init__(self, mode: InjectionMode = InjectionMode.CLIPBOARD):
        """Initialize the text injector.
        
        Args:
            mode: The injection mode to use.
        """
        self._mode = mode
        self._clipboard_delay_ms = 100
    
    @property
    def mode(self) -> InjectionMode:
        """Get the current injection mode."""
        return self._mode
    
    @mode.setter
    def mode(self, value: InjectionMode) -> None:
        """Set the injection mode."""
        self._mode = value
    
    @property
    def clipboard_delay_ms(self) -> int:
        """Get the clipboard delay in milliseconds."""
        return self._clipboard_delay_ms
    
    @clipboard_delay_ms.setter
    def clipboard_delay_ms(self, value: int) -> None:
        """Set the clipboard delay in milliseconds."""
        self._clipboard_delay_ms = max(0, value)
    
    def inject(self, text: str) -> bool:
        """Inject text according to the current mode.
        
        Args:
            text: The text to inject.
            
        Returns:
            True if injection was successful, False otherwise.
        """
        if not text:
            logger.warning("No text to inject")
            return False
        
        if self._mode == InjectionMode.CLIPBOARD:
            return self._copy_to_clipboard(text)
        else:
            return self._paste_to_focused_app(text)
    
    def _copy_to_clipboard(self, text: str) -> bool:
        """Copy text to clipboard.
        
        Args:
            text: The text to copy.
            
        Returns:
            True if successful, False otherwise.
        """
        try:
            pyperclip.copy(text)
            logger.info(f"Copied to clipboard: '{text[:50]}...'")
            return True
        except Exception as e:
            logger.error(f"Failed to copy to clipboard: {e}")
            return False
    
    def _paste_to_focused_app(self, text: str) -> bool:
        """Paste text into the focused application.
        
        This copies text to clipboard and then simulates Ctrl+V.
        
        Args:
            text: The text to paste.
            
        Returns:
            True if successful, False otherwise.
        """
        try:
            import keyboard
            
            # Copy new text to clipboard
            pyperclip.copy(text)
            
            # Wait for clipboard to update
            time.sleep(self._clipboard_delay_ms / 1000.0)
            
            # Simulate Ctrl+V
            keyboard.send("ctrl+v")
            
            # Wait a bit more for paste to complete
            time.sleep(self._clipboard_delay_ms / 1000.0)
            
            # Note: We intentionally do not restore the original clipboard content,
            # as doing so could interfere with the user's expected clipboard state.
            
            logger.info(f"Pasted to focused app: '{text[:50]}...'")
            return True
            
        except Exception as e:
            logger.error(f"Failed to paste to focused app: {e}")
            
            # Fallback to clipboard only
            return self._copy_to_clipboard(text)
    
    def get_clipboard_content(self) -> Optional[str]:
        """Get the current clipboard content.
        
        Returns:
            The clipboard content, or None if unavailable.
        """
        try:
            return pyperclip.paste()
        except Exception as e:
            logger.error(f"Failed to get clipboard content: {e}")
            return None
    
    def clear_clipboard(self) -> bool:
        """Clear the clipboard.
        
        Returns:
            True if successful, False otherwise.
        """
        try:
            pyperclip.copy("")
            return True
        except Exception as e:
            logger.error(f"Failed to clear clipboard: {e}")
            return False
