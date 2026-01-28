/**
 * VoxTether Electron - Main Process
 *
 * Entry point for the Electron application. Manages the main window,
 * system tray, and global hotkeys. Connects to a separate Python backend
 * server running on localhost.
 */

const { app, BrowserWindow, Tray, Menu, ipcMain, nativeImage, dialog, shell, clipboard, globalShortcut } = require('electron');
const path = require('path');
const fs = require('fs');
const http = require('http');

// Application state
let mainWindow = null;
let tray = null;
let isRecording = false;
let settings = null;
let registeredHotkey = null;

// Paths
const userDataPath = app.getPath('userData');
const settingsPath = path.join(userDataPath, 'settings.json');
const modelsPath = path.join(userDataPath, 'models');
const logsPath = path.join(userDataPath, 'logs');

// Backend configuration - connects to separate Python server on localhost
const BACKEND_PORT = 5678;
const BACKEND_URL = `http://127.0.0.1:${BACKEND_PORT}`;

// Debug mode
const isDebug = process.argv.includes('--debug');

/**
 * Default application settings
 */
const defaultSettings = {
    hotkey: 'Ctrl+Shift+Space',
    modelName: 'small',
    language: 'auto',
    outputMode: 'ClipboardAndPaste',
    showNotifications: true,
    showRecordingIndicator: true,
    audioDeviceId: -1,
    clipboardDelayMs: 50,
    firstRunCompleted: false,
    backendPort: BACKEND_PORT,
    backendHost: '127.0.0.1',
    startMinimized: true,
    startWithWindows: false,
    theme: 'system',
    recordingOutputFolder: '',
    saveRecordingAudio: false,
    saveRecordingTranscript: false
};

/**
 * Load settings from file or create with defaults
 */
function loadSettings() {
    try {
        if (fs.existsSync(settingsPath)) {
            const data = fs.readFileSync(settingsPath, 'utf8');
            settings = { ...defaultSettings, ...JSON.parse(data) };
        } else {
            settings = { ...defaultSettings };
            saveSettings();
        }
    } catch (error) {
        console.error('Failed to load settings:', error);
        settings = { ...defaultSettings };
    }
    return settings;
}

/**
 * Save settings to file
 */
function saveSettings() {
    try {
        // Ensure directory exists
        const dir = path.dirname(settingsPath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2));
        return true;
    } catch (error) {
        console.error('Failed to save settings:', error);
        return false;
    }
}

/**
 * Create the main application window
 */
function createMainWindow() {
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
            preload: path.join(__dirname, 'preload.js')
        }
    });

    mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

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
 * Get the application icon path
 */
function getIconPath() {
    // Check in assets folder
    const assetsIcon = path.join(__dirname, '..', 'assets', 'icon.ico');
    if (fs.existsSync(assetsIcon)) {
        return assetsIcon;
    }

    // Check in root assets folder
    const rootIcon = path.join(__dirname, '..', '..', '..', 'assets', 'icon.ico');
    if (fs.existsSync(rootIcon)) {
        return rootIcon;
    }

    return null;
}

/**
 * Create the system tray icon
 */
function createTray() {
    const iconPath = getIconPath();
    let icon;

    if (iconPath && fs.existsSync(iconPath)) {
        icon = nativeImage.createFromPath(iconPath);
    } else {
        // Create a simple default icon
        icon = nativeImage.createEmpty();
    }

    tray = new Tray(icon);
    tray.setToolTip('VoxTether - Push-to-talk dictation');

    updateTrayMenu();

    // Show window on double-click
    tray.on('double-click', () => {
        if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
        } else {
            createMainWindow();
        }
    });
}

/**
 * Update the tray context menu
 */
