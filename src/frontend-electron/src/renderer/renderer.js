/**
 * VoxTether Electron - Renderer Script
 *
 * Handles all UI interactions and communicates with the main process
 * via the exposed voxtether API (preload.js).
 */

// Model information
const MODEL_INFO = {
    tiny: { name: 'tiny', displayName: 'Tiny', sizeMb: 75, description: 'Quick notes, low-resource systems' },
    base: { name: 'base', displayName: 'Base', sizeMb: 142, description: 'General use' },
    small: { name: 'small', displayName: 'Small', sizeMb: 466, description: 'Recommended for most users' },
    medium: { name: 'medium', displayName: 'Medium', sizeMb: 1500, description: 'When accuracy is important' },
    'large-v3': { name: 'large-v3', displayName: 'Large v3', sizeMb: 3000, description: 'When accuracy is critical' },
    'large-v3-turbo': { name: 'large-v3-turbo', displayName: 'Large v3 Turbo', sizeMb: 1600, description: 'Best balance of speed and accuracy' },
    'distil-large-v3': { name: 'distil-large-v3', displayName: 'Distil Large v3', sizeMb: 1100, description: 'Fast high-quality transcription' }
};

// Application state
let settings = {};
let isCapturingHotkey = false;

// Mic test state
let micTestState = {
    isRunning: false,
    stream: null,
    audioContext: null,
    analyser: null,
    animationId: null,
    peakLevel: 0,
    audioData: null,
    // Cached DOM elements for animation loop performance
    elements: {
        volumeBar: null,
        volumePeak: null,
        peakLabel: null,
        canvas: null,
        canvasCtx: null
    }
};

// ============================================================================
// Initialization
// ============================================================================

document.addEventListener('DOMContentLoaded', async () => {
    console.log('VoxTether renderer initializing...');

    // Load settings
    await loadSettings();

    // Initialize UI
    initializeNavigation();
    initializeEventListeners();
    applyTheme(settings.theme);

    // Load page data
    await loadAboutInfo();
    await loadModels();
    await checkDeviceInfo();
    await loadMicDevices();

    // Set up IPC event listeners
    setupIPCListeners();

    console.log('VoxTether renderer ready');
});

// ============================================================================
// Settings Management
// ============================================================================

async function loadSettings() {
    try {
        settings = await window.voxtether.getSettings();
        applySettingsToUI();
    } catch (error) {
        console.error('Failed to load settings:', error);
        showNotification('Failed to load settings', 'error');
    }
}

function applySettingsToUI() {
    // General settings
    document.getElementById('hotkey-input').value = settings.hotkey || 'Ctrl+Shift+Space';
    document.getElementById('language-select').value = settings.language || 'auto';
    document.getElementById('output-mode-select').value = settings.outputMode || 'ClipboardAndPaste';
    document.getElementById('notifications-toggle').checked = settings.showNotifications !== false;
    document.getElementById('recording-indicator-toggle').checked = settings.showRecordingIndicator !== false;
    document.getElementById('start-with-windows-toggle').checked = settings.startWithWindows === true;
    document.getElementById('start-minimized-toggle').checked = settings.startMinimized !== false;
    document.getElementById('theme-select').value = settings.theme || 'system';

    // Audio settings
    document.getElementById('clipboard-delay-input').value = settings.clipboardDelayMs || 50;
    document.getElementById('audio-device-select').value = String(settings.audioDeviceId || -1);
}

async function saveSettings(newSettings) {
    try {
        Object.assign(settings, newSettings);
        const success = await window.voxtether.saveSettings(settings);
        if (success) {
            showNotification('Settings saved successfully', 'success');
            applyTheme(settings.theme);
        } else {
            showNotification('Failed to save settings', 'error');
        }
        return success;
    } catch (error) {
        console.error('Failed to save settings:', error);
        showNotification('Failed to save settings', 'error');
        return false;
    }
}

// ============================================================================
// Navigation
// ============================================================================

