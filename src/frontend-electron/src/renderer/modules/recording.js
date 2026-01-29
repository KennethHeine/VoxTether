/**
 * VoxTether Recording Module
 *
 * Handles push-to-talk recording, audio processing, and transcription.
 */

import { getSettings, getRecordingState, setRecordingState } from './state.js';
import { showNotification } from './notifications.js';
import { updateRecordingStatus } from './status.js';
import { addToHistory } from './history.js';
import { updateStatistics } from './statistics.js';
import { audioBufferToWav, uint8ArrayToBase64 } from './utils.js';

/**
 * Start test recording (triggered by the test button in settings)
 */
export async function startTestRecording() {
    try {
        await window.voxtether.startRecordingManual();
    } catch (error) {
        console.error('Failed to start recording:', error);
        showNotification('Failed to start recording', 'error');
    }
}

/**
 * Stop test recording (triggered by the test button in settings)
 */
export async function stopTestRecording() {
    try {
        await window.voxtether.stopRecordingManual();
    } catch (error) {
        console.error('Failed to stop recording:', error);
        showNotification('Failed to stop recording', 'error');
    }
}

/**
 * Update the test recording UI buttons
 * @param {boolean} isRecording - Whether recording is in progress
 */
export function updateTestRecordingUI(isRecording) {
    const startBtn = document.getElementById('start-test-recording-btn');
    const stopBtn = document.getElementById('stop-test-recording-btn');
    const statusSpan = document.getElementById('recording-status');

    if (!startBtn || !stopBtn || !statusSpan) return;

    if (isRecording) {
        startBtn.classList.add('hidden');
        stopBtn.classList.remove('hidden');
        statusSpan.textContent = '🔴 Recording...';
        statusSpan.className = 'recording-status recording';
    } else {
        startBtn.classList.remove('hidden');
        stopBtn.classList.add('hidden');
        if (statusSpan.classList.contains('transcribing')) {
            // Keep transcribing status
        } else {
            statusSpan.textContent = '';
            statusSpan.className = 'recording-status';
        }
    }
}

/**
 * Handle start recording event from main process (hotkey triggered)
 */
export async function handleStartRecording() {
    const state = getRecordingState();
    if (state.isRecording) return;

    try {
        // Get selected microphone device from audio settings
        const micSelect = document.getElementById('mic-device-select');
        const deviceId = micSelect ? micSelect.value : undefined;

        const constraints = {
            audio: deviceId ? {
                deviceId: { exact: deviceId },
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: false
            } : {
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: false
            }
        };

        state.stream = await navigator.mediaDevices.getUserMedia(constraints);
        state.audioChunks = [];

        // Create MediaRecorder with supported MIME type
        const mimeType = getSupportedMimeType();
        state.mediaRecorder = new MediaRecorder(state.stream, {
            mimeType: mimeType
        });

        state.mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                state.audioChunks.push(event.data);
            }
        };

        state.mediaRecorder.onstop = async () => {
            await processRecording();
        };

        state.mediaRecorder.start(100); // Collect data every 100ms
        state.isRecording = true;
        state.startTime = Date.now();  // Record start time for duration tracking

        setRecordingState(state);

        // Set up audio level monitoring
        setupRecordingLevelMonitor();

        console.log('Recording started');
        updateRecordingStatus('recording');

    } catch (error) {
        console.error('Failed to start recording:', error);
        showNotification('Failed to access microphone: ' + error.message, 'error');
        updateRecordingStatus('error');
    }
}

/**
 * Handle stop recording event from main process (hotkey released)
 */
export async function handleStopRecording() {
    const state = getRecordingState();
    if (!state.isRecording) return;

    state.isRecording = false;
    setRecordingState(state);

    // Stop audio level monitoring
    stopRecordingLevelMonitor();

    if (state.mediaRecorder && state.mediaRecorder.state !== 'inactive') {
        state.mediaRecorder.stop();
    }

    // Stop all tracks
    if (state.stream) {
        state.stream.getTracks().forEach(track => track.stop());
        state.stream = null;
        setRecordingState(state);
    }

    console.log('Recording stopped');
}

/**
 * Process the recorded audio and send to backend for transcription
 */
