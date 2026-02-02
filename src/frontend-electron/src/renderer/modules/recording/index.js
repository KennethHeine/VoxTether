/**
 * VoxTether Recording Module - Main Entry Point
 *
 * Orchestrates recording, audio processing, and transcription.
 * Re-exports all recording-related functions.
 */

import { getRecordingState, setRecordingState } from '../state.js';
import { showNotification } from '../notifications.js';
import { updateRecordingStatus } from '../status.js';
import { getAudioConstraints, createMediaRecorder } from './media-recorder.js';
import { setupRecordingLevelMonitor, stopRecordingLevelMonitor } from './audio-processing.js';
import { processRecording } from './transcription.js';

// Re-export functions from sub-modules
export { performTranscriptionOutput } from './transcription.js';
export {
    showTranscriptionPreviewModal,
    closePreviewModal,
    previewCopyOnly,
    previewInsert
} from './preview.js';

// Lock to prevent race conditions during recording state transitions
let isTransitioning = false;
// Flag to track if a stop was requested during a start transition
let pendingStop = false;

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
    // Prevent race condition with concurrent start/stop calls
    if (isTransitioning) {
        console.log('Recording state transition in progress, ignoring start');
        return;
    }

    const state = getRecordingState();
    if (state.isRecording) return;

    isTransitioning = true;
    pendingStop = false; // Clear any previous pending stop

    try {
        // Get selected microphone device from audio settings
        const micSelect = document.getElementById('mic-device-select');
        const deviceId = micSelect ? micSelect.value : undefined;

        const constraints = getAudioConstraints(deviceId);

        state.stream = await navigator.mediaDevices.getUserMedia(constraints);
        state.audioChunks = [];

        // Create MediaRecorder with callbacks
        state.mediaRecorder = createMediaRecorder(
            state.stream,
            (data) => {
                state.audioChunks.push(data);
            },
            async () => {
                await processRecording();
            }
        );

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
        // Hide overlay if recording failed to start
        await window.voxtether.hideOverlay();
    } finally {
        isTransitioning = false;

        // Process pending stop if one was queued during start
        if (pendingStop) {
            pendingStop = false;
            await handleStopRecording();
        }
    }
}

/**
 * Handle stop recording event from main process (hotkey released)
 */
export async function handleStopRecording() {
    // If a transition is in progress, queue this stop to be processed after
    if (isTransitioning) {
        console.log('Recording state transition in progress, queuing stop');
        pendingStop = true;
        return;
    }

    const state = getRecordingState();
    if (!state.isRecording) return;

    isTransitioning = true;

    try {
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
        }

        console.log('Recording stopped');
    } finally {
        isTransitioning = false;
    }
}
