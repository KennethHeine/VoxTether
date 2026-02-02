"""VoxTether Backend - FastAPI server for speech-to-text transcription."""

import sys
import time
from contextlib import asynccontextmanager
from pathlib import Path

import uvicorn
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from api import health, models, transcribe
from config import settings
from constants import APP_VERSION
from exceptions import VoxTetherError
from services.transcriber import TranscriberService
from utils.logging import setup_logging, get_logger

# Setup logging
log_file = Path(settings.logs_path) / "backend.log"
setup_logging(
    log_file=log_file,
    debug=settings.debug,
    json_format=False,  # Can be made configurable
)

logger = get_logger(__name__)

# Track application start time for uptime calculation
APP_START_TIME = time.time()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler."""
    logger.info("Starting VoxTether backend...")
    logger.info(f"Host: {settings.host}:{settings.port}")
    logger.info(f"Models path: {settings.models_path}")
    
    # Store start time in app state
    app.state.start_time = APP_START_TIME
    
    # Initialize the transcriber service
    transcriber = TranscriberService()
    app.state.transcriber = transcriber
    
    # Preload model if configured
    if settings.preload_model:
        logger.info(f"Preloading model: {settings.default_model}")
        try:
            await transcriber.load_model(settings.default_model)
            logger.info("Model preloaded successfully")
        except Exception as e:
            logger.warning(f"Failed to preload model: {e}")
    
    yield
    
    # Cleanup
    logger.info("Shutting down VoxTether backend...")
    if hasattr(app.state, "transcriber"):
        app.state.transcriber.unload_model()


app = FastAPI(
    title="VoxTether Backend",
    description="Speech-to-text transcription API using faster-whisper",
    version=APP_VERSION,
    lifespan=lifespan,
)


# Exception handlers
@app.exception_handler(VoxTetherError)
async def voxtether_exception_handler(request: Request, exc: VoxTetherError):
    """Handle VoxTether custom exceptions.
    
    Args:
        request: FastAPI request object.
        exc: VoxTether exception.
        
    Returns:
        JSON error response.
    """
    logger.error(f"VoxTether error: {exc.message}")
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "detail": exc.message,  # FastAPI standard field
            "error": exc.message,
            "status_code": exc.status_code,
        },
    )


# Add CORS middleware (localhost only)
# Note: Electron apps make direct HTTP requests, but we restrict CORS for browser access
app.add_middleware(
    CORSMiddleware,
    allow_origin_regex=r"^https?://(localhost|127\.0\.0\.1)(:\d+)?$",
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include routers
app.include_router(health.router, prefix="/api", tags=["Health"])
app.include_router(transcribe.router, prefix="/api", tags=["Transcription"])
app.include_router(models.router, prefix="/api", tags=["Models"])


def main():
    """Run the backend server."""
    logger.info(f"Starting server on {settings.host}:{settings.port}")
    
    # Check if running as PyInstaller bundle
    is_frozen = getattr(sys, 'frozen', False)
    
    if is_frozen:
        # PyInstaller bundle: must pass app object directly, reload not supported
        uvicorn.run(
            app,
            host=settings.host,
            port=settings.port,
            log_level="debug" if settings.debug else "info",
        )
    else:
        # Normal Python: use string reference to support reload in debug mode
        uvicorn.run(
            "main:app",
            host=settings.host,
            port=settings.port,
            reload=settings.debug,
            log_level="debug" if settings.debug else "info",
        )


if __name__ == "__main__":
    main()
