/**
 * VoxTether Electron - IPC Handlers
 *
 * Handles all IPC (Inter-Process Communication) between main and renderer processes.
 */

const { ipcMain, clipboard, shell, dialog, app } = require('electron');
const path = require('path');
const fs = require('fs');
const http = require('http');
const semver = require('semver');
const {
    IPC_GET_SETTINGS,
    IPC_SAVE_SETTINGS,
    IPC_START_RECORDING_MANUAL,
    IPC_STOP_RECORDING_MANUAL,
    IPC_GET_RECORDING_STATE,
    IPC_SHOW_TRANSCRIBING_OVERLAY,
    IPC_HIDE_OVERLAY,
    IPC_GET_OVERLAY_STATE,
    IPC_BACKEND_HEALTH,
    IPC_GET_DEVICES,
    IPC_GET_MODELS,
    IPC_DOWNLOAD_MODEL,
    IPC_LOAD_MODEL,
    IPC_DELETE_MODEL,
    IPC_TRANSCRIBE,
    IPC_COPY_TO_CLIPBOARD,
    IPC_OPEN_PATH,
    IPC_OPEN_EXTERNAL,
    IPC_SELECT_AUDIO_FILE,
    IPC_SELECT_OUTPUT_FOLDER,
    IPC_SAVE_TRANSCRIPT,
    IPC_SAVE_AUDIO_FILE,
    IPC_DELETE_TEMP_FILE,
    IPC_COPY_FILE,
    IPC_SELECT_RECORDING_FOLDER,
    IPC_SAVE_RECORDING_OUTPUT,
    IPC_GET_APP_INFO,
    IPC_CHECK_FOR_UPDATES,
    IPC_DOWNLOAD_UPDATE,
    IPC_INSTALL_UPDATE,
    BACKEND_URL,
    BACKEND_PORT,
    EVENT_DOWNLOAD_PROGRESS
} = require('../shared/constants.js');

/**
 * Register all IPC handlers
 * @param {object} dependencies - Dependencies object containing all needed functions and state
 */
