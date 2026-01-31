"""Structured logging utility for VoxTether backend."""

import contextvars
import json
import logging
import sys
import time
from pathlib import Path
from typing import Any, Dict, Optional
from uuid import uuid4


# Context variable for request ID tracking
request_id_var: contextvars.ContextVar[Optional[str]] = contextvars.ContextVar(
    "request_id", default=None
)


class StructuredFormatter(logging.Formatter):
    """JSON formatter for structured logging."""

    def format(self, record: logging.LogRecord) -> str:
        """Format log record as JSON.

        Args:
            record: Log record to format.

        Returns:
            JSON formatted log string.
        """
        log_data: Dict[str, Any] = {
            "timestamp": self.formatTime(record, self.datefmt),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
        }

        # Add request ID if available
        request_id = request_id_var.get()
        if request_id:
            log_data["request_id"] = request_id

        # Add exception info if present
        if record.exc_info:
            log_data["exception"] = self.formatException(record.exc_info)

        # Add any extra fields from the record
        # Standard LogRecord attributes to exclude
        standard_attrs = {
            'name', 'msg', 'args', 'created', 'filename', 'funcName', 'levelname',
            'levelno', 'lineno', 'module', 'msecs', 'message', 'pathname', 'process',
            'processName', 'relativeCreated', 'thread', 'threadName', 'exc_info',
            'exc_text', 'stack_info', 'taskName'
        }
        for key, value in record.__dict__.items():
            if key not in standard_attrs and not key.startswith('_'):
                log_data[key] = value

        return json.dumps(log_data)


class ConsoleFormatter(logging.Formatter):
    """Enhanced console formatter with request ID."""

    def format(self, record: logging.LogRecord) -> str:
        """Format log record for console output.

        Args:
            record: Log record to format.

        Returns:
            Formatted log string.
        """
        # Create a shallow copy to avoid mutating the original record
        record = logging.makeLogRecord(record.__dict__)
        
        request_id = request_id_var.get()
        if request_id:
            # Add request ID to the message
            record.msg = f"[{request_id[:8]}] {record.msg}"

        return super().format(record)


def setup_logging(
    log_file: Optional[Path] = None,
    debug: bool = False,
    json_format: bool = False,
) -> None:
    """Setup logging configuration.

    Args:
        log_file: Path to log file (optional).
        debug: Enable debug logging.
        json_format: Use JSON structured logging format.
    """
    log_level = logging.DEBUG if debug else logging.INFO

    # Create handlers
    handlers = []

    # Console handler
    console_handler = logging.StreamHandler(sys.stdout)
    if json_format:
        console_handler.setFormatter(StructuredFormatter())
    else:
        console_handler.setFormatter(
            ConsoleFormatter(
                "%(asctime)s - %(name)s - %(levelname)s - %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
        )
    handlers.append(console_handler)

    # File handler
    if log_file:
        log_file.parent.mkdir(parents=True, exist_ok=True)
        file_handler = logging.FileHandler(log_file, encoding="utf-8")
        if json_format:
            file_handler.setFormatter(StructuredFormatter())
        else:
            file_handler.setFormatter(
                logging.Formatter(
                    "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
                )
            )
        handlers.append(file_handler)

    # Configure root logger
    logging.basicConfig(
        level=log_level,
        handlers=handlers,
        force=True,  # Override any existing configuration
    )


def get_logger(name: str) -> logging.Logger:
    """Get a logger instance.

    Args:
        name: Logger name (typically __name__).

    Returns:
        Logger instance.
    """
    return logging.getLogger(name)


def set_request_id(request_id: Optional[str] = None) -> str:
    """Set request ID for the current context.

    Args:
        request_id: Request ID to set, or None to generate a new one.

    Returns:
        The request ID that was set.
    """
    if request_id is None:
        request_id = str(uuid4())
    request_id_var.set(request_id)
    return request_id


def get_request_id() -> Optional[str]:
    """Get the current request ID.

    Returns:
        Current request ID or None.
    """
    return request_id_var.get()


def clear_request_id() -> None:
    """Clear the request ID from the current context."""
    request_id_var.set(None)


class LogTimer:
    """Context manager for timing operations with logging."""

    def __init__(self, logger: logging.Logger, operation: str, level: int = logging.INFO):
        """Initialize the timer.

        Args:
            logger: Logger to use.
            operation: Description of the operation being timed.
            level: Log level to use.
        """
        self.logger = logger
        self.operation = operation
        self.level = level
        self.start_time: Optional[float] = None

    def __enter__(self) -> "LogTimer":
        """Start the timer."""
        self.start_time = time.time()
        self.logger.log(self.level, f"Starting: {self.operation}")
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        """Stop the timer and log the duration."""
        if self.start_time is not None:
            duration = time.time() - self.start_time
            if exc_type is None:
                self.logger.log(
                    self.level, f"Completed: {self.operation} ({duration:.2f}s)"
                )
            else:
                self.logger.error(
                    f"Failed: {self.operation} ({duration:.2f}s) - {exc_val}"
                )
