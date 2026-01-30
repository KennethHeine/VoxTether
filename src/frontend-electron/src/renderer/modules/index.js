/**
 * VoxTether Electron - Renderer Entry Point (Modular)
 *
 * This module imports and orchestrates all feature modules for the renderer.
 */

// State management
import { getSettings } from './state.js';

// Theme and UI
import { applyTheme } from './theme.js';
import { updateStatus } from './status.js';

// Navigation
import { initializeNavigation, navigateTo } from './navigation.js';

// Settings
import {
    loadSettings,
    saveGeneralSettings,
    saveAudioSettings,
    selectRecordingFolder,
    clearRecordingFolder
} from './settings.js';

// Hotkey capture
import {
    startWindowToggleHotkeyCapture,
    startToggleRecordingHotkeyCapture,
    handleHotkeyCapture
} from './hotkey.js';

// Recording
import {
    startTestRecording,
    stopTestRecording,
    updateTestRecordingUI,
    handleStartRecording,
    handleStopRecording,
    closePreviewModal,
    previewCopyOnly,
    previewInsert
} from './recording/index.js';

// Mic test
import {
    loadMicDevices,
    startMicTest,
    stopMicTest,
    handleMicDeviceChange
} from './mictest.js';

// Transcribe
import {
    selectAudioFile,
    selectOutputFolder,
    clearOutputFolder,
    transcribeSelectedFile,
    copyTranscription,
    saveTranscriptionToFile,
    updateTranscribeButton
} from './transcribe.js';

// History
import {
    loadHistory,
    filterHistory,
    exportHistory,
    clearHistory
} from './history.js';

// Statistics
import {
    loadStatistics,
    resetStatistics
} from './statistics.js';

// Models
import {
    loadModels,
    checkDeviceInfo,
    initializeDownloadListener
} from './models.js';

// About
import {
    loadAboutInfo,
    initializeAboutListeners
} from './about.js';

// Audio device detection
import {
    setupAudioDeviceDetection,
    refreshAudioDevices
} from './audio.js';

// Auto-updater
import {
    checkForUpdates,
    showUpdateNotification,
    showUpdateReadyNotification,
    handleUpdateDownloadProgress
} from './updater.js';

// ============================================================================
// Event Listener Setup
// ============================================================================

/**
 * Initialize all event listeners
 */