function initializeNavigation() {
    const navItems = document.querySelectorAll('.nav-item');

    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const page = item.dataset.page;
            navigateTo(page);
        });
    });
}

function navigateTo(pageName) {
    // Stop mic test if leaving audio page and it's running
    if (micTestState.isRunning) {
        stopMicTest();
    }

    // Update nav items
    document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.toggle('active', item.dataset.page === pageName);
    });

    // Update pages
    document.querySelectorAll('.page').forEach(page => {
        page.classList.toggle('active', page.id === `page-${pageName}`);
    });
}

// ============================================================================
// Event Listeners
// ============================================================================

function initializeEventListeners() {
    // General settings
    document.getElementById('capture-hotkey-btn').addEventListener('click', startHotkeyCapture);
    document.getElementById('hotkey-input').addEventListener('click', startHotkeyCapture);
    document.getElementById('save-general-btn').addEventListener('click', saveGeneralSettings);

    // Audio settings
    document.getElementById('refresh-devices-btn').addEventListener('click', refreshAudioDevices);
    document.getElementById('save-audio-btn').addEventListener('click', saveAudioSettings);

    // Mic test controls
    document.getElementById('start-mic-test-btn').addEventListener('click', startMicTest);
    document.getElementById('stop-mic-test-btn').addEventListener('click', stopMicTest);
    document.getElementById('mic-device-select').addEventListener('change', handleMicDeviceChange);

    // Transcribe page
    document.getElementById('select-audio-file-btn').addEventListener('click', selectAudioFile);
    document.getElementById('select-output-folder-btn').addEventListener('click', selectOutputFolder);
    document.getElementById('clear-output-folder-btn').addEventListener('click', clearOutputFolder);
    document.getElementById('transcribe-file-btn').addEventListener('click', transcribeSelectedFile);
    document.getElementById('copy-transcription-btn').addEventListener('click', copyTranscription);
    document.getElementById('save-transcription-btn').addEventListener('click', saveTranscriptionToFile);
    document.getElementById('audio-file-path').addEventListener('input', updateTranscribeButton);

    // About page
    document.getElementById('github-link').addEventListener('click', () => {
        window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether');
    });
    document.getElementById('docs-link').addEventListener('click', () => {
        window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether/tree/main/docs');
    });
    document.getElementById('releases-link').addEventListener('click', () => {
        window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether/releases');
    });

    // Theme change
    document.getElementById('theme-select').addEventListener('change', (e) => {
        applyTheme(e.target.value);
    });

    // Global keyboard listener for hotkey capture
    document.addEventListener('keydown', handleHotkeyCapture);
}

function setupIPCListeners() {
    // Recording state changes
    window.voxtether.onRecordingStateChanged((isRecording) => {
        updateStatus(isRecording ? 'Recording...' : 'Ready', isRecording ? 'recording' : 'ready');
    });

    // Status updates
    window.voxtether.onStatusChanged((status) => {
        updateStatus(status);
    });

    // Test microphone request from tray
    window.voxtether.onTestMicrophone(() => {
        // Navigate to audio page and start mic test
        navigateTo('audio');
        startMicTest();
    });
}

// ============================================================================
// Hotkey Capture
// ============================================================================

function startHotkeyCapture() {
    isCapturingHotkey = true;
    const input = document.getElementById('hotkey-input');
    input.value = 'Press hotkey combination...';
    input.classList.add('capturing');
    document.getElementById('capture-hotkey-btn').textContent = 'Listening...';
}

function handleHotkeyCapture(event) {
    if (!isCapturingHotkey) return;

    event.preventDefault();
    event.stopPropagation();

    // Build hotkey string
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');

    // Get key name
    let key = event.key;
    if (key === ' ') key = 'Space';
    else if (key.length === 1) key = key.toUpperCase();
    else if (key.startsWith('Arrow')) key = key.replace('Arrow', '');

    // Only complete if we have modifiers + a non-modifier key
    if (parts.length > 0 && !['Control', 'Alt', 'Shift', 'Meta'].includes(key)) {
        parts.push(key);
        const hotkey = parts.join('+');

        document.getElementById('hotkey-input').value = hotkey;
        stopHotkeyCapture();
    }
}

