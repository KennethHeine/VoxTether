/**
 * VoxTether Electron - Main Process Entry Point
 *
 * Orchestrates all main process modules and manages application lifecycle.
 */

const { app, BrowserWindow, Menu } = require('electron');
const fs = require('fs');

// Import modules
const { loadSettings, saveSettings, getSettings, updateSettings, getModelsPath, getLogsPath, getUserDataPath } = require('./settings-manager.js');
const { checkBackendConnection, backendRequest } = require('./backend-client.js');
const { createMainWindow, getMainWindow, toggleWindowVisibility, getIconPath } = require('./window.js');
const { createTray, updateTrayMenu } = require('./tray.js');
const { registerWindowToggleHotkey, registerToggleRecordingHotkey, unregisterAllHotkeys } = require('./hotkeys.js');
const { getRecordingState, startRecording, stopRecording, toggleRecording } = require('./recording.js');
const { showRecordingOverlay, hideRecordingOverlay, showTranscribingOverlay, getOverlayState } = require('./overlay.js');
const { setupAutoUpdater, getAutoUpdater } = require('./updater.js');
const { registerIpcHandlers } = require('./ipc-handlers.js');
const { EVENT_TEST_MICROPHONE } = require('../shared/constants.js');

// ============================================================================
// Application Lifecycle
// ============================================================================

// Single instance lock
const gotTheLock = app.requestSingleInstanceLock();

if (!gotTheLock) {
    app.quit();
} else {
    app.on('second-instance', () => {
        // Someone tried to run a second instance
        const mainWindow = getMainWindow();
        if (mainWindow) {
            if (mainWindow.isMinimized()) mainWindow.restore();
            mainWindow.show();
            mainWindow.focus();
        }
    });
}

// App ready
app.whenReady().then(async () => {
    console.log('VoxTether Electron starting...');

    // Remove the default application menu (File, Edit, View, Window, Help)
    Menu.setApplicationMenu(null);

    // Load settings
    loadSettings();
    const settings = getSettings();

    // Create necessary directories
    const modelsPath = getModelsPath();
    const logsPath = getLogsPath();
    [modelsPath, logsPath].forEach(dir => {
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
    });

    // Create tray icon
    const iconPath = getIconPath();
    createTray(iconPath, () => {
        const mainWindow = getMainWindow();
        if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
        } else {
            createMainWindow(settings);
        }
    });

    // Update tray menu
    const updateTrayMenuWrapper = () => {
        const mainWindow = getMainWindow();
        updateTrayMenu(
            getRecordingState(),
            () => {
                if (mainWindow) {
                    mainWindow.show();
                    mainWindow.focus();
                } else {
                    createMainWindow(getSettings());
                }
            },
            () => {
                const mainWindow = getMainWindow();
                if (mainWindow) {
                    mainWindow.webContents.send(EVENT_TEST_MICROPHONE);
                }
            },
            modelsPath,
            logsPath
        );
    };

    // Create main window
    createMainWindow(settings);

    // Update initial tray menu
    updateTrayMenuWrapper();

    // Register IPC handlers
    registerIpcHandlers({
        getSettings,
        updateSettings,
        saveSettings,
        getUserDataPath,
        getModelsPath,
        getLogsPath,
        backendRequest,
        getRecordingState,
        startRecording: () => {
            const mainWindow = getMainWindow();
            const settings = getSettings();
            startRecording(
                () => showRecordingOverlay('recording', settings),
                updateTrayMenuWrapper,
                mainWindow
            );
        },
        stopRecording: () => {
            const mainWindow = getMainWindow();
            stopRecording(
                hideRecordingOverlay,
                updateTrayMenuWrapper,
                mainWindow
            );
        },
        showTranscribingOverlay: () => {
            showTranscribingOverlay(getSettings());
        },
        hideOverlay: hideRecordingOverlay,
        getOverlayState,
        registerWindowToggleHotkey: () => {
            const settings = getSettings();
            registerWindowToggleHotkey(settings.windowToggleHotkey, toggleWindowVisibility);
        },
        registerToggleRecordingHotkey: () => {
            const settings = getSettings();
            const mainWindow = getMainWindow();
            registerToggleRecordingHotkey(settings.toggleRecordingHotkey, () => {
                toggleRecording(
                    () => showRecordingOverlay('recording', getSettings()),
                    hideRecordingOverlay,
                    updateTrayMenuWrapper,
                    mainWindow
                );
            });
        },
        getMainWindow,
        getAutoUpdater
    });

    // Check backend connection (backend should be running separately)
    try {
        const backendAvailable = await checkBackendConnection();
        if (!backendAvailable) {
            console.warn('Backend server not available. Please ensure the Python backend is running.');
            console.warn('Start it with: cd src/backend && python -m uvicorn main:app --port 5678');
        }
    } catch (error) {
        console.error('Failed to check backend connection:', error);
    }

    // Register window toggle hotkey
    registerWindowToggleHotkey(settings.windowToggleHotkey, toggleWindowVisibility);

    // Register toggle recording hotkey
    const mainWindow = getMainWindow();
    registerToggleRecordingHotkey(settings.toggleRecordingHotkey, () => {
        toggleRecording(
            () => showRecordingOverlay('recording', getSettings()),
            hideRecordingOverlay,
            updateTrayMenuWrapper,
            mainWindow
        );
    });

    // Set up auto-updater
    setupAutoUpdater(mainWindow);

    console.log('VoxTether Electron ready');
});

// Prevent app from quitting when all windows are closed
app.on('window-all-closed', () => {
    // On Windows, keep the app running in the tray
    if (process.platform !== 'darwin') {
        // Don't quit, just hide
    }
});

app.on('activate', () => {
    // On macOS, re-create window when dock icon is clicked
    if (BrowserWindow.getAllWindows().length === 0) {
        createMainWindow(getSettings());
    }
});

// Clean up on quit
app.on('before-quit', () => {
    app.isQuitting = true;
    // Unregister all shortcuts
    unregisterAllHotkeys();
    // Stop any active recording before quitting
    if (getRecordingState()) {
        const mainWindow = getMainWindow();
        stopRecording(hideRecordingOverlay, () => {}, mainWindow);
    }
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('Uncaught exception:', error);
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('Unhandled rejection at:', promise, 'reason:', reason);
});