function initializeEventListeners() {
    // General settings - window toggle hotkey
    const captureWindowToggleBtn = document.getElementById('capture-window-toggle-hotkey-btn');
    if (captureWindowToggleBtn) captureWindowToggleBtn.addEventListener('click', startWindowToggleHotkeyCapture);

    const windowToggleInput = document.getElementById('window-toggle-hotkey-input');
    if (windowToggleInput) windowToggleInput.addEventListener('click', startWindowToggleHotkeyCapture);

    const captureToggleRecordingBtn = document.getElementById('capture-toggle-recording-hotkey-btn');
    if (captureToggleRecordingBtn) captureToggleRecordingBtn.addEventListener('click', startToggleRecordingHotkeyCapture);

    const toggleRecordingInput = document.getElementById('toggle-recording-hotkey-input');
    if (toggleRecordingInput) toggleRecordingInput.addEventListener('click', startToggleRecordingHotkeyCapture);

    const saveGeneralBtn = document.getElementById('save-general-btn');
    if (saveGeneralBtn) saveGeneralBtn.addEventListener('click', saveGeneralSettings);

    // Recording output folder
    const selectRecordingFolderBtn = document.getElementById('select-recording-folder-btn');
    if (selectRecordingFolderBtn) selectRecordingFolderBtn.addEventListener('click', selectRecordingFolder);

    const clearRecordingFolderBtn = document.getElementById('clear-recording-folder-btn');
    if (clearRecordingFolderBtn) clearRecordingFolderBtn.addEventListener('click', clearRecordingFolder);

    // Test recording buttons
    const startTestRecordingBtn = document.getElementById('start-test-recording-btn');
    if (startTestRecordingBtn) startTestRecordingBtn.addEventListener('click', startTestRecording);

    const stopTestRecordingBtn = document.getElementById('stop-test-recording-btn');
    if (stopTestRecordingBtn) stopTestRecordingBtn.addEventListener('click', stopTestRecording);

    // Audio settings
    const refreshDevicesBtn = document.getElementById('refresh-devices-btn');
    if (refreshDevicesBtn) refreshDevicesBtn.addEventListener('click', refreshAudioDevices);

    const saveAudioBtn = document.getElementById('save-audio-btn');
    if (saveAudioBtn) saveAudioBtn.addEventListener('click', saveAudioSettings);

    // Mic test controls
    const startMicTestBtn = document.getElementById('start-mic-test-btn');
    if (startMicTestBtn) startMicTestBtn.addEventListener('click', startMicTest);

    const stopMicTestBtn = document.getElementById('stop-mic-test-btn');
    if (stopMicTestBtn) stopMicTestBtn.addEventListener('click', stopMicTest);

    const micDeviceSelect = document.getElementById('mic-device-select');
    if (micDeviceSelect) micDeviceSelect.addEventListener('change', handleMicDeviceChange);

    // Transcribe page
    const selectAudioFileBtn = document.getElementById('select-audio-file-btn');
    if (selectAudioFileBtn) selectAudioFileBtn.addEventListener('click', selectAudioFile);

    const selectOutputFolderBtn = document.getElementById('select-output-folder-btn');
    if (selectOutputFolderBtn) selectOutputFolderBtn.addEventListener('click', selectOutputFolder);

    const clearOutputFolderBtn = document.getElementById('clear-output-folder-btn');
    if (clearOutputFolderBtn) clearOutputFolderBtn.addEventListener('click', clearOutputFolder);

    const transcribeFileBtn = document.getElementById('transcribe-file-btn');
    if (transcribeFileBtn) transcribeFileBtn.addEventListener('click', transcribeSelectedFile);

    const copyTranscriptionBtn = document.getElementById('copy-transcription-btn');
    if (copyTranscriptionBtn) copyTranscriptionBtn.addEventListener('click', copyTranscription);

    const saveTranscriptionBtn = document.getElementById('save-transcription-btn');
    if (saveTranscriptionBtn) saveTranscriptionBtn.addEventListener('click', saveTranscriptionToFile);

    const audioFilePathInput = document.getElementById('audio-file-path');
    if (audioFilePathInput) audioFilePathInput.addEventListener('input', updateTranscribeButton);

    // History page
    const historySearchInput = document.getElementById('history-search');
    if (historySearchInput) historySearchInput.addEventListener('input', filterHistory);

    const exportHistoryBtn = document.getElementById('export-history-btn');
    if (exportHistoryBtn) exportHistoryBtn.addEventListener('click', exportHistory);

    const clearHistoryBtn = document.getElementById('clear-history-btn');
    if (clearHistoryBtn) clearHistoryBtn.addEventListener('click', clearHistory);

    // About page
    initializeAboutListeners();

    const checkUpdatesBtn = document.getElementById('check-updates-btn');
    if (checkUpdatesBtn) checkUpdatesBtn.addEventListener('click', checkForUpdates);

    // Theme change
    const themeSelect = document.getElementById('theme-select');
    if (themeSelect) {
        themeSelect.addEventListener('change', (e) => {
            applyTheme(e.target.value);
        });
    }

    // Statistics reset
    const resetStatsBtn = document.getElementById('reset-stats-btn');
    if (resetStatsBtn) resetStatsBtn.addEventListener('click', resetStatistics);

    // Transcription preview modal
    const previewCloseBtn = document.getElementById('preview-close-btn');
    if (previewCloseBtn) previewCloseBtn.addEventListener('click', closePreviewModal);

    const previewCancelBtn = document.getElementById('preview-cancel-btn');
    if (previewCancelBtn) previewCancelBtn.addEventListener('click', closePreviewModal);

    const previewCopyBtn = document.getElementById('preview-copy-btn');
    if (previewCopyBtn) previewCopyBtn.addEventListener('click', previewCopyOnly);

    const previewInsertBtn = document.getElementById('preview-insert-btn');
    if (previewInsertBtn) previewInsertBtn.addEventListener('click', previewInsert);

    // Global keyboard listener for hotkey capture
    document.addEventListener('keydown', handleHotkeyCapture);
}

