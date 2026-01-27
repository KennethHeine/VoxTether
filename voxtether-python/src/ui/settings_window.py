"""Settings window for VoxTether using tkinter."""

import logging
import tkinter as tk
from tkinter import ttk, messagebox
from typing import Callable, Optional

from ..settings import Settings, SettingsService
from ..model_manager import ModelManager, AVAILABLE_MODELS
from ..transcriber import Transcriber

logger = logging.getLogger(__name__)


class SettingsWindow:
    """Settings window for VoxTether configuration."""
    
    def __init__(
        self,
        settings_service: SettingsService,
        model_manager: ModelManager,
        transcriber: Transcriber,
        on_save: Optional[Callable[[Settings], None]] = None,
    ):
        """Initialize the settings window.
        
        Args:
            settings_service: The settings service.
            model_manager: The model manager.
            transcriber: The transcriber instance.
            on_save: Callback when settings are saved.
        """
        self._settings_service = settings_service
        self._model_manager = model_manager
        self._transcriber = transcriber
        self._on_save = on_save
        self._window: Optional[tk.Toplevel] = None
        self._root: Optional[tk.Tk] = None
        
        # Variables for form fields
        self._hotkey_var: Optional[tk.StringVar] = None
        self._model_var: Optional[tk.StringVar] = None
        self._language_var: Optional[tk.StringVar] = None
        self._device_var: Optional[tk.StringVar] = None
        self._compute_type_var: Optional[tk.StringVar] = None
        self._output_mode_var: Optional[tk.StringVar] = None
        self._show_notifications_var: Optional[tk.BooleanVar] = None
        self._show_recording_indicator_var: Optional[tk.BooleanVar] = None
        self._play_sounds_var: Optional[tk.BooleanVar] = None
        self._start_with_windows_var: Optional[tk.BooleanVar] = None
    
    def show(self, parent: Optional[tk.Tk] = None) -> None:
        """Show the settings window.
        
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
        self._load_settings()
        
        if parent:
            self._window.transient(parent)
            self._window.grab_set()
    
    def _setup_window(self) -> None:
        """Set up the window properties."""
        self._window.title("VoxTether Settings")
        self._window.geometry("500x600")
        self._window.resizable(True, True)
        self._window.minsize(400, 500)
        
        # Center on screen
        self._window.update_idletasks()
        x = (self._window.winfo_screenwidth() - 500) // 2
        y = (self._window.winfo_screenheight() - 600) // 2
        self._window.geometry(f"+{x}+{y}")
    
    def _create_widgets(self) -> None:
        """Create the window widgets."""
        # Create notebook for tabs
        notebook = ttk.Notebook(self._window)
        notebook.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # General tab
        general_frame = ttk.Frame(notebook, padding=10)
        notebook.add(general_frame, text="General")
        self._create_general_tab(general_frame)
        
        # Model tab
        model_frame = ttk.Frame(notebook, padding=10)
        notebook.add(model_frame, text="Model")
        self._create_model_tab(model_frame)
        
        # Performance tab
        perf_frame = ttk.Frame(notebook, padding=10)
        notebook.add(perf_frame, text="Performance")
        self._create_performance_tab(perf_frame)
        
        # Buttons
        button_frame = ttk.Frame(self._window, padding=10)
        button_frame.pack(fill=tk.X)
        
        ttk.Button(
            button_frame,
            text="Save",
            command=self._save_settings,
        ).pack(side=tk.RIGHT, padx=5)
        
        ttk.Button(
            button_frame,
            text="Cancel",
            command=self._close,
        ).pack(side=tk.RIGHT)
    
    def _create_general_tab(self, parent: ttk.Frame) -> None:
        """Create the general settings tab."""
        # Hotkey
        hotkey_frame = ttk.LabelFrame(parent, text="Hotkey", padding=10)
        hotkey_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(hotkey_frame, text="Push-to-talk hotkey:").pack(anchor=tk.W)
        self._hotkey_var = tk.StringVar()
        hotkey_entry = ttk.Entry(hotkey_frame, textvariable=self._hotkey_var)
        hotkey_entry.pack(fill=tk.X, pady=5)
        ttk.Label(
            hotkey_frame,
            text="Example: ctrl+shift+space",
            foreground="gray",
        ).pack(anchor=tk.W)
        
        # Output mode
        output_frame = ttk.LabelFrame(parent, text="Output", padding=10)
        output_frame.pack(fill=tk.X, pady=5)
        
        self._output_mode_var = tk.StringVar()
        ttk.Radiobutton(
            output_frame,
            text="Copy to clipboard only",
            variable=self._output_mode_var,
            value="clipboard",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            output_frame,
            text="Paste into focused application",
            variable=self._output_mode_var,
            value="focused_app",
        ).pack(anchor=tk.W)
        
        # UI Options
        ui_frame = ttk.LabelFrame(parent, text="Interface", padding=10)
        ui_frame.pack(fill=tk.X, pady=5)
        
        self._show_notifications_var = tk.BooleanVar()
        ttk.Checkbutton(
            ui_frame,
            text="Show notifications",
            variable=self._show_notifications_var,
        ).pack(anchor=tk.W)
        
        self._show_recording_indicator_var = tk.BooleanVar()
        ttk.Checkbutton(
            ui_frame,
            text="Show recording indicator",
            variable=self._show_recording_indicator_var,
        ).pack(anchor=tk.W)
        
        self._play_sounds_var = tk.BooleanVar()
        ttk.Checkbutton(
            ui_frame,
            text="Play sounds on start/stop",
            variable=self._play_sounds_var,
        ).pack(anchor=tk.W)
        
        # System
        system_frame = ttk.LabelFrame(parent, text="System", padding=10)
        system_frame.pack(fill=tk.X, pady=5)
        
        self._start_with_windows_var = tk.BooleanVar()
        ttk.Checkbutton(
            system_frame,
            text="Start with Windows",
            variable=self._start_with_windows_var,
        ).pack(anchor=tk.W)
    
    def _create_model_tab(self, parent: ttk.Frame) -> None:
        """Create the model settings tab."""
        # Model selection
        model_frame = ttk.LabelFrame(parent, text="Speech Recognition Model", padding=10)
        model_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(model_frame, text="Select model:").pack(anchor=tk.W)
        
        self._model_var = tk.StringVar()
        model_combo = ttk.Combobox(
            model_frame,
            textvariable=self._model_var,
            values=list(AVAILABLE_MODELS.keys()),
            state="readonly",
        )
        model_combo.pack(fill=tk.X, pady=5)
        model_combo.bind("<<ComboboxSelected>>", self._on_model_selected)
        
        # Model info
        self._model_info_label = ttk.Label(model_frame, text="", wraplength=400)
        self._model_info_label.pack(anchor=tk.W, pady=5)
        
        # Download button
        self._download_btn = ttk.Button(
            model_frame,
            text="Download Model",
            command=self._download_model,
        )
        self._download_btn.pack(anchor=tk.W, pady=5)
        
        # Language
        lang_frame = ttk.LabelFrame(parent, text="Language", padding=10)
        lang_frame.pack(fill=tk.X, pady=5)
        
        ttk.Label(lang_frame, text="Transcription language:").pack(anchor=tk.W)
        
        languages = [
            ("auto", "Auto-detect"),
            ("en", "English"),
            ("es", "Spanish"),
            ("fr", "French"),
            ("de", "German"),
            ("it", "Italian"),
            ("pt", "Portuguese"),
            ("ru", "Russian"),
            ("zh", "Chinese"),
            ("ja", "Japanese"),
            ("ko", "Korean"),
        ]
        
        self._language_var = tk.StringVar()
        lang_combo = ttk.Combobox(
            lang_frame,
            textvariable=self._language_var,
            values=[f"{code} - {name}" for code, name in languages],
            state="readonly",
        )
        lang_combo.pack(fill=tk.X, pady=5)
    
    def _create_performance_tab(self, parent: ttk.Frame) -> None:
        """Create the performance settings tab."""
        # Device selection
        device_frame = ttk.LabelFrame(parent, text="Compute Device", padding=10)
        device_frame.pack(fill=tk.X, pady=5)
        
        self._device_var = tk.StringVar()
        ttk.Radiobutton(
            device_frame,
            text="Auto (use GPU if available)",
            variable=self._device_var,
            value="auto",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            device_frame,
            text="NVIDIA GPU (CUDA)",
            variable=self._device_var,
            value="cuda",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            device_frame,
            text="CPU only",
            variable=self._device_var,
            value="cpu",
        ).pack(anchor=tk.W)
        
        # Device info
        device_info = self._transcriber.get_device_info()
        info_text = f"CUDA available: {'Yes' if device_info.cuda_available else 'No'}"
        if device_info.device_name:
            info_text += f"\nGPU: {device_info.device_name}"
        if device_info.cuda_version:
            info_text += f"\nCUDA version: {device_info.cuda_version}"
        
        ttk.Label(device_frame, text=info_text, foreground="gray").pack(anchor=tk.W, pady=5)
        
        # Compute type
        compute_frame = ttk.LabelFrame(parent, text="Precision", padding=10)
        compute_frame.pack(fill=tk.X, pady=5)
        
        self._compute_type_var = tk.StringVar()
        ttk.Radiobutton(
            compute_frame,
            text="Auto (recommended)",
            variable=self._compute_type_var,
            value="auto",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            compute_frame,
            text="Float16 (faster on GPU)",
            variable=self._compute_type_var,
            value="float16",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            compute_frame,
            text="Int8 (faster on CPU)",
            variable=self._compute_type_var,
            value="int8",
        ).pack(anchor=tk.W)
        ttk.Radiobutton(
            compute_frame,
            text="Float32 (highest accuracy)",
            variable=self._compute_type_var,
            value="float32",
        ).pack(anchor=tk.W)
    
    def _on_model_selected(self, event=None) -> None:
        """Handle model selection change."""
        model_name = self._model_var.get()
        model_info = AVAILABLE_MODELS.get(model_name)
        
        if model_info:
            downloaded = self._model_manager.is_model_downloaded(model_name)
            status = "✓ Downloaded" if downloaded else "Not downloaded"
            
            info_text = (
                f"{model_info.description}\n"
                f"Size: ~{model_info.size_mb} MB\n"
                f"Recommended for: {model_info.recommended_for}\n"
                f"Status: {status}"
            )
            self._model_info_label.config(text=info_text)
            
            self._download_btn.config(
                state=tk.DISABLED if downloaded else tk.NORMAL,
                text="Downloaded" if downloaded else "Download Model",
            )
    
    def _download_model(self) -> None:
        """Download the selected model."""
        model_name = self._model_var.get()
        if not model_name:
            return
        
        # Show progress dialog
        progress = tk.Toplevel(self._window)
        progress.title("Downloading Model")
        progress.geometry("300x100")
        progress.transient(self._window)
        progress.grab_set()
        
        ttk.Label(progress, text=f"Downloading {model_name}...").pack(pady=10)
        progress_bar = ttk.Progressbar(progress, mode="indeterminate")
        progress_bar.pack(fill=tk.X, padx=20, pady=10)
        progress_bar.start()
        
        def download():
            try:
                self._model_manager.download_model(model_name)
                progress.after(0, lambda: self._on_download_complete(progress, model_name))
            except Exception as e:
                progress.after(0, lambda: self._on_download_error(progress, str(e)))
        
        import threading
        thread = threading.Thread(target=download, daemon=True)
        thread.start()
    
    def _on_download_complete(self, progress: tk.Toplevel, model_name: str) -> None:
        """Handle download completion."""
        progress.destroy()
        messagebox.showinfo("Download Complete", f"Model '{model_name}' downloaded successfully!")
        self._on_model_selected()
    
    def _on_download_error(self, progress: tk.Toplevel, error: str) -> None:
        """Handle download error."""
        progress.destroy()
        messagebox.showerror("Download Failed", f"Failed to download model:\n{error}")
    
    def _load_settings(self) -> None:
        """Load current settings into the form."""
        settings = self._settings_service.settings
        
        self._hotkey_var.set(settings.hotkey)
        self._model_var.set(settings.model_name)
        self._language_var.set(f"{settings.language} - " if settings.language != "auto" else "auto - Auto-detect")
        self._device_var.set(settings.device)
        self._compute_type_var.set(settings.compute_type)
        self._output_mode_var.set(settings.output_mode)
        self._show_notifications_var.set(settings.show_notifications)
        self._show_recording_indicator_var.set(settings.show_recording_indicator)
        self._play_sounds_var.set(settings.play_sounds)
        self._start_with_windows_var.set(settings.start_with_windows)
        
        self._on_model_selected()
    
    def _save_settings(self) -> None:
        """Save settings and close."""
        # Extract language code from combo value
        lang_value = self._language_var.get()
        language = lang_value.split(" - ")[0] if " - " in lang_value else lang_value
        
        self._settings_service.update(
            hotkey=self._hotkey_var.get(),
            model_name=self._model_var.get(),
            language=language,
            device=self._device_var.get(),
            compute_type=self._compute_type_var.get(),
            output_mode=self._output_mode_var.get(),
            show_notifications=self._show_notifications_var.get(),
            show_recording_indicator=self._show_recording_indicator_var.get(),
            play_sounds=self._play_sounds_var.get(),
            start_with_windows=self._start_with_windows_var.get(),
        )
        
        if self._on_save:
            self._on_save(self._settings_service.settings)
        
        messagebox.showinfo("Settings Saved", "Settings have been saved successfully!")
        self._close()
    
    def _close(self) -> None:
        """Close the window."""
        if self._window:
            self._window.destroy()
            self._window = None
