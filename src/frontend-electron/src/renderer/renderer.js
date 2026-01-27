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
// eslint-disable-next-line no-unused-vars
let currentDownload = null;  // Tracks ongoing download for potential cancellation

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
    document.getElementById('test-microphone-btn').addEventListener('click', testMicrophone);
    document.getElementById('save-audio-btn').addEventListener('click', saveAudioSettings);

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
    // Download progress updates
    window.voxtether.onDownloadProgress((data) => {
        updateDownloadProgress(data);
    });

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
        testMicrophone();
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
    // Audio devices are handled by the backend/NAudio
    // For now, just show a refresh notification
    showNotification('Audio devices refreshed', 'info');
}

async function testMicrophone() {
    const btn = document.getElementById('test-microphone-btn');
    const resultDiv = document.getElementById('test-result');
    const resultText = document.getElementById('test-result-text');

    btn.disabled = true;
    btn.textContent = '🔴 Recording...';
    resultDiv.classList.add('hidden');

    // Simulate a 2-second recording test
    // In a real implementation, this would trigger the backend
    try {
        updateStatus('Testing...', 'recording');
        await new Promise(resolve => setTimeout(resolve, 2000));

        // Mock result - in real implementation, call transcribe API
        resultText.textContent = '(Test recording - backend integration pending)';
        resultDiv.classList.remove('hidden');
        updateStatus('Ready', 'ready');
    } catch (error) {
        resultText.textContent = `Error: ${error.message}`;
        resultDiv.classList.remove('hidden');
        updateStatus('Error', 'error');
    } finally {
        btn.disabled = false;
        btn.textContent = '🎤 Test Recording';
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
            modelsGrid.innerHTML = '<div class="model-card"><div class="model-name">Backend not available</div><div class="model-description">Start the backend to manage models</div></div>';
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
            option.textContent = 'No models downloaded';
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

        // Update models grid
        modelsGrid.innerHTML = '';

        // Sort models by size
        const sortedModels = Object.values(MODEL_INFO);

        for (const modelInfo of sortedModels) {
            const apiModel = models.find(m => m.name === modelInfo.name) || {};
            const isDownloaded = apiModel.downloaded || false;
            const isActive = apiModel.name === currentModel;

            // Create card using DOM methods to prevent XSS
            const card = document.createElement('div');
            card.className = `model-card ${isActive ? 'active' : ''}`;

            const nameDiv = document.createElement('div');
            nameDiv.className = 'model-name';
            nameDiv.textContent = modelInfo.displayName;
            card.appendChild(nameDiv);

            const descDiv = document.createElement('div');
            descDiv.className = 'model-description';
            descDiv.textContent = modelInfo.description;
            card.appendChild(descDiv);

            const sizeDiv = document.createElement('div');
            sizeDiv.className = 'model-size';
            sizeDiv.textContent = `~${formatSize(modelInfo.sizeMb * 1024 * 1024)}`;
            card.appendChild(sizeDiv);

            const statusDiv = document.createElement('div');
            statusDiv.className = `model-status ${isDownloaded ? 'downloaded' : 'not-downloaded'}`;
            statusDiv.textContent = isDownloaded ? '✓ Downloaded' : '○ Not downloaded';
            card.appendChild(statusDiv);

            const actionsDiv = document.createElement('div');
            actionsDiv.className = 'model-actions';

            if (isDownloaded) {
                const loadBtn = document.createElement('button');
                loadBtn.className = 'btn btn-secondary btn-small';
                loadBtn.textContent = isActive ? '✓ Active' : 'Load';
                loadBtn.addEventListener('click', () => loadModel(modelInfo.name));
                actionsDiv.appendChild(loadBtn);

                const deleteBtn = document.createElement('button');
                deleteBtn.className = 'btn btn-danger btn-small';
                deleteBtn.textContent = 'Delete';
                deleteBtn.addEventListener('click', () => deleteModel(modelInfo.name));
                actionsDiv.appendChild(deleteBtn);
            } else {
                const downloadBtn = document.createElement('button');
                downloadBtn.className = 'btn btn-primary btn-small';
                downloadBtn.textContent = 'Download';
                downloadBtn.addEventListener('click', () => downloadModel(modelInfo.name));
                actionsDiv.appendChild(downloadBtn);
            }

            card.appendChild(actionsDiv);
            modelsGrid.appendChild(card);
        }
    } catch (error) {
        console.error('Failed to load models:', error);
        modelsGrid.innerHTML = '<div class="model-card"><div class="model-name">Error loading models</div></div>';
    }
}

async function downloadModel(modelName) {
    const progressDiv = document.getElementById('download-progress');
    const progressModelName = progressDiv.querySelector('.progress-model-name');
    const progressPercent = progressDiv.querySelector('.progress-percent');
    const progressFill = progressDiv.querySelector('.progress-fill');
    const progressDownloaded = progressDiv.querySelector('.progress-downloaded');
    const progressSpeed = progressDiv.querySelector('.progress-speed');

    progressModelName.textContent = `Downloading ${modelName}...`;
    progressPercent.textContent = '0%';
    progressFill.style.width = '0%';
    progressDownloaded.textContent = '0 MB';
    progressSpeed.textContent = '0 MB/s';
    progressDiv.classList.remove('hidden');

    currentDownload = modelName;

    try {
        const result = await window.voxtether.downloadModel(modelName);

        if (result.success) {
            showNotification(`Model ${modelName} downloaded successfully`, 'success');
            await loadModels();
        } else {
            showNotification(`Failed to download model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Download failed:', error);
        showNotification(`Download failed: ${error.message}`, 'error');
    } finally {
        progressDiv.classList.add('hidden');
        currentDownload = null;
    }
}

function updateDownloadProgress(data) {
    const progressDiv = document.getElementById('download-progress');

    if (progressDiv.classList.contains('hidden')) return;

    const progressPercent = progressDiv.querySelector('.progress-percent');
    const progressFill = progressDiv.querySelector('.progress-fill');
    const progressDownloaded = progressDiv.querySelector('.progress-downloaded');
    const progressSpeed = progressDiv.querySelector('.progress-speed');

    const percent = Math.round(data.progress * 100);
    progressPercent.textContent = `${percent}%`;
    progressFill.style.width = `${percent}%`;
    progressDownloaded.textContent = `${data.downloaded_mb.toFixed(1)} MB / ${data.total_mb.toFixed(1)} MB`;
    progressSpeed.textContent = `${data.speed_mbps.toFixed(1)} MB/s`;
}

async function loadModel(modelName) {
    try {
        const result = await window.voxtether.loadModel(modelName);
        if (result.success) {
            showNotification(`Model ${modelName} loaded`, 'success');
            await loadModels();
        } else {
            showNotification(`Failed to load model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to load model:', error);
        showNotification(`Failed to load model: ${error.message}`, 'error');
    }
}

async function deleteModel(modelName) {
    if (!confirm(`Are you sure you want to delete the ${modelName} model?`)) {
        return;
    }

    try {
        const result = await window.voxtether.deleteModel(modelName);
        if (result.success) {
            showNotification(`Model ${modelName} deleted`, 'success');
            await loadModels();
        } else {
            showNotification(`Failed to delete model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to delete model:', error);
        showNotification(`Failed to delete model: ${error.message}`, 'error');
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
