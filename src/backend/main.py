"""VoxTether Backend - FastAPI server for speech-to-text transcription."""

import logging
import os
import sys
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from api import health, models, transcribe
from config import settings
from services.transcriber import TranscriberService

# Configure logging
logging.basicConfig(
    level=logging.DEBUG if settings.debug else logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
    ],
)

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler."""
    logger.info("Starting VoxTether backend...")
    logger.info(f"Host: {settings.host}:{settings.port}")
    logger.info(f"Models path: {settings.models_path}")
    
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
    version="1.0.0",
    lifespan=lifespan,
)

# Add CORS middleware (localhost only)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Only accessible from localhost anyway
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
    uvicorn.run(
        "main:app",
        host=settings.host,
        port=settings.port,
        reload=settings.debug,
        log_level="debug" if settings.debug else "info",
    )


if __name__ == "__main__":
    main()