async function processRecording() {
    const state = getRecordingState();
    const settings = getSettings();

    if (state.audioChunks.length === 0) {
        console.log('No audio data recorded');
        updateRecordingStatus('ready');
        // Hide overlay when no audio
        await window.voxtether.hideOverlay();
        return;
    }

    updateRecordingStatus('transcribing');
    // Show transcribing overlay (loading state)
    await window.voxtether.showTranscribingOverlay();

    let tempPath = null;
    let audioBase64 = null;

    // Calculate recording duration
    const recordingDurationMs = state.startTime ? Date.now() - state.startTime : 0;

    try {
        // Create audio blob from chunks
        const mimeType = getSupportedMimeType();
        const audioBlob = new Blob(state.audioChunks, { type: mimeType });

        // Convert to WAV for backend compatibility
        const wavBlob = await convertToWav(audioBlob);

        // Save to temp file and transcribe
        const arrayBuffer = await wavBlob.arrayBuffer();
        const uint8Array = new Uint8Array(arrayBuffer);

        // Keep base64 for potential saving - only encode if we'll actually save
        if (settings.saveRecordingAudio && settings.recordingOutputFolder) {
            audioBase64 = uint8ArrayToBase64(uint8Array);
        }

        // Create a temporary file path
        tempPath = await saveTempAudio(uint8Array);

        if (tempPath) {
            // Transcribe using backend
            const language = settings.language || 'auto';
            const result = await window.voxtether.transcribe(tempPath, language);

            if (result.success && result.data && result.data.text) {
                const text = result.data.text.trim();
                if (text) {
                    // Copy to clipboard based on output mode
                    await handleTranscriptionOutput(text);

                    // Save audio and/or transcript to output folder if enabled
                    await saveRecordingToFolder(audioBase64, text);

                    // Add to history
                    addToHistory(text, recordingDurationMs);

                    // Update statistics
                    updateStatistics(recordingDurationMs, text.length);

                    showNotification('Transcription complete', 'success');
                } else {
                    showNotification('No speech detected', 'info');
                }
            } else {
                const errorMsg = result.error || result.data?.error || 'Transcription failed';
                showNotification(errorMsg, 'error');
            }
        }
    } catch (error) {
        console.error('Failed to process recording:', error);
        showNotification('Failed to process recording: ' + error.message, 'error');
    } finally {
        // Clean up temp file
        if (tempPath) {
            try {
                await window.voxtether.deleteTempFile(tempPath);
            } catch (_e) {
                // Ignore cleanup errors
            }
        }
        // Hide overlay when transcription is complete
        await window.voxtether.hideOverlay();
    }

    state.audioChunks = [];
    setRecordingState(state);
    updateRecordingStatus('ready');
}

/**
 * Get a supported MIME type for MediaRecorder
 * @returns {string}
 */
function getSupportedMimeType() {
    const types = [
        'audio/webm;codecs=opus',
        'audio/webm',
        'audio/ogg;codecs=opus',
        'audio/mp4',
        'audio/wav'
    ];

    for (const type of types) {
        if (MediaRecorder.isTypeSupported(type)) {
            return type;
        }
    }

    return 'audio/webm'; // Default fallback
}

/**
 * Convert audio blob to WAV format
 * @param {Blob} blob - Audio blob to convert
 * @returns {Promise<Blob>}
 */
async function convertToWav(blob) {
    return new Promise((resolve, reject) => {
        const audioContext = new (window.AudioContext || window.webkitAudioContext)();
        const reader = new FileReader();

        reader.onload = async () => {
            try {
                const arrayBuffer = reader.result;
                const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);

                // Convert to WAV
                const wavBuffer = audioBufferToWav(audioBuffer);
                const wavBlob = new Blob([wavBuffer], { type: 'audio/wav' });

                audioContext.close();
                resolve(wavBlob);
            } catch (error) {
                audioContext.close();
                reject(error);
            }
        };

        reader.onerror = reject;
        reader.readAsArrayBuffer(blob);
    });
}

/**
 * Save audio data to temp file and return path
 * @param {Uint8Array} uint8Array - Audio data
 * @returns {Promise<string|null>}
 */
async function saveTempAudio(uint8Array) {
    try {
        // Convert Uint8Array to base64 for IPC transfer
        let binary = '';
        const len = uint8Array.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(uint8Array[i]);
        }
        const base64Audio = btoa(binary);

        // Save using the binary file handler
        const result = await window.voxtether.saveAudioFile(base64Audio);

        if (result.success) {
            return result.filePath;
        } else {
            console.error('Failed to save audio file:', result.error);
            return null;
        }
    } catch (error) {
        console.error('Failed to save temp audio:', error);
        return null;
    }
}

/**
 * Handle transcription output based on settings
 * @param {string} text - The transcribed text
 */
async function handleTranscriptionOutput(text) {
    const settings = getSettings();

    // Check if preview modal is enabled
    if (settings.showTranscriptionPreview) {
        showTranscriptionPreviewModal(text);
        return; // Preview modal handles the output
    }

    // Direct output without preview
    await performTranscriptionOutput(text);
}

/**
 * Perform the actual transcription output (clipboard/paste)
 * @param {string} text - Text to output
 */
export async function performTranscriptionOutput(text) {
    const settings = getSettings();
    const outputMode = settings.outputMode || 'ClipboardAndPaste';

    switch (outputMode) {
    case 'ClipboardAndPaste':
    case 'Clipboard':
        await window.voxtether.copyToClipboard(text);
        break;
    case 'SimulateTyping':
        // Typing simulation would require additional implementation
        await window.voxtether.copyToClipboard(text);
        break;
    }
}

