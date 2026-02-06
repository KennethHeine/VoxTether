/**
 * VoxTether Electron - Window Management
 *
 * Creates and manages the main application window.
 */

const { BrowserWindow, app } = require('electron');
const path = require('path');
const fs = require('fs');

let mainWindow = null;
const isDebug = process.argv.includes('--debug');

/**
 * Get the path to the application icon
 * @returns {string|null} Path to the icon file
 */
function getIconPath() {
    // In packaged app, check extraResources folder
    const resourcesIcon = path.join(process.resourcesPath || '', 'assets', 'icon.ico');
    if (fs.existsSync(resourcesIcon)) {
        return resourcesIcon;
    }

    // Check in frontend-electron assets folder (development mode)
    const assetsIcon = path.join(__dirname, '..', 'assets', 'icon.ico');
    if (fs.existsSync(assetsIcon)) {
        return assetsIcon;
    }

    // Check in root assets folder (development mode)
    const rootIcon = path.join(__dirname, '..', '..', '..', 'assets', 'icon.ico');
    if (fs.existsSync(rootIcon)) {
        return rootIcon;
    }

    return null;
}

/**
 * Create the main application window
 * @param {object} settings - Application settings
 * @returns {BrowserWindow} The created window
 */
function createMainWindow(settings) {
    mainWindow = new BrowserWindow({
        width: 800,
        height: 600,
        minWidth: 600,
        minHeight: 400,
        title: 'VoxTether Settings',
        icon: getIconPath(),
        show: !settings.startMinimized,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, '..', 'preload.js'),
            // IMPORTANT: Disable background throttling to ensure IPC events (like start-recording)
            // are processed immediately even when window is hidden. This is essential for the
            // hotkey-triggered recording feature to work when settings window is minimized.
            // Trade-off: Slightly higher CPU/battery usage when window is hidden.
            backgroundThrottling: false
        }
    });

    mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'));

    // Open DevTools in debug mode
    if (isDebug) {
        mainWindow.webContents.openDevTools();
    }

    // Handle window close - minimize to tray instead
    mainWindow.on('close', (event) => {
        if (!app.isQuitting) {
            event.preventDefault();
            mainWindow.hide();
        }
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });

    return mainWindow;
}

/**
 * Get the main window instance
 * @returns {BrowserWindow|null} The main window
 */
function getMainWindow() {
    return mainWindow;
}

/**
 * Toggle main window visibility
 */
function toggleWindowVisibility() {
    if (!mainWindow) return;

    if (mainWindow.isVisible()) {
        mainWindow.hide();
    } else {
        mainWindow.show();
        mainWindow.focus();
    }
}

module.exports = {
    createMainWindow,
    getMainWindow,
    toggleWindowVisibility,
    getIconPath
};
