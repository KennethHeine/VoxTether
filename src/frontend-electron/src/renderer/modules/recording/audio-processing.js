/**
 * VoxTether Recording - Audio Processing
 *
 * Handles audio level monitoring and visualization during recording.
 */

import { getRecordingState, setRecordingState } from '../state.js';

/**
 * Set up audio level monitoring during recording
 */
export function setupRecordingLevelMonitor() {
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
export function stopRecordingLevelMonitor() {
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
