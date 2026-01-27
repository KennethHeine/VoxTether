"""First-run model setup window for VoxTether."""

import logging
import threading
import tkinter as tk
from tkinter import ttk, messagebox
from typing import Callable, Optional

from ..model_manager import ModelManager, AVAILABLE_MODELS
from ..transcriber import Transcriber

logger = logging.getLogger(__name__)


class ModelSetupWindow:
    """First-run model setup window."""
    
    def __init__(
        self,
        model_manager: ModelManager,
        transcriber: Transcriber,
        on_complete: Optional[Callable[[str], None]] = None,
        on_skip: Optional[Callable[[], None]] = None,
    ):
        """Initialize the model setup window.
        
        Args:
            model_manager: The model manager.
            transcriber: The transcriber instance.
            on_complete: Callback when setup is complete with selected model name.
            on_skip: Callback when user skips setup.
        """
        self._model_manager = model_manager
        self._transcriber = transcriber
        self._on_complete = on_complete
        self._on_skip = on_skip
        self._window: Optional[tk.Tk] = None
        self._selected_model: Optional[str] = None
    
    def show(self) -> None:
        """Show the model setup window."""
        self._window = tk.Tk()
        self._window.title("VoxTether - First Run Setup")
        self._window.geometry("600x700")
        self._window.resizable(True, True)
        self._window.minsize(500, 600)
        
        # Center on screen
        self._window.update_idletasks()
        x = (self._window.winfo_screenwidth() - 600) // 2
        y = (self._window.winfo_screenheight() - 700) // 2
        self._window.geometry(f"+{x}+{y}")
        
        self._create_widgets()
        self._window.mainloop()
    
    def _create_widgets(self) -> None:
        """Create the window widgets."""
        # Header
        header_frame = ttk.Frame(self._window, padding=20)
        header_frame.pack(fill=tk.X)
        
        ttk.Label(
            header_frame,
            text="Welcome to VoxTether!",
            font=("Segoe UI", 16, "bold"),
        ).pack(anchor=tk.W)
        
        ttk.Label(
            header_frame,
            text=(
                "VoxTether needs a speech recognition model to work.\n"
                "Please select and download a model to get started."
            ),
            wraplength=550,
        ).pack(anchor=tk.W, pady=10)
        
        # GPU detection info
        device_info = self._transcriber.get_device_info()
        if device_info.cuda_available:
            gpu_text = f"✓ NVIDIA GPU detected: {device_info.device_name or 'Unknown'}"
            gpu_color = "green"
        elif device_info.device_name:
            # GPU detected but CUDA not available (libraries not configured)
            gpu_text = f"⚠ NVIDIA GPU detected ({device_info.device_name}) but CUDA not configured. CPU mode will be used."
            gpu_color = "orange"
        else:
            gpu_text = "ℹ No NVIDIA GPU detected. CPU mode will be used."
            gpu_color = "gray"
        
        ttk.Label(header_frame, text=gpu_text, foreground=gpu_color, wraplength=550).pack(anchor=tk.W)
        
        # Model selection
        model_frame = ttk.LabelFrame(self._window, text="Select a Model", padding=15)
        model_frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        
        # Create a canvas with scrollbar for model list
        canvas = tk.Canvas(model_frame)
        scrollbar = ttk.Scrollbar(model_frame, orient=tk.VERTICAL, command=canvas.yview)
        scrollable_frame = ttk.Frame(canvas)
        
        scrollable_frame.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
        )
        
        canvas.create_window((0, 0), window=scrollable_frame, anchor=tk.NW)
        canvas.configure(yscrollcommand=scrollbar.set)
        
        canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Model radio buttons
        self._model_var = tk.StringVar(value="small")  # Default to small
        
        # Recommended models first
        recommended_order = ["small", "base", "tiny", "large-v3-turbo", "distil-large-v3", "medium", "large-v3"]
        
        for model_name in recommended_order:
            if model_name not in AVAILABLE_MODELS:
                continue
            
            model_info = AVAILABLE_MODELS[model_name]
            downloaded = self._model_manager.is_model_downloaded(model_name)
            
            frame = ttk.Frame(scrollable_frame, padding=5)
            frame.pack(fill=tk.X, pady=2)
            
            # Radio button
            radio = ttk.Radiobutton(
                frame,
                variable=self._model_var,
                value=model_name,
            )
            radio.pack(side=tk.LEFT)
            
            # Model name and badge
            name_frame = ttk.Frame(frame)
            name_frame.pack(side=tk.LEFT, fill=tk.X, expand=True)
            
            name_text = model_name
            if model_name == "small":
                name_text += " (Recommended)"
            
            ttk.Label(
                name_frame,
                text=name_text,
                font=("Segoe UI", 10, "bold"),
            ).pack(anchor=tk.W)
            
            # Description
            ttk.Label(
                name_frame,
                text=f"{model_info.description} | ~{model_info.size_mb} MB",
                foreground="gray",
            ).pack(anchor=tk.W)
            
            # Status indicator
            if downloaded:
                status_label = ttk.Label(frame, text="✓ Ready", foreground="green")
            else:
                status_label = ttk.Label(frame, text="Download required", foreground="orange")
            status_label.pack(side=tk.RIGHT, padx=10)
        
        # Buttons
        button_frame = ttk.Frame(self._window, padding=20)
        button_frame.pack(fill=tk.X)
        
        ttk.Button(
            button_frame,
            text="Skip (I'll download later)",
            command=self._skip,
        ).pack(side=tk.LEFT)
        
        self._download_btn = ttk.Button(
            button_frame,
            text="Download & Continue",
            command=self._download_and_continue,
        )
        self._download_btn.pack(side=tk.RIGHT)
    
    def _download_and_continue(self) -> None:
        """Download the selected model and continue."""
        model_name = self._model_var.get()
        
        if self._model_manager.is_model_downloaded(model_name):
            # Already downloaded, just continue
            self._complete(model_name)
            return
        
        # Show progress dialog
        progress_window = tk.Toplevel(self._window)
        progress_window.title("Downloading Model")
        progress_window.geometry("400x150")
        progress_window.transient(self._window)
        progress_window.grab_set()
        progress_window.resizable(False, False)
        
        # Center on parent
        progress_window.update_idletasks()
        x = self._window.winfo_x() + (self._window.winfo_width() - 400) // 2
        y = self._window.winfo_y() + (self._window.winfo_height() - 150) // 2
        progress_window.geometry(f"+{x}+{y}")
        
        ttk.Label(
            progress_window,
            text=f"Downloading {model_name} model...",
            font=("Segoe UI", 11),
            padding=20,
        ).pack()
        
        ttk.Label(
            progress_window,
            text="This may take a few minutes depending on your connection.",
            foreground="gray",
            padding=(20, 0, 20, 10),
        ).pack()
        
        progress_bar = ttk.Progressbar(progress_window, mode="indeterminate", length=350)
        progress_bar.pack(padx=20, pady=10)
        progress_bar.start(10)
        
        # Disable main window buttons
        self._download_btn.config(state=tk.DISABLED)
        
        def download():
            try:
                self._model_manager.download_model(model_name)
                self._window.after(0, lambda: self._on_download_success(progress_window, model_name))
            except Exception as e:
                self._window.after(0, lambda: self._on_download_error(progress_window, str(e)))
        
        thread = threading.Thread(target=download, daemon=True)
        thread.start()
    
    def _on_download_success(self, progress_window: tk.Toplevel, model_name: str) -> None:
        """Handle successful download."""
        progress_window.destroy()
        self._complete(model_name)
    
    def _on_download_error(self, progress_window: tk.Toplevel, error: str) -> None:
        """Handle download error."""
        progress_window.destroy()
        self._download_btn.config(state=tk.NORMAL)
        
        messagebox.showerror(
            "Download Failed",
            f"Failed to download the model:\n\n{error}\n\n"
            "Please check your internet connection and try again.",
        )
    
    def _complete(self, model_name: str) -> None:
        """Complete setup with the selected model."""
        self._selected_model = model_name
        
        if self._on_complete:
            self._on_complete(model_name)
        
        if self._window:
            self._window.destroy()
    
    def _skip(self) -> None:
        """Skip model setup."""
        result = messagebox.askquestion(
            "Skip Model Download?",
            "VoxTether won't work without a model.\n\n"
            "You can download a model later from Settings.\n\n"
            "Are you sure you want to skip?",
            icon="warning",
        )
        
        if result == "yes":
            if self._on_skip:
                self._on_skip()
            
            if self._window:
                self._window.destroy()