function registerIpcHandlers(dependencies) {
    const {
        getSettings,
        updateSettings,
        saveSettings,
        getUserDataPath,
        getModelsPath,
        getLogsPath,
        backendRequest,
        getRecordingState,
        startRecording,
        stopRecording,
        showTranscribingOverlay,
        hideOverlay,
        getOverlayState,
        registerWindowToggleHotkey,
        registerToggleRecordingHotkey,
        getMainWindow,
        getAutoUpdater
    } = dependencies;

    // Settings handlers
    ipcMain.handle(IPC_GET_SETTINGS, () => getSettings());

    ipcMain.handle(IPC_SAVE_SETTINGS, (event, newSettings) => {
        const currentSettings = getSettings();
        const windowToggleHotkeyChanged = newSettings.windowToggleHotkey &&
            newSettings.windowToggleHotkey !== currentSettings.windowToggleHotkey;
        const toggleRecordingHotkeyChanged = newSettings.toggleRecordingHotkey &&
            newSettings.toggleRecordingHotkey !== currentSettings.toggleRecordingHotkey;

        updateSettings(newSettings);
        const saved = saveSettings();

        // Re-register window toggle hotkey if it changed
        if (windowToggleHotkeyChanged) {
            registerWindowToggleHotkey();
        }

        // Re-register toggle recording hotkey if it changed
        if (toggleRecordingHotkeyChanged) {
            registerToggleRecordingHotkey();
        }

        return saved;
    });

    // Recording control from renderer
    ipcMain.handle(IPC_START_RECORDING_MANUAL, () => {
        startRecording();
        return { success: true };
    });

    ipcMain.handle(IPC_STOP_RECORDING_MANUAL, () => {
        stopRecording();
        return { success: true };
    });

    ipcMain.handle(IPC_GET_RECORDING_STATE, () => {
        return { isRecording: getRecordingState() };
    });

    // Overlay state management for transcribing feedback
    ipcMain.handle(IPC_SHOW_TRANSCRIBING_OVERLAY, () => {
        showTranscribingOverlay();
        return { success: true };
    });

    ipcMain.handle(IPC_HIDE_OVERLAY, () => {
        hideOverlay();
        return { success: true };
    });

    ipcMain.handle(IPC_GET_OVERLAY_STATE, () => {
        return { state: getOverlayState() };
    });

    // Backend communication
    ipcMain.handle(IPC_BACKEND_HEALTH, async () => {
        try {
            const result = await backendRequest('GET', '/api/health');
            return { success: true, data: result };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_GET_DEVICES, async () => {
        try {
            const result = await backendRequest('GET', '/api/devices');
            return { success: true, data: result };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_GET_MODELS, async () => {
        try {
            const result = await backendRequest('GET', '/api/models');
            return { success: true, data: result };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_DOWNLOAD_MODEL, async (event, modelName) => {
        const mainWindow = getMainWindow();
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
                                    mainWindow.webContents.send(EVENT_DOWNLOAD_PROGRESS, data);
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

    ipcMain.handle(IPC_LOAD_MODEL, async (event, modelName) => {
        try {
            await backendRequest('POST', `/api/models/${modelName}/load`);
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_DELETE_MODEL, async (event, modelName) => {
        try {
            await backendRequest('DELETE', `/api/models/${modelName}`);
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    // Transcription
    ipcMain.handle(IPC_TRANSCRIBE, async (event, audioPath, language) => {
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
    ipcMain.handle(IPC_COPY_TO_CLIPBOARD, (event, text) => {
        clipboard.writeText(text);
        return true;
    });

    // Shell
    ipcMain.handle(IPC_OPEN_PATH, (event, pathToOpen) => {
        shell.openPath(pathToOpen);
    });

    ipcMain.handle(IPC_OPEN_EXTERNAL, (event, url) => {
        shell.openExternal(url);
    });

    // File dialogs
    ipcMain.handle(IPC_SELECT_AUDIO_FILE, async () => {
        const mainWindow = getMainWindow();
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

    ipcMain.handle(IPC_SELECT_OUTPUT_FOLDER, async () => {
        const mainWindow = getMainWindow();
        const result = await dialog.showOpenDialog(mainWindow, {
            title: 'Select Output Folder',
            properties: ['openDirectory', 'createDirectory']
        });

        if (result.canceled || result.filePaths.length === 0) {
            return { success: false, canceled: true };
        }

        return { success: true, folderPath: result.filePaths[0] };
    });

    ipcMain.handle(IPC_SAVE_TRANSCRIPT, async (event, filePath, content) => {
        try {
            fs.writeFileSync(filePath, content, 'utf8');
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_SAVE_AUDIO_FILE, async (event, audioData) => {
        try {
            const userDataPath = getUserDataPath();
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

    ipcMain.handle(IPC_DELETE_TEMP_FILE, async (event, filePath) => {
        try {
            if (fs.existsSync(filePath) && filePath.includes('temp')) {
                fs.unlinkSync(filePath);
            }
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_COPY_FILE, async (event, sourcePath, destFolder) => {
        try {
            const fileName = path.basename(sourcePath);
            const destPath = path.join(destFolder, fileName);
            fs.copyFileSync(sourcePath, destPath);
            return { success: true, destPath: destPath };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_SELECT_RECORDING_FOLDER, async () => {
        const mainWindow = getMainWindow();
        const result = await dialog.showOpenDialog(mainWindow, {
            title: 'Select Recording Output Folder',
            properties: ['openDirectory', 'createDirectory']
        });

        if (result.canceled || result.filePaths.length === 0) {
            return { success: false, canceled: true };
        }

        return { success: true, folderPath: result.filePaths[0] };
    });

    ipcMain.handle(IPC_SAVE_RECORDING_OUTPUT, async (event, options) => {
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
    ipcMain.handle(IPC_GET_APP_INFO, () => ({
        version: app.getVersion(),
        userDataPath: getUserDataPath(),
        modelsPath: getModelsPath(),
        logsPath: getLogsPath()
    }));

    // Auto-updater IPC handlers
    ipcMain.handle(IPC_CHECK_FOR_UPDATES, async () => {
        const autoUpdater = getAutoUpdater();
        if (!autoUpdater) {
            return { available: false, error: 'Auto-updater not available in development mode' };
        }
        try {
            const result = await autoUpdater.checkForUpdates();

            // Check if there's actually a newer version available
            // result.updateInfo contains the remote version information
            if (result && result.updateInfo) {
                const currentVersion = app.getVersion();
                const remoteVersion = result.updateInfo.version;

                // Use semver to properly compare versions
                // gt() returns true if remoteVersion > currentVersion
                const isNewer = semver.gt(remoteVersion, currentVersion);

                return {
                    available: isNewer,
                    updateInfo: result.updateInfo
                };
            }

            // No update info returned - no updates available
            return { available: false };
        } catch (error) {
            // Handle "no published versions" error - this means no releases exist yet
            // This is NOT a configuration error - user is on the latest version
            if (error.code === 'ERR_UPDATER_NO_PUBLISHED_VERSIONS' ||
                (error.message && error.message.includes('No published versions'))) {
                return { available: false };  // No error, just no updates available
            }

            // Handle missing configuration or file not found errors
            // Check for common error indicators across platforms and versions
            const isConfigError = error.code === 'ENOENT' ||
                (error.message && (
                    error.message.includes('ENOENT') ||
                    error.message.includes('app-update') ||
                    error.message.includes('no such file') ||
                    error.message.includes('Cannot find')
                ));
            if (isConfigError) {
                return { available: false, error: 'Update configuration not found. This may be a development or portable build.' };
            }
            return { available: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_DOWNLOAD_UPDATE, async () => {
        const autoUpdater = getAutoUpdater();
        if (!autoUpdater) {
            return { success: false, error: 'Auto-updater not available' };
        }
        try {
            await autoUpdater.downloadUpdate();
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    });

    ipcMain.handle(IPC_INSTALL_UPDATE, () => {
        const autoUpdater = getAutoUpdater();
        if (autoUpdater) {
            autoUpdater.quitAndInstall();
        }
    });
}

module.exports = {
    registerIpcHandlers
};
