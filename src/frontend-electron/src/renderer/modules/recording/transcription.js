/**
 * VoxTether Recording - Transcription Processing
 *
 * Handles audio transcription workflow, conversion, and output.
 */

import { getSettings, getRecordingState, setRecordingState } from '../state.js';
import { showNotification } from '../notifications.js';
import { updateRecordingStatus } from '../status.js';
import { addToHistory } from '../history.js';
import { updateStatistics } from '../statistics.js';
import { audioBufferToWav, uint8ArrayToBase64 } from '../utils.js';
import { getSupportedMimeType } from './media-recorder.js';

/**
 * Process the recorded audio and transcribe it
 */
export async function processRecording() {
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
                    await handleTranscriptionOutput(text, recordingDurationMs);

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
 * @param {number} durationMs - Recording duration in milliseconds
 */
async function handleTranscriptionOutput(text, durationMs) {
    const settings = getSettings();

    // Check if preview modal is enabled
    if (settings.showTranscriptionPreview) {
        // Dynamically import to avoid circular dependency
        const { showTranscriptionPreviewModal } = await import('./preview.js');
        showTranscriptionPreviewModal(text, durationMs);
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
