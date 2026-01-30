/**
 * VoxTether Electron - Recording State Management
 *
 * Manages recording state and coordination between main and renderer processes.
 */

const {
    EVENT_RECORDING_STATE_CHANGED,
    EVENT_START_RECORDING,
    EVENT_STOP_RECORDING
} = require('../shared/constants.js');

let isRecording = false;

/**
 * Get current recording state
 * @returns {boolean} True if recording is in progress
 */
function getRecordingState() {
    return isRecording;
}

/**
 * Start recording
 * @param {Function} showOverlay - Function to show recording overlay
 * @param {Function} updateTrayMenu - Function to update tray menu
 * @param {object} mainWindow - Main window instance
 */
function startRecording(showOverlay, updateTrayMenu, mainWindow) {
    if (isRecording) return;

    isRecording = true;
    console.log('Recording started');

    // Show recording overlay indicator
    if (showOverlay) {
        showOverlay();
    }

    // Update tray menu
    if (updateTrayMenu) {
        updateTrayMenu();
    }

    // Notify renderer to start recording
    if (mainWindow) {
        mainWindow.webContents.send(EVENT_RECORDING_STATE_CHANGED, true);
        mainWindow.webContents.send(EVENT_START_RECORDING);
    }
}

/**
 * Stop recording
 * @param {Function} hideOverlay - Function to hide recording overlay
 * @param {Function} updateTrayMenu - Function to update tray menu
 * @param {object} mainWindow - Main window instance
 */
function stopRecording(hideOverlay, updateTrayMenu, mainWindow) {
    if (!isRecording) return;

    isRecording = false;
    console.log('Recording stopped');

    // Hide recording overlay indicator
    if (hideOverlay) {
        hideOverlay();
    }

    // Update tray menu
    if (updateTrayMenu) {
        updateTrayMenu();
    }

    // Notify renderer to stop recording
    if (mainWindow) {
        mainWindow.webContents.send(EVENT_RECORDING_STATE_CHANGED, false);
        mainWindow.webContents.send(EVENT_STOP_RECORDING);
    }
}

/**
 * Toggle recording state
 * @param {Function} showOverlay - Function to show recording overlay
 * @param {Function} hideOverlay - Function to hide recording overlay
 * @param {Function} updateTrayMenu - Function to update tray menu
 * @param {object} mainWindow - Main window instance
 */
function toggleRecording(showOverlay, hideOverlay, updateTrayMenu, mainWindow) {
    if (isRecording) {
        stopRecording(hideOverlay, updateTrayMenu, mainWindow);
    } else {
        startRecording(showOverlay, updateTrayMenu, mainWindow);
    }
}

module.exports = {
    getRecordingState,
    startRecording,
    stopRecording,
    toggleRecording
};
