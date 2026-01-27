/**
 * VoxTether Electron - Main Process
 *
 * Entry point for the Electron application. Manages the main window,
 * system tray, backend process lifecycle, and global hotkeys.
 */

const { app, BrowserWindow, Tray, Menu, ipcMain, nativeImage, dialog, shell, clipboard } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');
const http = require('http');

// Application state
let mainWindow = null;
let tray = null;
let backendProcess = null;
let isRecording = false;
let settings = null;

// Paths
const userDataPath = app.getPath('userData');
const settingsPath = path.join(userDataPath, 'settings.json');
const modelsPath = path.join(userDataPath, 'models');
const logsPath = path.join(userDataPath, 'logs');

// Backend configuration
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
    startMinimized: true,
    startWithWindows: false,
    theme: 'system'
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
 * Start the Python backend process
 */
async function startBackend() {
    return new Promise((resolve, _reject) => {
        // Find backend executable
        const backendPaths = [
            // Development path
            path.join(__dirname, '..', '..', 'backend', 'vox-backend.exe'),
            // Packaged app path
            path.join(process.resourcesPath, 'backend', 'vox-backend.exe'),
            // Alternative dev path
            path.join(__dirname, '..', '..', 'backend', 'dist', 'vox-backend.exe')
        ];

        let backendPath = null;
        for (const p of backendPaths) {
            if (fs.existsSync(p)) {
                backendPath = p;
                break;
            }
        }

        // If no exe found, try running Python directly (development mode)
        if (!backendPath) {
            const mainPyPath = path.join(__dirname, '..', '..', 'backend', 'main.py');
            if (fs.existsSync(mainPyPath)) {
                console.log('Starting backend in development mode with Python');
                backendProcess = spawn('python', ['-m', 'uvicorn', 'main:app', '--port', String(BACKEND_PORT), '--host', '127.0.0.1'], {
                    cwd: path.dirname(mainPyPath),
                    stdio: isDebug ? 'inherit' : 'ignore',
                    detached: false
                });
            } else {
                console.warn('Backend not found. Some features may not work.');
                resolve(false);
                return;
            }
        } else {
            console.log('Starting backend:', backendPath);
            backendProcess = spawn(backendPath, [], {
                stdio: isDebug ? 'inherit' : 'ignore',
                detached: false
            });
        }

        if (backendProcess) {
            backendProcess.on('error', (err) => {
                console.error('Backend process error:', err);
            });

            backendProcess.on('exit', (code) => {
                console.log('Backend exited with code:', code);
                backendProcess = null;
            });

            // Wait for backend to be ready
            waitForBackend(30000)
                .then(() => resolve(true))
                .catch(() => {
                    console.warn('Backend did not become ready in time');
                    resolve(false);
                });
        } else {
            resolve(false);
        }
    });
}

/**
 * Wait for backend to respond to health check
 */
function waitForBackend(timeoutMs) {
    return new Promise((resolve, reject) => {
        const startTime = Date.now();

        const check = () => {
            if (Date.now() - startTime > timeoutMs) {
                reject(new Error('Backend startup timeout'));
                return;
            }

            const req = http.get(`${BACKEND_URL}/api/health`, (res) => {
                if (res.statusCode === 200) {
                    console.log('Backend is ready');
                    resolve();
                } else {
                    setTimeout(check, 500);
                }
            });

            req.on('error', () => {
                setTimeout(check, 500);
            });

            req.setTimeout(2000, () => {
                req.destroy();
                setTimeout(check, 500);
            });
        };

        // Start checking after a brief delay
        setTimeout(check, 1000);
    });
}

/**
 * Stop the backend process
 */
function stopBackend() {
    if (backendProcess) {
        console.log('Stopping backend process');

        // On Windows, we need to kill the process tree
        if (process.platform === 'win32') {
            const pid = backendProcess.pid;
            // Use taskkill with the specific PID
            spawn('taskkill', ['/pid', String(pid), '/f', '/t'], { stdio: 'ignore' });
        } else {
            backendProcess.kill('SIGTERM');
        }

        backendProcess = null;
    }
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
// IPC Handlers - Communication between main and renderer processes
// ============================================================================

// Settings
ipcMain.handle('get-settings', () => settings);

ipcMain.handle('save-settings', (event, newSettings) => {
    settings = { ...settings, ...newSettings };
    return saveSettings();
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

    // Start backend
    try {
        const backendStarted = await startBackend();
        if (!backendStarted) {
            console.warn('Backend failed to start. Some features may be unavailable.');
        }
    } catch (error) {
        console.error('Failed to start backend:', error);
    }

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
});

app.on('will-quit', () => {
    stopBackend();
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('Uncaught exception:', error);
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('Unhandled rejection at:', promise, 'reason:', reason);
});
