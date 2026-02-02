/**
 * VoxTether Mic Test Module
 *
 * Handles microphone testing with real-time visualization.
 */

import { getMicTestState, setMicTestState } from './state.js';
import {
    MIC_TEST_FFT_SIZE,
    AUDIO_SMOOTHING_TIME_CONSTANT,
    AUDIO_LEVEL_NORMALIZATION,
    RMS_SCALE_FACTOR,
    PEAK_DECAY_RATE,
    WAVEFORM_COLORS,
    WAVEFORM_LINE_WIDTH,
    CENTER_LINE_WIDTH
} from './audio-constants.js';

/**
 * Check if mic test is currently running
 * @returns {boolean}
 */
export function isMicTestRunning() {
    return getMicTestState().isRunning;
}

/**
 * Load available microphone devices
 */
export async function loadMicDevices() {
    const micSelect = document.getElementById('mic-device-select');
    if (!micSelect) return;

    try {
        // Request permission first
        await navigator.mediaDevices.getUserMedia({ audio: true })
            .then(stream => stream.getTracks().forEach(track => track.stop()));

        const devices = await navigator.mediaDevices.enumerateDevices();
        const audioInputs = devices.filter(d => d.kind === 'audioinput');

        micSelect.innerHTML = '';

        if (audioInputs.length === 0) {
            const option = document.createElement('option');
            option.value = '';
            option.textContent = 'No microphones found';
            micSelect.appendChild(option);
            return;
        }

        audioInputs.forEach((device, index) => {
            const option = document.createElement('option');
            option.value = device.deviceId;
            option.textContent = device.label || `Microphone ${index + 1}`;
            micSelect.appendChild(option);
        });

    } catch (error) {
        console.error('Failed to load mic devices:', error);
        micSelect.innerHTML = '';
        const option = document.createElement('option');
        option.value = '';
        option.textContent = 'Error loading devices';
        micSelect.appendChild(option);
    }
}

/**
 * Handle microphone device selection change
 */
export async function handleMicDeviceChange() {
    if (isMicTestRunning()) {
        await stopMicTest();
        await startMicTest();
    }
}

/**
 * Start the microphone test with real-time visualization
 */
export async function startMicTest() {
    const startBtn = document.getElementById('start-mic-test-btn');
    const stopBtn = document.getElementById('stop-mic-test-btn');
    const visualizer = document.getElementById('mic-test-visualizer');
    const statusDiv = document.getElementById('mic-test-status');
    const micSelect = document.getElementById('mic-device-select');

    const selectedDeviceId = micSelect.value;

    if (!selectedDeviceId) {
        updateMicTestStatus('error', '❌', 'No microphone selected');
        return;
    }

    const state = getMicTestState();

    try {
        // Request microphone access
        const constraints = {
            audio: {
                deviceId: { exact: selectedDeviceId },
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: false
            }
        };

        state.stream = await navigator.mediaDevices.getUserMedia(constraints);

        // Create audio context and analyser
        state.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        state.analyser = state.audioContext.createAnalyser();
        state.analyser.fftSize = MIC_TEST_FFT_SIZE;
        state.analyser.smoothingTimeConstant = AUDIO_SMOOTHING_TIME_CONSTANT;

        const source = state.audioContext.createMediaStreamSource(state.stream);
        source.connect(state.analyser);

        // Initialize audio data buffer
        state.audioData = new Uint8Array(state.analyser.frequencyBinCount);
        state.peakLevel = 0;
        state.isRunning = true;

        // Cache DOM elements for animation loop performance
        state.elements.volumeBar = document.getElementById('volume-bar');
        state.elements.volumePeak = document.getElementById('volume-peak');
        state.elements.peakLabel = document.getElementById('peak-label');
        state.elements.canvas = document.getElementById('waveform-canvas');
        if (state.elements.canvas) {
            state.elements.canvasCtx = state.elements.canvas.getContext('2d');
        }

        setMicTestState(state);

        // Update UI
        startBtn.classList.add('hidden');
        stopBtn.classList.remove('hidden');
        visualizer.classList.remove('hidden');
        statusDiv.classList.add('active');
        statusDiv.classList.remove('error');

        updateMicTestStatus('active', '🎤', 'Listening... Speak into your microphone');

        // Start visualization loop
        animateMicTest();

    } catch (error) {
        console.error('Failed to start mic test:', error);

        let errorMessage = 'Failed to access microphone';
        if (error.name === 'NotAllowedError') {
            errorMessage = 'Microphone access denied. Please allow microphone access.';
        } else if (error.name === 'NotFoundError') {
            errorMessage = 'No microphone found. Please connect a microphone.';
        } else if (error.name === 'NotReadableError') {
            errorMessage = 'Microphone is in use by another application.';
        }

        updateMicTestStatus('error', '❌', errorMessage);
        await stopMicTest();
    }
}

/**
 * Stop the microphone test
 */