function stopHotkeyCapture() {
    isCapturingHotkey = false;
    const input = document.getElementById('hotkey-input');
    input.classList.remove('capturing');
    document.getElementById('capture-hotkey-btn').textContent = 'Capture';
}

// ============================================================================
// Settings Pages
// ============================================================================

async function saveGeneralSettings() {
    const newSettings = {
        hotkey: document.getElementById('hotkey-input').value,
        language: document.getElementById('language-select').value,
        outputMode: document.getElementById('output-mode-select').value,
        showNotifications: document.getElementById('notifications-toggle').checked,
        showRecordingIndicator: document.getElementById('recording-indicator-toggle').checked,
        startWithWindows: document.getElementById('start-with-windows-toggle').checked,
        startMinimized: document.getElementById('start-minimized-toggle').checked,
        theme: document.getElementById('theme-select').value
    };

    await saveSettings(newSettings);
}

async function saveAudioSettings() {
    const newSettings = {
        audioDeviceId: parseInt(document.getElementById('audio-device-select').value),
        clipboardDelayMs: parseInt(document.getElementById('clipboard-delay-input').value)
    };

    await saveSettings(newSettings);
}

async function refreshAudioDevices() {
    await loadMicDevices();
    showNotification('Audio devices refreshed', 'info');
}

// ============================================================================
// Microphone Test (Client-side using Web Audio API)
// ============================================================================

/**
 * Load available microphone devices using the MediaDevices API
 */
