/**
 * VoxTether Electron - Recording Overlay
 *
 * Manages the recording indicator overlay window displayed at the top of the screen.
 */

const { BrowserWindow, screen } = require('electron');
const path = require('path');
const { VALID_OVERLAY_STATES, OVERLAY_STATE_HIDDEN } = require('../shared/constants.js');

let recordingOverlayWindow = null;
let currentOverlayState = OVERLAY_STATE_HIDDEN;

/**
 * Create the recording indicator overlay window - a horizontal bar at the top of the screen
 * @returns {BrowserWindow} The overlay window
 */
function createRecordingOverlay() {
    if (recordingOverlayWindow) {
        return recordingOverlayWindow;
    }

    // Get screen dimensions
    const primaryDisplay = screen.getPrimaryDisplay();
    const { width: screenWidth } = primaryDisplay.workAreaSize;

    // Bar dimensions - centered at top of screen
    const barWidth = 200;
    const barHeight = 6;

    // Create a small, always-on-top window for the bar
    recordingOverlayWindow = new BrowserWindow({
        width: barWidth,
        height: barHeight,
        x: Math.round((screenWidth - barWidth) / 2),  // Center horizontally
        y: 0,  // Top of screen
        frame: false,
        transparent: true,
        alwaysOnTop: true,
        skipTaskbar: true,
        resizable: false,
        movable: false,
        focusable: false,
        show: false,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true
        }
    });

    // Load the overlay HTML file
    recordingOverlayWindow.loadFile(path.join(__dirname, '..', 'overlay', 'overlay.html'));

    recordingOverlayWindow.on('closed', () => {
        recordingOverlayWindow = null;
        currentOverlayState = OVERLAY_STATE_HIDDEN;
    });

    return recordingOverlayWindow;
}

/**
 * Update the overlay state (recording or transcribing)
 * @param {'recording'|'transcribing'} state - The state to show
 */
function updateOverlayState(state) {
    // Validate state to prevent XSS
    if (!VALID_OVERLAY_STATES.includes(state)) {
        console.warn(`Invalid overlay state: ${state}`);
        return;
    }

    if (recordingOverlayWindow && !recordingOverlayWindow.isDestroyed()) {
        recordingOverlayWindow.webContents.executeJavaScript(`
            document.getElementById('overlay-bar').className = 'bar ${state}';
        `).then(() => {
            currentOverlayState = state;
        }).catch(() => {
            // Ignore errors if window is being destroyed
        });
    }
}

/**
 * Show the recording overlay
 * @param {'recording'|'transcribing'} state - The state to show (default: 'recording')
 * @param {object} settings - Application settings
 */
function showRecordingOverlay(state = 'recording', settings) {
    if (!settings.showRecordingIndicator) {
        return;
    }

    // Validate state to prevent XSS
    if (!VALID_OVERLAY_STATES.includes(state)) {
        console.warn(`Invalid overlay state: ${state}`);
        return;
    }

    const overlay = createRecordingOverlay();
    if (overlay && !overlay.isDestroyed()) {
        // Update state before showing to avoid brief flicker
        updateOverlayState(state);
        overlay.showInactive();
    }
}

/**
 * Hide the recording overlay
 */
function hideRecordingOverlay() {
    if (recordingOverlayWindow && !recordingOverlayWindow.isDestroyed()) {
        recordingOverlayWindow.hide();
        currentOverlayState = OVERLAY_STATE_HIDDEN;
    }
}

/**
 * Show transcribing overlay (loading state after recording)
 * @param {object} settings - Application settings
 */
function showTranscribingOverlay(settings) {
    showRecordingOverlay('transcribing', settings);
}

/**
 * Get current overlay state
 * @returns {'hidden'|'recording'|'transcribing'} Current state
 */
function getOverlayState() {
    return currentOverlayState;
}

module.exports = {
    createRecordingOverlay,
    updateOverlayState,
    showRecordingOverlay,
    hideRecordingOverlay,
    showTranscribingOverlay,
    getOverlayState
};
