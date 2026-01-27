"""Global hotkey listener for VoxTether."""

import logging
import threading
from typing import Callable

import keyboard

logger = logging.getLogger(__name__)


HotkeyCallback = Callable[[], None]


class HotkeyListener:
    """Listens for global hotkeys."""
    
    def __init__(self):
        """Initialize the hotkey listener."""
        self._hotkey_handlers: dict[str, HotkeyCallback] = {}
        self._registered_hotkeys: dict[str, object] = {}
        self._on_press_handlers: dict[str, HotkeyCallback] = {}
        self._on_release_handlers: dict[str, HotkeyCallback] = {}
        self._push_to_talk_hooks: dict[str, object] = {}  # Store hook handles
        self._is_running = False
        self._lock = threading.Lock()
    
    def register_hotkey(
        self,
        hotkey: str,
        callback: HotkeyCallback,
        suppress: bool = False,
    ) -> bool:
        """Register a hotkey.
        
        Args:
            hotkey: Hotkey combination (e.g., "ctrl+shift+space").
            callback: Function to call when the hotkey is pressed.
            suppress: Whether to suppress the hotkey from reaching other apps.
            
        Returns:
            True if registration was successful, False otherwise.
        """
        try:
            normalized = self._normalize_hotkey(hotkey)
            
            with self._lock:
                # Unregister existing handler if any
                if normalized in self._registered_hotkeys:
                    self._unregister_hotkey_internal(normalized)
                
                # Register the new hotkey
                handle = keyboard.add_hotkey(
                    normalized,
                    callback,
                    suppress=suppress,
                )
                
                self._registered_hotkeys[normalized] = handle
                self._hotkey_handlers[normalized] = callback
            
            logger.info(f"Registered hotkey: {normalized}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to register hotkey '{hotkey}': {e}")
            return False
    
    def register_push_to_talk(
        self,
        hotkey: str,
        on_press: HotkeyCallback,
        on_release: HotkeyCallback,
    ) -> bool:
        """Register a push-to-talk hotkey.
        
        Args:
            hotkey: Hotkey combination (e.g., "ctrl+shift+space").
            on_press: Function to call when the hotkey is pressed.
            on_release: Function to call when the hotkey is released.
            
        Returns:
            True if registration was successful, False otherwise.
        """
        try:
            normalized = self._normalize_hotkey(hotkey)
            
            with self._lock:
                # Unregister existing hook if any
                if normalized in self._push_to_talk_hooks:
                    try:
                        keyboard.unhook(self._push_to_talk_hooks[normalized])
                    except (KeyError, ValueError):
                        pass  # Hook already removed or invalid
                
                self._on_press_handlers[normalized] = on_press
                self._on_release_handlers[normalized] = on_release
                
                # Track pressed state
                pressed_state = {"pressed": False}
                
                def on_key_event(event):
                    """Handle key events for push-to-talk."""
                    # Check if all modifier keys are pressed
                    parts = normalized.lower().split("+")
                    modifiers = parts[:-1]
                    key = parts[-1]
                    
                    # Check if this event is for our key
                    if event.name.lower() != key:
                        return
                    
                    # Check modifiers
                    if "ctrl" in modifiers and not keyboard.is_pressed("ctrl"):
                        return
                    if "shift" in modifiers and not keyboard.is_pressed("shift"):
                        return
                    if "alt" in modifiers and not keyboard.is_pressed("alt"):
                        return
                    
                    if event.event_type == "down":
                        if not pressed_state["pressed"]:
                            pressed_state["pressed"] = True
                            try:
                                on_press()
                            except Exception as e:
                                logger.error(f"Error in on_press callback: {e}")
                    elif event.event_type == "up":
                        if pressed_state["pressed"]:
                            pressed_state["pressed"] = False
                            try:
                                on_release()
                            except Exception as e:
                                logger.error(f"Error in on_release callback: {e}")
                
                # Store the hook handle for later cleanup
                hook_handle = keyboard.hook(on_key_event)
                self._push_to_talk_hooks[normalized] = hook_handle
            
            logger.info(f"Registered push-to-talk hotkey: {normalized}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to register push-to-talk hotkey '{hotkey}': {e}")
            return False
    
    def unregister_hotkey(self, hotkey: str) -> bool:
        """Unregister a hotkey.
        
        Args:
            hotkey: Hotkey combination to unregister.
            
        Returns:
            True if unregistration was successful, False otherwise.
        """
        normalized = self._normalize_hotkey(hotkey)
        
        with self._lock:
            return self._unregister_hotkey_internal(normalized)
    
    def _unregister_hotkey_internal(self, normalized: str) -> bool:
        """Internal method to unregister a hotkey (must hold lock)."""
        try:
            if normalized in self._registered_hotkeys:
                keyboard.remove_hotkey(self._registered_hotkeys[normalized])
                del self._registered_hotkeys[normalized]
            
            if normalized in self._push_to_talk_hooks:
                try:
                    keyboard.unhook(self._push_to_talk_hooks[normalized])
                except (KeyError, ValueError):
                    pass  # Hook already removed or invalid
                del self._push_to_talk_hooks[normalized]
            
            if normalized in self._hotkey_handlers:
                del self._hotkey_handlers[normalized]
            
            if normalized in self._on_press_handlers:
                del self._on_press_handlers[normalized]
            
            if normalized in self._on_release_handlers:
                del self._on_release_handlers[normalized]
            
            logger.info(f"Unregistered hotkey: {normalized}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to unregister hotkey '{normalized}': {e}")
            return False
    
    def unregister_all(self) -> None:
        """Unregister all hotkeys."""
        with self._lock:
            for normalized in list(self._registered_hotkeys.keys()):
                self._unregister_hotkey_internal(normalized)
        
        try:
            keyboard.unhook_all()
        except Exception as e:
            logger.warning(f"Error unhooking all: {e}")
    
    def _normalize_hotkey(self, hotkey: str) -> str:
        """Normalize a hotkey string.
        
        Args:
            hotkey: Hotkey string (e.g., "Ctrl + Shift + Space").
            
        Returns:
            Normalized hotkey string (e.g., "ctrl+shift+space").
        """
        # Remove spaces and lowercase
        normalized = hotkey.lower().replace(" ", "")
        
        # Normalize common key names
        replacements = {
            "control": "ctrl",
            "command": "ctrl",
            "windows": "win",
            "option": "alt",
        }
        
        for old, new in replacements.items():
            normalized = normalized.replace(old, new)
        
        return normalized
    
    def parse_hotkey_string(self, hotkey: str) -> tuple[list[str], str]:
        """Parse a hotkey string into modifiers and key.
        
        Args:
            hotkey: Hotkey string (e.g., "ctrl+shift+space").
            
        Returns:
            Tuple of (modifiers, key).
        """
        normalized = self._normalize_hotkey(hotkey)
        parts = normalized.split("+")
        
        if len(parts) == 1:
            return [], parts[0]
        
        return parts[:-1], parts[-1]
    
    def format_hotkey(self, modifiers: list[str], key: str) -> str:
        """Format modifiers and key into a hotkey string.
        
        Args:
            modifiers: List of modifier keys.
            key: The main key.
            
        Returns:
            Formatted hotkey string.
        """
        all_parts = modifiers + [key]
        return "+".join(all_parts)