async function loadMicDevices() {
    const micSelect = document.getElementById('mic-device-select');

    try {
        // Request permission to access audio devices
        // This is needed to get device labels
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            // Stop the stream immediately, we just needed permission
            stream.getTracks().forEach(track => track.stop());
        } catch (_e) {
            // Permission denied or no devices available - continue anyway
        }

        const devices = await navigator.mediaDevices.enumerateDevices();
        const audioInputs = devices.filter(device => device.kind === 'audioinput');

        // Clear existing options
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
async function handleMicDeviceChange() {
    // If mic test is running, restart with new device
    if (micTestState.isRunning) {
        await stopMicTest();
        await startMicTest();
    }
}

/**
 * Start the microphone test with real-time visualization
 */
async function startMicTest() {
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

        micTestState.stream = await navigator.mediaDevices.getUserMedia(constraints);

        // Create audio context and analyser
        micTestState.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        micTestState.analyser = micTestState.audioContext.createAnalyser();
        micTestState.analyser.fftSize = 2048;
        micTestState.analyser.smoothingTimeConstant = 0.8;

        const source = micTestState.audioContext.createMediaStreamSource(micTestState.stream);
        source.connect(micTestState.analyser);

        // Initialize audio data buffer
        micTestState.audioData = new Uint8Array(micTestState.analyser.frequencyBinCount);
        micTestState.peakLevel = 0;
        micTestState.isRunning = true;

        // Cache DOM elements for animation loop performance
        micTestState.elements.volumeBar = document.getElementById('volume-bar');
        micTestState.elements.volumePeak = document.getElementById('volume-peak');
        micTestState.elements.peakLabel = document.getElementById('peak-label');
        micTestState.elements.canvas = document.getElementById('waveform-canvas');
        if (micTestState.elements.canvas) {
            micTestState.elements.canvasCtx = micTestState.elements.canvas.getContext('2d');
        }

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
async function stopMicTest() {
    const startBtn = document.getElementById('start-mic-test-btn');
    const stopBtn = document.getElementById('stop-mic-test-btn');
    const visualizer = document.getElementById('mic-test-visualizer');

    micTestState.isRunning = false;

    // Cancel animation
    if (micTestState.animationId) {
        cancelAnimationFrame(micTestState.animationId);
        micTestState.animationId = null;
    }

    // Stop audio stream
    if (micTestState.stream) {
        micTestState.stream.getTracks().forEach(track => track.stop());
        micTestState.stream = null;
    }

    // Close audio context
    if (micTestState.audioContext) {
        try {
            await micTestState.audioContext.close();
        } catch (_e) {
            // Ignore close errors
        }
        micTestState.audioContext = null;
        micTestState.analyser = null;
    }

    // Reset peak level
    micTestState.peakLevel = 0;

    // Update UI
    startBtn.classList.remove('hidden');
    stopBtn.classList.add('hidden');
    visualizer.classList.add('hidden');

    const statusDiv = document.getElementById('mic-test-status');
    statusDiv.classList.remove('active');
    statusDiv.classList.remove('error');
    updateMicTestStatus('', 'ℹ️', 'Click "Start Test" to begin microphone testing');
}

/**
 * Animation loop for mic test visualization
 */
function animateMicTest() {
    if (!micTestState.isRunning || !micTestState.analyser) {
        return;
    }

    // Get audio data
    micTestState.analyser.getByteTimeDomainData(micTestState.audioData);

    // Calculate RMS level
    let sum = 0;
    for (let i = 0; i < micTestState.audioData.length; i++) {
        const value = (micTestState.audioData[i] - 128) / 128;
        sum += value * value;
    }
    const rms = Math.sqrt(sum / micTestState.audioData.length);
    const level = Math.min(1, rms * 3); // Scale for visibility

    // Update peak level with decay
    if (level > micTestState.peakLevel) {
        micTestState.peakLevel = level;
    } else {
        micTestState.peakLevel = Math.max(level, micTestState.peakLevel * 0.98);
    }

    // Update volume bar using cached elements
    const { volumeBar, volumePeak, peakLabel } = micTestState.elements;
    if (volumeBar && volumePeak && peakLabel) {
        volumeBar.style.width = `${level * 100}%`;
        volumePeak.style.left = `${micTestState.peakLevel * 100}%`;
        peakLabel.textContent = `Peak: ${Math.round(micTestState.peakLevel * 100)}%`;
    }

    // Draw waveform
    drawWaveform();

    // Schedule next frame
    micTestState.animationId = requestAnimationFrame(animateMicTest);
}

/**
 * Draw the audio waveform on canvas
 */
function drawWaveform() {
    const { canvas, canvasCtx } = micTestState.elements;
    if (!canvas || !canvasCtx || !micTestState.audioData) return;

    const width = canvas.width;
    const height = canvas.height;

    // Get theme colors
    const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
    const bgColor = isDark ? '#202020' : '#f9f9f9';
    const lineColor = isDark ? '#4682B4' : '#0078d4';
    const centerLineColor = isDark ? '#404040' : '#e0e0e0';

    // Clear canvas
    canvasCtx.fillStyle = bgColor;
    canvasCtx.fillRect(0, 0, width, height);

    // Draw center line
    canvasCtx.beginPath();
    canvasCtx.strokeStyle = centerLineColor;
    canvasCtx.lineWidth = 1;
    canvasCtx.moveTo(0, height / 2);
    canvasCtx.lineTo(width, height / 2);
    canvasCtx.stroke();

    // Draw waveform
    canvasCtx.beginPath();
    canvasCtx.strokeStyle = lineColor;
    canvasCtx.lineWidth = 2;

    const sliceWidth = width / micTestState.audioData.length;
    let x = 0;

    for (let i = 0; i < micTestState.audioData.length; i++) {
        const v = micTestState.audioData[i] / 128.0;
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
 */
function updateMicTestStatus(state, icon, message) {
    const statusDiv = document.getElementById('mic-test-status');
    if (!statusDiv) return;

    const iconSpan = statusDiv.querySelector('.status-icon');
    const messageSpan = statusDiv.querySelector('.status-message');

    statusDiv.classList.remove('active', 'error');
    if (state) {
        statusDiv.classList.add(state);
    }

    if (iconSpan) iconSpan.textContent = icon;
    if (messageSpan) messageSpan.textContent = message;
}

// ============================================================================
// Transcribe Page
// ============================================================================

// Transcribe state
let transcribeState = {
    audioFilePath: '',
    outputFolderPath: '',
    lastTranscription: ''
};

/**
 * Select an audio file to transcribe
 */
async function selectAudioFile() {
    try {
        const result = await window.voxtether.selectAudioFile();

        if (result.success && result.filePath) {
            transcribeState.audioFilePath = result.filePath;
            document.getElementById('audio-file-path').value = result.filePath;
            updateTranscribeButton();
        }
    } catch (error) {
        console.error('Failed to select audio file:', error);
        showNotification('Failed to select audio file', 'error');
    }
}

/**
 * Select an output folder for saving transcripts
 */
async function selectOutputFolder() {
    try {
        const result = await window.voxtether.selectOutputFolder();

        if (result.success && result.folderPath) {
            transcribeState.outputFolderPath = result.folderPath;
            document.getElementById('output-folder-path').value = result.folderPath;
        }
    } catch (error) {
        console.error('Failed to select output folder:', error);
        showNotification('Failed to select output folder', 'error');
    }
}

/**
 * Clear the output folder selection
 */
function clearOutputFolder() {
    transcribeState.outputFolderPath = '';
    document.getElementById('output-folder-path').value = '';
}

/**
 * Update the transcribe button state based on file selection
 */
function updateTranscribeButton() {
    const transcribeBtn = document.getElementById('transcribe-file-btn');
    const hasFile = transcribeState.audioFilePath || document.getElementById('audio-file-path').value;
    transcribeBtn.disabled = !hasFile;
}

/**
 * Transcribe the selected audio file
 */
async function transcribeSelectedFile() {
    const audioFilePath = transcribeState.audioFilePath;
    const language = document.getElementById('transcribe-language-select').value;

    if (!audioFilePath) {
        showNotification('Please select an audio file first', 'error');
        return;
    }

    // Show progress, hide result
    const progressDiv = document.getElementById('transcription-progress');
    const resultDiv = document.getElementById('transcription-result');
    const transcribeBtn = document.getElementById('transcribe-file-btn');

    progressDiv.classList.remove('hidden');
    resultDiv.classList.add('hidden');
    transcribeBtn.disabled = true;

    try {
        const result = await window.voxtether.transcribe(audioFilePath, language);

        if (result.success && result.data) {
            const transcription = result.data;
            transcribeState.lastTranscription = transcription.text || '';

            // Update result display
            document.getElementById('transcription-text').value = transcription.text || '';

            // Update meta info
            const metaDiv = document.getElementById('result-meta');
            const duration = transcription.duration ? transcription.duration.toFixed(1) : 'N/A';
            const detectedLang = transcription.language || 'Unknown';
            metaDiv.textContent = `Duration: ${duration}s | Language: ${detectedLang}`;

            // Show result
            resultDiv.classList.remove('hidden');

            // Auto-save if enabled
            await handleAutoSave(audioFilePath, transcription.text);

            showNotification('Transcription completed successfully', 'success');
        } else {
            const errorMsg = result.error || result.data?.error || 'Unknown error';
            showNotification(`Transcription failed: ${errorMsg}`, 'error');
        }
    } catch (error) {
        console.error('Transcription failed:', error);
        showNotification(`Transcription failed: ${error.message}`, 'error');
    } finally {
        progressDiv.classList.add('hidden');
        transcribeBtn.disabled = false;
    }
}

/**
 * Handle automatic saving of transcript and audio copy
 */
async function handleAutoSave(audioFilePath, transcriptText) {
    const saveTranscript = document.getElementById('save-transcript-toggle').checked;
    const saveAudioCopy = document.getElementById('save-audio-copy-toggle').checked;

    if (!saveTranscript && !saveAudioCopy) {
        return;
    }

    // Determine output folder - use selected folder or same as audio file
    let outputFolder = transcribeState.outputFolderPath;
    if (!outputFolder) {
        // Get directory of audio file
        const lastSlash = Math.max(audioFilePath.lastIndexOf('/'), audioFilePath.lastIndexOf('\\'));
        outputFolder = audioFilePath.substring(0, lastSlash);
    }

    // Get base filename without extension
    const fileName = audioFilePath.substring(audioFilePath.lastIndexOf('/') + 1).replace(/\\/g, '/');
    const lastDot = fileName.lastIndexOf('.');
    const baseName = lastDot > 0 ? fileName.substring(0, lastDot) : fileName;

    try {
        // Save transcript
        if (saveTranscript && transcriptText) {
            const transcriptPath = `${outputFolder}/${baseName}.txt`;
            const result = await window.voxtether.saveTranscript(transcriptPath, transcriptText);
            if (!result.success) {
                console.warn('Failed to save transcript:', result.error);
            }
        }

        // Copy audio file
        if (saveAudioCopy && transcribeState.outputFolderPath) {
            const result = await window.voxtether.copyFile(audioFilePath, transcribeState.outputFolderPath);
            if (!result.success) {
                console.warn('Failed to copy audio file:', result.error);
            }
        }
    } catch (error) {
        console.error('Auto-save failed:', error);
    }
}

/**
 * Copy transcription text to clipboard
 */
async function copyTranscription() {
    const text = document.getElementById('transcription-text').value;

    if (!text) {
        showNotification('No transcription to copy', 'error');
        return;
    }

    try {
        await window.voxtether.copyToClipboard(text);
        showNotification('Transcription copied to clipboard', 'success');
    } catch (error) {
        console.error('Failed to copy to clipboard:', error);
        showNotification('Failed to copy to clipboard', 'error');
    }
}

/**
 * Save transcription to a file manually
 */
async function saveTranscriptionToFile() {
    const text = document.getElementById('transcription-text').value;

    if (!text) {
        showNotification('No transcription to save', 'error');
        return;
    }

    // Determine output folder
    let outputFolder = transcribeState.outputFolderPath;
    if (!outputFolder && transcribeState.audioFilePath) {
        const lastSlash = Math.max(transcribeState.audioFilePath.lastIndexOf('/'), transcribeState.audioFilePath.lastIndexOf('\\'));
        outputFolder = transcribeState.audioFilePath.substring(0, lastSlash);
    }

    if (!outputFolder) {
        showNotification('Please select an output folder first', 'error');
        return;
    }

    // Generate filename
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
    const transcriptPath = `${outputFolder}/transcription-${timestamp}.txt`;

    try {
        const result = await window.voxtether.saveTranscript(transcriptPath, text);
        if (result.success) {
            showNotification('Transcription saved successfully', 'success');
        } else {
            showNotification(`Failed to save: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to save transcription:', error);
        showNotification(`Failed to save: ${error.message}`, 'error');
    }
}

// ============================================================================
// Models Page
// ============================================================================

async function loadModels() {
    const modelsGrid = document.getElementById('models-grid');
    const modelSelect = document.getElementById('model-select');

    try {
        const result = await window.voxtether.getModels();

        if (!result.success) {
            // Clear and show error using DOM methods
            modelsGrid.innerHTML = '';
            const errorCard = document.createElement('div');
            errorCard.className = 'model-card';
            const errorName = document.createElement('div');
            errorName.className = 'model-name';
            errorName.textContent = 'Backend not available';
            const errorDesc = document.createElement('div');
            errorDesc.className = 'model-description';
            errorDesc.textContent = 'Start the backend server to view models';
            const errorHint = document.createElement('div');
            errorHint.className = 'model-description';
            errorHint.style.marginTop = '10px';
            errorHint.textContent = 'Run: python cli.py serve';
            errorCard.appendChild(errorName);
            errorCard.appendChild(errorDesc);
            errorCard.appendChild(errorHint);
            modelsGrid.appendChild(errorCard);
            return;
        }

        const models = result.data.models || [];
        const currentModel = result.data.current_model;

        // Update model select dropdown using DOM methods to prevent XSS
        modelSelect.innerHTML = '';
        const downloadedModels = models.filter(m => m.downloaded);
        if (downloadedModels.length === 0) {
            const option = document.createElement('option');
            option.value = '';
            option.textContent = 'No models available - use CLI to download';
            modelSelect.appendChild(option);
        } else {
            for (const m of downloadedModels) {
                const option = document.createElement('option');
                option.value = m.name;
                option.textContent = m.display_name;
                if (m.name === currentModel) {
                    option.selected = true;
                }
                modelSelect.appendChild(option);
            }
        }

        // Update models grid - only show downloaded models
        modelsGrid.innerHTML = '';

        // Filter to only downloaded models
        const availableModels = models.filter(m => m.downloaded);

        if (availableModels.length === 0) {
            // Show message about using CLI
            const noModelsCard = document.createElement('div');
            noModelsCard.className = 'model-card';

            const titleDiv = document.createElement('div');
            titleDiv.className = 'model-name';
            titleDiv.textContent = 'No Models Downloaded';
            noModelsCard.appendChild(titleDiv);

            const msgDiv = document.createElement('div');
            msgDiv.className = 'model-description';
            msgDiv.textContent = 'Use the backend CLI to download models:';
            noModelsCard.appendChild(msgDiv);

            const cmdDiv = document.createElement('div');
            cmdDiv.className = 'model-size';
            cmdDiv.style.fontFamily = 'monospace';
            cmdDiv.style.marginTop = '10px';
            cmdDiv.textContent = 'python cli.py download small';
            noModelsCard.appendChild(cmdDiv);

            modelsGrid.appendChild(noModelsCard);
            return;
        }

        for (const model of availableModels) {
            const modelInfo = MODEL_INFO[model.name] || { displayName: model.display_name, sizeMb: model.size_mb, description: model.description };
            const isActive = model.name === currentModel;

            // Create card using DOM methods to prevent XSS
            const card = document.createElement('div');
            card.className = `model-card ${isActive ? 'active' : ''}`;

            const nameDiv = document.createElement('div');
            nameDiv.className = 'model-name';
            nameDiv.textContent = modelInfo.displayName || model.display_name;
            card.appendChild(nameDiv);

            const descDiv = document.createElement('div');
            descDiv.className = 'model-description';
            descDiv.textContent = modelInfo.description || model.description;
            card.appendChild(descDiv);

            const sizeDiv = document.createElement('div');
            sizeDiv.className = 'model-size';
            sizeDiv.textContent = `~${formatSize((modelInfo.sizeMb || model.size_mb) * 1024 * 1024)}`;
            card.appendChild(sizeDiv);

            const statusDiv = document.createElement('div');
            statusDiv.className = 'model-status downloaded';
            statusDiv.textContent = isActive ? '✓ Active' : '✓ Downloaded';
            card.appendChild(statusDiv);

            const actionsDiv = document.createElement('div');
            actionsDiv.className = 'model-actions';

            if (!isActive) {
                const loadBtn = document.createElement('button');
                loadBtn.className = 'btn btn-primary btn-small';
                loadBtn.textContent = 'Load Model';
                loadBtn.addEventListener('click', () => loadModel(model.name));
                actionsDiv.appendChild(loadBtn);
            } else {
                const activeSpan = document.createElement('span');
                activeSpan.className = 'btn btn-secondary btn-small';
                activeSpan.style.opacity = '0.7';
                activeSpan.textContent = '✓ Currently Active';
                actionsDiv.appendChild(activeSpan);
            }

            card.appendChild(actionsDiv);
            modelsGrid.appendChild(card);
        }
    } catch (error) {
        console.error('Failed to load models:', error);
        // Clear and show error using DOM methods
        modelsGrid.innerHTML = '';
        const errorCard = document.createElement('div');
        errorCard.className = 'model-card';
        const errorName = document.createElement('div');
        errorName.className = 'model-name';
        errorName.textContent = 'Error loading models';
        errorCard.appendChild(errorName);
        modelsGrid.appendChild(errorCard);
    }
}

async function loadModel(modelName) {
    try {
        showNotification(`Loading model ${modelName}...`, 'info');
        const result = await window.voxtether.loadModel(modelName);
        if (result.success) {
            showNotification(`Model ${modelName} loaded successfully`, 'success');
            await loadModels();
        } else {
            showNotification(`Failed to load model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to load model:', error);
        showNotification(`Failed to load model: ${error.message}`, 'error');
    }
}

async function checkDeviceInfo() {
    const deviceInfo = document.getElementById('device-info');
    const deviceText = deviceInfo.querySelector('.device-text');
    const deviceIcon = deviceInfo.querySelector('.device-icon');

    try {
        const result = await window.voxtether.getDevices();

        if (result.success && result.data) {
            const data = result.data;
            if (data.cuda_available) {
                deviceIcon.textContent = '🎮';
                deviceText.textContent = `GPU: ${data.device_name || 'NVIDIA'} (CUDA ${data.cuda_version || ''})`;
            } else {
                deviceIcon.textContent = '💻';
                deviceText.textContent = 'CPU Mode (No CUDA GPU detected)';
            }
        } else {
            deviceIcon.textContent = '⚠️';
            deviceText.textContent = 'Backend not available';
        }
    } catch (_error) {
        deviceIcon.textContent = '⚠️';
        deviceText.textContent = 'Could not detect device';
    }
}

// ============================================================================
// About Page
// ============================================================================

async function loadAboutInfo() {
    try {
        const appInfo = await window.voxtether.getAppInfo();

        document.getElementById('app-version').textContent = `Version ${appInfo.version}`;
        document.getElementById('platform-info').textContent = window.platform.isWindows ? 'Windows' :
            window.platform.isMac ? 'macOS' : 'Linux';
        document.getElementById('electron-version').textContent = process.versions?.electron || '-';

        const dataPath = document.getElementById('data-path');
        dataPath.textContent = appInfo.userDataPath;
        dataPath.addEventListener('click', () => window.voxtether.openPath(appInfo.userDataPath));

        const modelsPath = document.getElementById('models-path');
        modelsPath.textContent = appInfo.modelsPath;
        modelsPath.addEventListener('click', () => window.voxtether.openPath(appInfo.modelsPath));
    } catch (error) {
        console.error('Failed to load app info:', error);
    }
}

// ============================================================================
// Theme Management
// ============================================================================

function applyTheme(theme) {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

    if (theme === 'dark' || (theme === 'system' && prefersDark)) {
        document.documentElement.setAttribute('data-theme', 'dark');
    } else {
        document.documentElement.removeAttribute('data-theme');
    }
}

// Listen for system theme changes
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (settings.theme === 'system') {
        applyTheme('system');
    }
});

// ============================================================================
// Status Updates
// ============================================================================

function updateStatus(text, state = 'ready') {
    const statusIndicator = document.getElementById('status-indicator');
    const statusDot = statusIndicator.querySelector('.status-dot');
    const statusText = statusIndicator.querySelector('.status-text');

    statusText.textContent = text;
    statusDot.className = 'status-dot ' + state;
}

// ============================================================================
// Utilities
// ============================================================================

function formatSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}

function showNotification(message, type = 'info') {
    // For now, use console and native notification
    console.log(`[${type.toUpperCase()}] ${message}`);

    // Could add a toast notification system here
    // For now, using alert for important messages
    if (type === 'error') {
        alert(message);
    }
}

// Note: Model actions are now handled via addEventListener, not global functions