function updateTrayMenu() {
    const contextMenu = Menu.buildFromTemplate([
        {
            label: isRecording ? '🔴 Recording...' : '⚪ Ready',
            enabled: false
        },
        { type: 'separator' },
        {
            label: 'Settings...',
            click: () => {
                if (mainWindow) {
                    mainWindow.show();
                    mainWindow.focus();
                } else {
                    createMainWindow();
                }
            }
        },
        {
            label: 'Test Microphone',
            click: async () => {
                if (mainWindow) {
                    mainWindow.webContents.send('test-microphone');
                }
            }
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
                    detail: `Version: ${app.getVersion()}\n\nPush-to-talk dictation for Windows.\nFully offline speech-to-text using faster-whisper.\n\nElectron Frontend with Python Backend`,
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

    tray.setContextMenu(contextMenu);
}

/**
 * Check if the Python backend server is running on localhost
 * The backend should be started separately with: python -m uvicorn main:app --port 5678
 */
async function checkBackendConnection() {
    return new Promise((resolve) => {
        let resolved = false;

        const req = http.get(`${BACKEND_URL}/api/health`, (res) => {
            resolved = true;
            if (res.statusCode === 200) {
                console.log('Backend server is available');
                resolve(true);
            } else {
                console.warn('Backend server returned non-200 status');
                resolve(false);
            }
        });

        req.on('error', () => {
            if (!resolved) {
                resolved = true;
                console.warn('Backend server not available at', BACKEND_URL);
                resolve(false);
            }
        });

        req.setTimeout(5000, () => {
            if (!resolved) {
                resolved = true;
                req.destroy();
                console.warn('Backend server connection timeout');
                resolve(false);
            }
        });
    });
}

/**
 * Make HTTP request to backend
 */
function backendRequest(method, endpoint, body = null) {
    return new Promise((resolve, reject) => {
        const url = new URL(endpoint, BACKEND_URL);
        const options = {
            hostname: url.hostname,
            port: url.port,
            path: url.pathname,
            method: method,
            headers: {
                'Content-Type': 'application/json'
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(data));
                } catch {
                    resolve(data);
                }
            });
        });

        req.on('error', reject);
        req.setTimeout(30000, () => {
            req.destroy();
            reject(new Error('Request timeout'));
        });

        if (body) {
            req.write(JSON.stringify(body));
        }
        req.end();
    });
}

// ============================================================================
// Global Hotkey Registration
// ============================================================================

/**
 * Register the push-to-talk global hotkey
 */
function registerHotkey() {
    // Unregister previous hotkey if exists
    if (registeredHotkey) {
        try {
            globalShortcut.unregister(registeredHotkey);
        } catch (error) {
            console.warn('Failed to unregister previous hotkey:', error);
        }
        registeredHotkey = null;
    }

    const hotkey = settings.hotkey;
    if (!hotkey) {
        console.log('No hotkey configured');
        return false;
    }

    try {
        // Convert our hotkey format to Electron's format
        const electronHotkey = convertToElectronHotkey(hotkey);
        console.log(`Registering hotkey: ${hotkey} -> ${electronHotkey}`);

        const success = globalShortcut.register(electronHotkey, () => {
            // Toggle recording on hotkey press
            toggleRecording();
        });

        if (success) {
            registeredHotkey = electronHotkey;
            console.log('Hotkey registered successfully');
            return true;
        } else {
            console.error('Failed to register hotkey');
            return false;
        }
    } catch (error) {
        console.error('Error registering hotkey:', error);
        return false;
    }
}

/**
 * Convert our hotkey format to Electron's accelerator format
 */
function convertToElectronHotkey(hotkey) {
    // Our format: Ctrl+Shift+Space
    // Electron format: CommandOrControl+Shift+Space
    return hotkey
        .replace(/Ctrl/g, 'CommandOrControl')
        .replace(/Win/g, 'Super');
}

/**
 * Toggle recording state
 */
function toggleRecording() {
    if (isRecording) {
        stopRecording();
    } else {
        startRecording();
    }
}

/**
 * Start recording
 */
function startRecording() {
    if (isRecording) return;

    isRecording = true;
    console.log('Recording started');

    // Update tray menu
    updateTrayMenu();

    // Notify renderer to start recording
    if (mainWindow) {
        mainWindow.webContents.send('recording-state-changed', true);
        mainWindow.webContents.send('start-recording');
    }
}

/**
 * Stop recording
 */
function stopRecording() {
    if (!isRecording) return;

    isRecording = false;
    console.log('Recording stopped');

    // Update tray menu
    updateTrayMenu();

    // Notify renderer to stop recording
    if (mainWindow) {
        mainWindow.webContents.send('recording-state-changed', false);
        mainWindow.webContents.send('stop-recording');
    }
}

// ============================================================================
// IPC Handlers - Communication between main and renderer processes
// ============================================================================

// Settings
ipcMain.handle('get-settings', () => settings);

ipcMain.handle('save-settings', (event, newSettings) => {
    const hotkeyChanged = newSettings.hotkey && newSettings.hotkey !== settings.hotkey;
    settings = { ...settings, ...newSettings };
    const saved = saveSettings();

    // Re-register hotkey if it changed
    if (hotkeyChanged) {
        registerHotkey();
    }

    return saved;
});