export async function stopMicTest() {
    const startBtn = document.getElementById('start-mic-test-btn');
    const stopBtn = document.getElementById('stop-mic-test-btn');
    const visualizer = document.getElementById('mic-test-visualizer');

    const state = getMicTestState();
    state.isRunning = false;

    // Cancel animation
    if (state.animationId) {
        cancelAnimationFrame(state.animationId);
        state.animationId = null;
    }

    // Stop audio stream
    if (state.stream) {
        state.stream.getTracks().forEach(track => track.stop());
        state.stream = null;
    }

    // Close audio context
    if (state.audioContext) {
        try {
            await state.audioContext.close();
        } catch (_e) {
            // Ignore close errors
        }
        state.audioContext = null;
        state.analyser = null;
    }

    // Reset peak level
    state.peakLevel = 0;
    setMicTestState(state);

    // Update UI
    if (startBtn) startBtn.classList.remove('hidden');
    if (stopBtn) stopBtn.classList.add('hidden');
    if (visualizer) visualizer.classList.add('hidden');

    const statusDiv = document.getElementById('mic-test-status');
    if (statusDiv) {
        statusDiv.classList.remove('active');
        statusDiv.classList.remove('error');
    }
    updateMicTestStatus('', 'ℹ️', 'Click "Start Test" to begin microphone testing');
}

/**
 * Animation loop for mic test visualization
 */
function animateMicTest() {
    const state = getMicTestState();

    if (!state.isRunning || !state.analyser) {
        return;
    }

    // Get audio data
    state.analyser.getByteTimeDomainData(state.audioData);

    // Calculate RMS level
    let sum = 0;
    for (let i = 0; i < state.audioData.length; i++) {
        const value = (state.audioData[i] - AUDIO_LEVEL_NORMALIZATION) / AUDIO_LEVEL_NORMALIZATION;
        sum += value * value;
    }
    const rms = Math.sqrt(sum / state.audioData.length);
    const level = Math.min(1, rms * RMS_SCALE_FACTOR); // Scale for visibility

    // Update peak level with decay
    if (level > state.peakLevel) {
        state.peakLevel = level;
    } else {
        state.peakLevel = Math.max(level, state.peakLevel * PEAK_DECAY_RATE);
    }

    // Update volume bar using cached elements
    const { volumeBar, volumePeak, peakLabel } = state.elements;
    if (volumeBar && volumePeak && peakLabel) {
        volumeBar.style.width = `${level * 100}%`;
        volumePeak.style.left = `${state.peakLevel * 100}%`;
        peakLabel.textContent = `Peak: ${Math.round(state.peakLevel * 100)}%`;
    }

    // Draw waveform
    drawWaveform();

    // Schedule next frame
    state.animationId = requestAnimationFrame(animateMicTest);
    setMicTestState(state);
}

/**
 * Draw the audio waveform on canvas
 */
function drawWaveform() {
    const state = getMicTestState();
    const { canvas, canvasCtx } = state.elements;
    if (!canvas || !canvasCtx || !state.audioData) return;

    const width = canvas.width;
    const height = canvas.height;

    // Get theme colors from constants
    const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
    const colors = isDark ? WAVEFORM_COLORS.dark : WAVEFORM_COLORS.light;

    // Clear canvas
    canvasCtx.fillStyle = colors.background;
    canvasCtx.fillRect(0, 0, width, height);

    // Draw center line
    canvasCtx.beginPath();
    canvasCtx.strokeStyle = colors.centerLine;
    canvasCtx.lineWidth = CENTER_LINE_WIDTH;
    canvasCtx.moveTo(0, height / 2);
    canvasCtx.lineTo(width, height / 2);
    canvasCtx.stroke();

    // Draw waveform
    canvasCtx.beginPath();
    canvasCtx.strokeStyle = colors.line;
    canvasCtx.lineWidth = WAVEFORM_LINE_WIDTH;

    const sliceWidth = width / state.audioData.length;
    let x = 0;

    for (let i = 0; i < state.audioData.length; i++) {
        const v = state.audioData[i] / (AUDIO_LEVEL_NORMALIZATION * 1.0);
        const y = (v * height) / 2;

        if (i === 0) {
            canvasCtx.moveTo(x, y);
        } else {
            canvasCtx.lineTo(x, y);
        }

        x += sliceWidth;
    }

    canvasCtx.stroke();
}

/**
 * Update mic test status display
 * @param {string} statusState - State class name
 * @param {string} icon - Status icon
 * @param {string} message - Status message
 */
function updateMicTestStatus(statusState, icon, message) {
    const statusDiv = document.getElementById('mic-test-status');
    if (!statusDiv) return;

    const iconSpan = statusDiv.querySelector('.status-icon');
    const messageSpan = statusDiv.querySelector('.status-message');

    statusDiv.classList.remove('active', 'error');
    if (statusState) {
        statusDiv.classList.add(statusState);
    }

    if (iconSpan) iconSpan.textContent = icon;
    if (messageSpan) messageSpan.textContent = message;
}
