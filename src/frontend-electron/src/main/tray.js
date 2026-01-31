/**
 * VoxTether Electron - System Tray
 *
 * Manages the system tray icon and context menu.
 */

const { Tray, Menu, nativeImage, app, dialog, shell } = require('electron');
const fs = require('fs');

let tray = null;

/**
 * Create the system tray icon
 * @param {string} iconPath - Path to the icon file
 * @param {Function} onSettingsClick - Callback for settings menu item
 * @returns {Tray} The tray instance
 */
function createTray(iconPath, onSettingsClick) {
    let icon;

    if (iconPath && fs.existsSync(iconPath)) {
        icon = nativeImage.createFromPath(iconPath);
    } else {
        // Create a simple default icon
        icon = nativeImage.createEmpty();
    }

    tray = new Tray(icon);
    tray.setToolTip('VoxTether - Voice dictation');

    // Show window on double-click
    tray.on('double-click', onSettingsClick);

    return tray;
}

/**
 * Update the tray context menu
 * @param {boolean} isRecording - Whether recording is in progress
 * @param {Function} onSettingsClick - Callback for settings menu item
 * @param {Function} onTestMicrophone - Callback for test microphone
 * @param {string} modelsPath - Path to models folder
 * @param {string} logsPath - Path to logs folder
 */
function updateTrayMenu(isRecording, onSettingsClick, onTestMicrophone, modelsPath, logsPath) {
    const contextMenu = Menu.buildFromTemplate([
        {
            label: isRecording ? '🔴 Recording...' : '⚪ Ready',
            enabled: false
        },
        { type: 'separator' },
        {
            label: 'Settings...',
            click: onSettingsClick
        },
        {
            label: 'Test Microphone',
            click: onTestMicrophone
        },
        { type: 'separator' },
        {
            label: 'Open Models Folder',
            click: () => {
                if (!fs.existsSync(modelsPath)) {
                    fs.mkdirSync(modelsPath, { recursive: true });
                }
                shell.openPath(modelsPath);
            }
        },
        {
            label: 'Open Logs',
            click: () => {
                if (!fs.existsSync(logsPath)) {
                    fs.mkdirSync(logsPath, { recursive: true });
                }
                shell.openPath(logsPath);
            }
        },
        { type: 'separator' },
        {
            label: 'About VoxTether',
            click: () => {
                dialog.showMessageBox({
                    type: 'info',
                    title: 'About VoxTether',
                    message: 'VoxTether',
                    detail: `Version: ${app.getVersion()}\n\nVoice dictation for Windows.\nFully offline speech-to-text using faster-whisper.\n\nElectron Frontend with Python Backend`,
                    buttons: ['OK']
                });
            }
        },
        { type: 'separator' },
        {
            label: 'Exit',
            click: () => {
                app.isQuitting = true;
                app.quit();
            }
        }
    ]);

    if (tray) {
        tray.setContextMenu(contextMenu);
    }
}

/**
 * Get the tray instance
 * @returns {Tray|null} The tray instance
 */
function getTray() {
    return tray;
}

module.exports = {
    createTray,
    updateTrayMenu,
    getTray
};