// Recording control from renderer
ipcMain.handle('start-recording-manual', () => {
    startRecording();
    return { success: true };
});

ipcMain.handle('stop-recording-manual', () => {
    stopRecording();
    return { success: true };
});

ipcMain.handle('get-recording-state', () => {
    return { isRecording };
});

// Backend communication
ipcMain.handle('backend-health', async () => {
    try {
        const result = await backendRequest('GET', '/api/health');
        return { success: true, data: result };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('get-devices', async () => {
    try {
        const result = await backendRequest('GET', '/api/devices');
        return { success: true, data: result };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('get-models', async () => {
    try {
        const result = await backendRequest('GET', '/api/models');
        return { success: true, data: result };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('download-model', async (event, modelName) => {
    // For model download, we use SSE which requires special handling
    // Forward progress to renderer
    const url = `${BACKEND_URL}/api/models/${modelName}/download`;

    return new Promise((resolve) => {
        const req = http.request(url, { method: 'POST' }, (res) => {
            res.on('data', (chunk) => {
                const lines = chunk.toString().split('\n');
                for (const line of lines) {
                    if (line.startsWith('data: ')) {
                        try {
                            const data = JSON.parse(line.substring(6));
                            if (mainWindow) {
                                mainWindow.webContents.send('download-progress', data);
                            }
                            if (data.status === 'complete') {
                                resolve({ success: true });
                            } else if (data.status === 'error') {
                                resolve({ success: false, error: data.error });
                            }
                        } catch (_e) {
                            // Ignore parse errors for partial data
                        }
                    }
                }
            });

            res.on('end', () => {
                resolve({ success: true });
            });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: error.message });
        });

        req.end();
    });
});

ipcMain.handle('load-model', async (event, modelName) => {
    try {
        await backendRequest('POST', `/api/models/${modelName}/load`);
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('delete-model', async (event, modelName) => {
    try {
        await backendRequest('DELETE', `/api/models/${modelName}`);
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

// Transcription
ipcMain.handle('transcribe', async (event, audioPath, language) => {
    return new Promise((resolve, _reject) => {
        // Create multipart form data
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;
        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);

        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        const bodyEnd = `\r\n--${boundary}\r\n` +
            `Content-Disposition: form-data; name="language"\r\n\r\n${language || 'auto'}\r\n` +
            `--${boundary}--\r\n`;

        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        const options = {
            hostname: '127.0.0.1',
            port: BACKEND_PORT,
            path: '/api/transcribe',
            method: 'POST',
            headers: {
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': bodyBuffer.length
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve({ success: true, data: JSON.parse(data) });
                } catch {
                    resolve({ success: false, error: 'Failed to parse response' });
                }
            });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: error.message });
        });

        req.write(bodyBuffer);
        req.end();
    });
});

// Clipboard
ipcMain.handle('copy-to-clipboard', (event, text) => {
    clipboard.writeText(text);
    return true;
});

// Shell
ipcMain.handle('open-path', (event, pathToOpen) => {
    shell.openPath(pathToOpen);
});

ipcMain.handle('open-external', (event, url) => {
    shell.openExternal(url);
});

// File dialogs
ipcMain.handle('select-audio-file', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        title: 'Select Audio File',
        filters: [
            { name: 'Audio Files', extensions: ['wav', 'mp3', 'm4a', 'flac', 'ogg', 'wma', 'aac', 'webm'] },
            { name: 'All Files', extensions: ['*'] }
        ],
        properties: ['openFile']
    });

    if (result.canceled || result.filePaths.length === 0) {
        return { success: false, canceled: true };
    }

    return { success: true, filePath: result.filePaths[0] };
});

ipcMain.handle('select-output-folder', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        title: 'Select Output Folder',
        properties: ['openDirectory', 'createDirectory']
    });

    if (result.canceled || result.filePaths.length === 0) {
        return { success: false, canceled: true };
    }

    return { success: true, folderPath: result.filePaths[0] };
});

ipcMain.handle('save-transcript', async (event, filePath, content) => {
    try {
        fs.writeFileSync(filePath, content, 'utf8');
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('save-audio-file', async (event, audioData) => {
    try {
        // audioData is a base64 encoded string or array of bytes
        const tempDir = path.join(userDataPath, 'temp');
        if (!fs.existsSync(tempDir)) {
            fs.mkdirSync(tempDir, { recursive: true });
        }

        const timestamp = Date.now();
        const tempPath = path.join(tempDir, `recording_${timestamp}.wav`);

        // Convert base64 to buffer if needed
        let buffer;
        if (typeof audioData === 'string') {
            buffer = Buffer.from(audioData, 'base64');
        } else {
            buffer = Buffer.from(audioData);
        }

        fs.writeFileSync(tempPath, buffer);
        return { success: true, filePath: tempPath };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('delete-temp-file', async (event, filePath) => {
    try {
        if (fs.existsSync(filePath) && filePath.includes('temp')) {
            fs.unlinkSync(filePath);
        }
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('copy-file', async (event, sourcePath, destFolder) => {
    try {
        const fileName = path.basename(sourcePath);
        const destPath = path.join(destFolder, fileName);
        fs.copyFileSync(sourcePath, destPath);
        return { success: true, destPath: destPath };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

ipcMain.handle('select-recording-folder', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        title: 'Select Recording Output Folder',
        properties: ['openDirectory', 'createDirectory']
    });

    if (result.canceled || result.filePaths.length === 0) {
        return { success: false, canceled: true };
    }

    return { success: true, folderPath: result.filePaths[0] };
});

ipcMain.handle('save-recording-output', async (event, options) => {
    // options: { audioData: base64, transcript: string, baseFolder: string, saveAudio: boolean, saveTranscript: boolean }
    try {
        const { audioData, transcript, baseFolder, saveAudio, saveTranscript } = options;

        if (!baseFolder) {
            return { success: false, error: 'No output folder configured' };
        }

        // Security: Validate baseFolder is an absolute path and doesn't contain traversal sequences
        const normalizedBase = path.normalize(baseFolder);
        if (!path.isAbsolute(normalizedBase) || normalizedBase.includes('..')) {
            return { success: false, error: 'Invalid output folder path' };
        }

        // Verify base folder exists and is writable
        if (!fs.existsSync(normalizedBase)) {
            return { success: false, error: 'Output folder does not exist' };
        }

        // Check if we actually have data to save
        const willSaveAudio = saveAudio && audioData;
        const willSaveTranscript = saveTranscript && transcript;

        if (!willSaveAudio && !willSaveTranscript) {
            return { success: true, message: 'Nothing to save' };
        }

        // Create timestamped folder name using local time (e.g., "2024-01-15_14-30-45")
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const hours = String(now.getHours()).padStart(2, '0');
        const minutes = String(now.getMinutes()).padStart(2, '0');
        const seconds = String(now.getSeconds()).padStart(2, '0');
        const folderName = `${year}-${month}-${day}_${hours}-${minutes}-${seconds}`;

        const outputFolder = path.join(normalizedBase, folderName);

        // Create the folder using async operations
        await fs.promises.mkdir(outputFolder, { recursive: true });

        let audioPath = null;
        let transcriptPath = null;

        // Save audio file if requested
        if (willSaveAudio) {
            const buffer = Buffer.from(audioData, 'base64');
            audioPath = path.join(outputFolder, 'recording.wav');
            await fs.promises.writeFile(audioPath, buffer);
        }

        // Save transcript if requested
        if (willSaveTranscript) {
            transcriptPath = path.join(outputFolder, 'transcript.txt');
            await fs.promises.writeFile(transcriptPath, transcript, 'utf8');
        }

        return {
            success: true,
            folderPath: outputFolder,
            audioPath: audioPath,
            transcriptPath: transcriptPath
        };
    } catch (error) {
        return { success: false, error: error.message };
    }
});

// App info
ipcMain.handle('get-app-info', () => ({
    version: app.getVersion(),
    userDataPath: userDataPath,
    modelsPath: modelsPath,
    logsPath: logsPath
}));

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

    // Load settings
    loadSettings();

    // Create necessary directories
    [modelsPath, logsPath].forEach(dir => {
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
    });

    // Create tray icon
    createTray();

    // Create main window
    createMainWindow();

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

    // Register global hotkey
    registerHotkey();

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
        createMainWindow();
    }
});

// Clean up on quit
app.on('before-quit', () => {
    app.isQuitting = true;
    // Unregister all shortcuts
    globalShortcut.unregisterAll();
});

// Note: Backend is managed separately, no cleanup needed here

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('Uncaught exception:', error);
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('Unhandled rejection at:', promise, 'reason:', reason);
});
