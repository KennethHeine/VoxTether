# Suggested Features for VoxTether Backend

This document outlines potential features and improvements that could be added to the VoxTether backend.

---

## High Priority Features

### 1. WebSocket Support for Real-time Transcription

**Description:** Add WebSocket endpoint for streaming audio and receiving real-time transcription results.

**Benefits:**
- Lower latency for live transcription
- Continuous transcription without repeated HTTP requests
- Better user experience for longer dictation sessions

**Implementation Notes:**
- Use FastAPI's WebSocket support
- Stream audio chunks and return partial transcription results
- Requires faster-whisper streaming capability

---

### 2. Model Download Progress via WebSocket

**Description:** Replace SSE with WebSocket for more reliable download progress updates.

**Benefits:**
- Bidirectional communication (pause/cancel downloads)
- More reliable than SSE in some network configurations
- Consistent with real-time transcription feature

---

### 3. Batch Transcription Endpoint

**Description:** Add endpoint to transcribe multiple audio files in a single request.

**Benefits:**
- More efficient for bulk transcription
- Reduced overhead from multiple HTTP requests
- Useful for processing recorded meetings or lectures

**API Design:**
```
POST /api/transcribe/batch
Content-Type: multipart/form-data
files[]: audio1.wav, audio2.wav, ...
```

---

### 4. Custom Vocabulary / Prompt Support

**Description:** Allow users to provide custom vocabulary or initial prompts to improve accuracy for domain-specific terms.

**Benefits:**
- Better accuracy for technical terms, names, and jargon
- Customization per transcription request
- Useful for professional/enterprise users

**API Addition:**
```json
{
  "file": "<audio>",
  "language": "en",
  "initial_prompt": "Technical terms: API, FastAPI, WebSocket, JWT"
}
```

---

## Medium Priority Features

### 5. Transcription Queue with Job IDs

**Description:** Implement a job queue for async transcription with status tracking.

**Benefits:**
- Non-blocking for large files
- Client can poll or receive callbacks for completion
- Better handling of concurrent requests

**API Design:**
```
POST /api/transcribe/async -> { "job_id": "abc123" }
GET /api/transcribe/status/{job_id} -> { "status": "processing", "progress": 45 }
GET /api/transcribe/result/{job_id} -> { "text": "..." }
```

---

### 6. Word-Level Timestamps

**Description:** Return word-level timing information in transcription results.

**Benefits:**
- Enable subtitle/caption generation
- Support for audio-to-text synchronization
- Useful for editing and highlighting

**Response Addition:**
```json
{
  "text": "Hello world",
  "words": [
    {"word": "Hello", "start": 0.0, "end": 0.5},
    {"word": "world", "start": 0.5, "end": 1.0}
  ]
}
```

---

### 7. Speaker Diarization

**Description:** Identify and separate different speakers in the audio.

**Benefits:**
- Better transcription for meetings and interviews
- Attribution of text to specific speakers
- Essential for multi-person recordings

**Note:** May require additional libraries (pyannote-audio).

---

### 8. Audio Preprocessing Options

**Description:** Add options for audio preprocessing before transcription.

**Options:**
- Noise reduction
- Volume normalization
- Sample rate conversion
- Silence trimming

**Benefits:**
- Improved accuracy for noisy recordings
- Reduced processing time by removing silence
- Consistent quality regardless of input

---

### 9. Model Performance Metrics

**Description:** Track and expose transcription performance metrics.

**Metrics:**
- Average transcription time
- Real-time factor (audio duration / processing time)
- GPU/CPU utilization
- Memory usage

**Endpoint:**
```
GET /api/metrics -> {
  "total_transcriptions": 150,
  "average_rtf": 0.15,
  "gpu_utilization": 45.2
}
```

---

### 10. Language Model Hot-Swap

**Description:** Enable switching models without unloading the current model first.

**Benefits:**
- Faster model switching
- Keep fallback model loaded
- Support A/B testing of models

---

## Low Priority Features

### 11. Multi-GPU Support

**Description:** Distribute transcription across multiple GPUs.

**Benefits:**
- Increased throughput for high-volume scenarios
- Better utilization of available hardware
- Load balancing across devices

---

### 12. Model Caching with LRU

**Description:** Keep multiple models in memory with LRU eviction.

**Benefits:**
- Faster model switching for users who use multiple models
- Automatic memory management
- Configurable cache size

---

### 13. Audio Format Conversion API

**Description:** Add endpoint to convert audio files to optimal format.

**Endpoint:**
```
POST /api/audio/convert
Input: Any audio format
Output: 16kHz mono WAV (optimal for Whisper)
```

**Benefits:**
- Reduce transcription time
- Smaller file sizes for upload
- Consistent audio quality

---

### 14. Transcription History/Cache

**Description:** Cache recent transcriptions to avoid redundant processing.

**Benefits:**
- Instant results for repeated audio
- Reduced server load
- Audio fingerprinting for cache lookup

---

### 15. API Rate Limiting

**Description:** Implement rate limiting for API endpoints.

**Benefits:**
- Prevent abuse in shared/network deployments
- Fair resource allocation
- Configurable per-client limits

---

### 16. Authentication/API Keys

**Description:** Add optional API key authentication for network deployments.

**Benefits:**
- Secure multi-user deployments
- Usage tracking per user/key
- Revocable access

**Note:** Should remain optional for localhost usage.

---

### 17. Plugin/Extension System

**Description:** Allow users to add custom post-processing plugins.

**Use Cases:**
- Custom text formatting
- Punctuation correction
- Text-to-action conversion
- Integration with external services

---

### 18. Backup Model Sources

**Description:** Support alternative model sources beyond HuggingFace.

**Benefits:**
- Resilience if HuggingFace is unavailable
- Support for private model hosting
- Faster downloads from local mirrors

---

## Implementation Considerations

### Priority Matrix

| Feature | Impact | Effort | Priority |
|---------|--------|--------|----------|
| WebSocket Real-time | High | High | High |
| Custom Vocabulary | High | Low | High |
| Batch Transcription | Medium | Medium | Medium |
| Word Timestamps | Medium | Low | Medium |
| Job Queue | Medium | High | Medium |
| Speaker Diarization | High | High | Medium |
| Rate Limiting | Low | Low | Low |
| Authentication | Low | Medium | Low |

### Technical Dependencies

- **WebSocket Features**: No additional dependencies (FastAPI supports natively)
- **Speaker Diarization**: Requires pyannote-audio or similar
- **Audio Preprocessing**: May need additional audio processing libraries
- **Rate Limiting**: Consider slowapi or custom implementation

---

## Contributing

If you'd like to implement any of these features, please:

1. Open an issue to discuss the implementation approach
2. Reference this document in your PR
3. Include tests and documentation updates
4. Follow the existing code style (ruff for linting)

---

*Last Updated: January 2025*