/**
 * Save recording and transcript to a timestamped folder
 * @param {string|null} audioBase64 - Base64 encoded audio data
 * @param {string} transcriptText - Transcript text
 */
async function saveRecordingToFolder(audioBase64, transcriptText) {
    const settings = getSettings();
    const shouldSaveAudio = settings.saveRecordingAudio;
    const shouldSaveTranscript = settings.saveRecordingTranscript;
    const outputFolder = settings.recordingOutputFolder;

    if (!outputFolder || (!shouldSaveAudio && !shouldSaveTranscript)) {
        return;
    }

    try {
        const result = await window.voxtether.saveRecordingOutput({
            audioData: audioBase64,
            transcript: transcriptText,
            baseFolder: outputFolder,
            saveAudio: shouldSaveAudio,
            saveTranscript: shouldSaveTranscript
        });

        if (!result.success) {
            console.warn('Failed to save recording output:', result.error);
        }
    } catch (error) {
        console.error('Failed to save recording output:', error);
    }
}

// Preview modal state
let _pendingPreviewDuration = 0;

/**
 * Show the transcription preview modal
 * @param {string} text - Transcription text
 * @param {number} durationMs - Recording duration
 */
export function showTranscriptionPreviewModal(text, durationMs = 0) {
    _pendingPreviewDuration = durationMs;
    const modal = document.getElementById('transcription-preview-modal');
    const textarea = document.getElementById('preview-text');

    if (!modal || !textarea) return;

    textarea.value = text;
    modal.classList.remove('hidden');
    textarea.focus();
    textarea.select();
}

/**
 * Close the preview modal (cancel)
 */
export function closePreviewModal() {
    const modal = document.getElementById('transcription-preview-modal');
    if (!modal) return;

    modal.classList.add('hidden');
    document.getElementById('preview-text').value = '';
    _pendingPreviewDuration = 0;
}

/**
 * Copy only without inserting (from preview modal)
 */
export async function previewCopyOnly() {
    const text = document.getElementById('preview-text').value.trim();
    if (text) {
        await window.voxtether.copyToClipboard(text);
        showNotification('Copied to clipboard', 'success');
    }
    closePreviewModal();
}

/**
 * Insert the text (copy to clipboard and close)
 */
export async function previewInsert() {
    const text = document.getElementById('preview-text').value.trim();
    if (text) {
        await performTranscriptionOutput(text);
        showNotification('Transcription inserted', 'success');
    }
    closePreviewModal();
}

/**
 * Set up audio level monitoring during recording
 */
function setupRecordingLevelMonitor() {
    const state = getRecordingState();
    if (!state.stream) return;

    try {
        // Create audio context and analyser
        state.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        state.analyser = state.audioContext.createAnalyser();
        state.analyser.fftSize = 256;
        state.analyser.smoothingTimeConstant = 0.8;

        const source = state.audioContext.createMediaStreamSource(state.stream);
        source.connect(state.analyser);

        state.audioData = new Uint8Array(state.analyser.frequencyBinCount);
        setRecordingState(state);

        // Show the level meter
        const levelMeter = document.getElementById('recording-level-meter');
        if (levelMeter) {
            levelMeter.classList.remove('hidden');
        }

        // Start animation loop
        animateRecordingLevel();

    } catch (error) {
        console.error('Failed to set up level monitor:', error);
    }
}

/**
 * Stop audio level monitoring
 */
function stopRecordingLevelMonitor() {
    const state = getRecordingState();

    // Cancel animation
    if (state.levelAnimationId) {
        cancelAnimationFrame(state.levelAnimationId);
        state.levelAnimationId = null;
    }

    // Close audio context
    if (state.audioContext) {
        try {
            state.audioContext.close();
        } catch (_e) {
            // Ignore close errors
        }
        state.audioContext = null;
        state.analyser = null;
        state.audioData = null;
    }

    setRecordingState(state);

    // Hide the level meter and reset bar
    const levelMeter = document.getElementById('recording-level-meter');
    const levelBar = document.getElementById('recording-level-bar');
    if (levelMeter) {
        levelMeter.classList.add('hidden');
    }
    if (levelBar) {
        levelBar.style.width = '0%';
    }
}

/**
 * Animation loop for recording level meter
 */
function animateRecordingLevel() {
    const state = getRecordingState();

    if (!state.isRecording || !state.analyser) {
        return;
    }

    // Get audio data
    state.analyser.getByteFrequencyData(state.audioData);

    // Calculate average level
    let sum = 0;
    for (let i = 0; i < state.audioData.length; i++) {
        sum += state.audioData[i];
    }
    const average = sum / state.audioData.length;
    const level = Math.min(100, (average / 128) * 100);

    // Update level bar
    const levelBar = document.getElementById('recording-level-bar');
    if (levelBar) {
        levelBar.style.width = `${level}%`;
    }

    // Schedule next frame
    state.levelAnimationId = requestAnimationFrame(animateRecordingLevel);
    setRecordingState(state);
}
