/**
 * VoxTether Electron - Auto-Updater
 *
 * Manages automatic updates using electron-updater.
 */

const { app } = require('electron');
const path = require('path');
const fs = require('fs');
const {
    EVENT_UPDATE_AVAILABLE,
    EVENT_UPDATE_DOWNLOAD_PROGRESS,
    EVENT_UPDATE_DOWNLOADED
} = require('../shared/constants.js');

// Auto-updater (only available in packaged builds)
let autoUpdater = null;
try {
    autoUpdater = require('electron-updater').autoUpdater;
} catch (_e) {
    console.log('Auto-updater not available (development mode)');
}

/**
 * Ensure app-update.yml exists in the resources folder
 */
function ensureAppUpdateConfig() {
    if (!app.isPackaged) {
        return; // Not needed in development mode
    }

    const resourcesPath = process.resourcesPath;
    const appUpdatePath = path.join(resourcesPath, 'app-update.yml');

    if (!fs.existsSync(appUpdatePath)) {
        console.log('Creating missing app-update.yml...');
        const config = `provider: github
owner: KennethHeine
repo: VoxTether
`;
        try {
            fs.writeFileSync(appUpdatePath, config, 'utf8');
            console.log('Created app-update.yml successfully');
        } catch (error) {
            console.error('Failed to create app-update.yml:', error.message);
        }
    }
}

/**
 * Set up auto-updater event handlers
 * @param {object} mainWindow - Main window instance to send events to
 */
function setupAutoUpdater(mainWindow) {
    if (!autoUpdater) {
        console.log('Auto-updater not available');
        return;
    }

    // Ensure app-update.yml exists before configuring updater
    ensureAppUpdateConfig();

    // Configure auto-updater
    autoUpdater.autoDownload = false;  // Don't auto-download, let user decide
    autoUpdater.autoInstallOnAppQuit = true;

    autoUpdater.on('checking-for-update', () => {
        console.log('Checking for updates...');
    });

    autoUpdater.on('update-available', (info) => {
        console.log('Update available:', info.version);
        // Notify renderer about available update
        if (mainWindow) {
            mainWindow.webContents.send(EVENT_UPDATE_AVAILABLE, info);
        }
    });

    autoUpdater.on('update-not-available', () => {
        console.log('No updates available');
    });

    autoUpdater.on('download-progress', (progress) => {
        if (mainWindow) {
            mainWindow.webContents.send(EVENT_UPDATE_DOWNLOAD_PROGRESS, progress);
        }
    });

    autoUpdater.on('update-downloaded', (info) => {
        console.log('Update downloaded:', info.version);
        if (mainWindow) {
            mainWindow.webContents.send(EVENT_UPDATE_DOWNLOADED, info);
        }
    });

    autoUpdater.on('error', (error) => {
        console.error('Auto-updater error:', error);
    });

    // Check for updates on startup (after a short delay)
    setTimeout(() => {
        autoUpdater.checkForUpdates().catch(err => {
            console.log('Update check failed:', err.message);
        });
    }, 5000);
}

/**
 * Get the autoUpdater instance
 * @returns {object|null} The autoUpdater instance
 */
function getAutoUpdater() {
    return autoUpdater;
}

module.exports = {
    setupAutoUpdater,
    ensureAppUpdateConfig,
    getAutoUpdater
};