/**
 * Set up IPC event listeners from main process
 */
function setupIPCListeners() {
    // Recording state changes (updates status indicator in sidebar)
    window.voxtether.onRecordingStateChanged((isRecording) => {
        if (isRecording) {
            updateStatus('Recording...', 'recording');
        } else {
            // When recording stops, check backend health to show appropriate status
            if (isBackendAvailable) {
                updateStatus('Ready', 'ready');
            } else {
                updateStatus('Backend Offline', 'error');
            }
        }
        updateTestRecordingUI(isRecording);
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

    // Start recording from main process (hotkey pressed)
    window.voxtether.onStartRecording(() => {
        handleStartRecording();
    });

    // Stop recording from main process (hotkey released)
    window.voxtether.onStopRecording(() => {
        handleStopRecording();
    });

    // Auto-updater events
    window.voxtether.onUpdateAvailable((info) => {
        console.log('Update available:', info.version);
        showUpdateNotification(info);
    });

    window.voxtether.onUpdateDownloadProgress((progress) => {
        console.log('Update download progress:', progress.percent + '%');
        handleUpdateDownloadProgress(progress);
    });

    window.voxtether.onUpdateDownloaded((info) => {
        console.log('Update downloaded:', info.version);
        showUpdateReadyNotification(info);
    });
}

/**
 * Set up system theme change listener
 */
function setupThemeListener() {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        const settings = getSettings();
        if (settings.theme === 'system') {
            applyTheme('system');
        }
    });
}

// ============================================================================
// Backend Health Monitoring
// ============================================================================

// Health check interval in milliseconds (check every 5 seconds)
const HEALTH_CHECK_INTERVAL = 5000;

// Store interval ID for cleanup
let healthCheckIntervalId = null;

// Track if backend is currently available
let isBackendAvailable = false;

/**
 * Check backend health and update the status indicator
 * @returns {Promise<boolean>} True if backend is healthy, false otherwise
 */
async function checkBackendHealth() {
    try {
        const result = await window.voxtether.backendHealth();
        if (result.success) {
            isBackendAvailable = true;
            // Only update to ready if we're not recording or processing
            const currentState = await window.voxtether.getRecordingState();
            if (!currentState.isRecording) {
                updateStatus('Ready', 'ready');
            }
            return true;
        } else {
            isBackendAvailable = false;
            updateStatus('Backend Offline', 'error');
            return false;
        }
    } catch (error) {
        console.error('Backend health check failed:', error);
        isBackendAvailable = false;
        updateStatus('Backend Offline', 'error');
        return false;
    }
}

/**
 * Start periodic backend health monitoring
 */
function startHealthMonitoring() {
    // Clear any existing interval
    if (healthCheckIntervalId) {
        clearInterval(healthCheckIntervalId);
    }

    // Set up periodic health check
    healthCheckIntervalId = setInterval(checkBackendHealth, HEALTH_CHECK_INTERVAL);
}

// Note: _stopHealthMonitoring is available for cleanup but not currently used
function _stopHealthMonitoring() {
    if (healthCheckIntervalId) {
        clearInterval(healthCheckIntervalId);
        healthCheckIntervalId = null;
    }
}

// ============================================================================
// Initialization
// ============================================================================

/**
 * Initialize the application
 */
async function initialize() {
    console.log('VoxTether renderer initializing...');

    // Load settings
    await loadSettings();

    // Load history and statistics from localStorage
    loadHistory();
    loadStatistics();

    // Get settings for theme
    const settings = getSettings();

    // Initialize UI
    initializeNavigation();
    initializeEventListeners();
    applyTheme(settings.theme);

    // Check backend health first to set correct initial status
    await checkBackendHealth();

    // Load page data
    await loadAboutInfo();
    await loadModels();
    await checkDeviceInfo();
    await loadMicDevices();

    // Set up IPC event listeners
    setupIPCListeners();

    // Set up model download progress listener
    initializeDownloadListener();

    // Set up audio device change detection
    setupAudioDeviceDetection();

    // Set up theme listener
    setupThemeListener();

    // Start periodic backend health monitoring
    startHealthMonitoring();

    console.log('VoxTether renderer ready');
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', initialize);

// Export for potential testing
export { initialize };
