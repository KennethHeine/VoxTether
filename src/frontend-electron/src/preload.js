/**
 * VoxTether Electron - Preload Script
 *
 * This script runs in the renderer process before the web page loads.
 * It exposes a safe subset of Electron APIs to the renderer via contextBridge.
 *
 * IMPORTANT: All IPC channel names are imported from shared/constants.js.
 * When adding a new IPC channel, add the constant to constants.js first,
 * then use it here and in ipc-handlers.js. This ensures the renderer,
 * preload, and main process always use the same channel names.
 */

const { contextBridge, ipcRenderer } = require('electron');
const {
    // IPC Channel Names (Invoke/Handle) - used with ipcRenderer.invoke()
    IPC_GET_SETTINGS,
    IPC_SAVE_SETTINGS,
    IPC_BACKEND_HEALTH,
    IPC_GET_DEVICES,
    IPC_GET_MODELS,
    IPC_DOWNLOAD_MODEL,
    IPC_LOAD_MODEL,
    IPC_DELETE_MODEL,
    IPC_TRANSCRIBE,
    IPC_TEST_OPENAI_CONNECTION,
    IPC_TEST_AZURE_CONNECTION,
    IPC_START_RECORDING_MANUAL,
    IPC_STOP_RECORDING_MANUAL,
    IPC_GET_RECORDING_STATE,
    IPC_SHOW_TRANSCRIBING_OVERLAY,
    IPC_HIDE_OVERLAY,
    IPC_GET_OVERLAY_STATE,
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
    // Event Channel Names (On/Send) - used with ipcRenderer.on()
    EVENT_DOWNLOAD_PROGRESS,
    EVENT_TEST_MICROPHONE,
    EVENT_RECORDING_STATE_CHANGED,
    EVENT_STATUS_CHANGED,
    EVENT_START_RECORDING,
    EVENT_STOP_RECORDING,
    EVENT_UPDATE_AVAILABLE,
    EVENT_UPDATE_DOWNLOAD_PROGRESS,
    EVENT_UPDATE_DOWNLOADED
} = require('./shared/constants.js');

// Expose protected methods that allow the renderer process to use
// ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('voxtether', {
    // Settings
    getSettings: () => ipcRenderer.invoke(IPC_GET_SETTINGS),
    saveSettings: (settings) => ipcRenderer.invoke(IPC_SAVE_SETTINGS, settings),

    // Backend communication
    backendHealth: () => ipcRenderer.invoke(IPC_BACKEND_HEALTH),
    getDevices: () => ipcRenderer.invoke(IPC_GET_DEVICES),
    getModels: () => ipcRenderer.invoke(IPC_GET_MODELS),
    downloadModel: (modelName) => ipcRenderer.invoke(IPC_DOWNLOAD_MODEL, modelName),
    loadModel: (modelName) => ipcRenderer.invoke(IPC_LOAD_MODEL, modelName),
    deleteModel: (modelName) => ipcRenderer.invoke(IPC_DELETE_MODEL, modelName),
    transcribe: (audioPath, language) => ipcRenderer.invoke(IPC_TRANSCRIBE, audioPath, language),

    // OpenAI API
    testOpenAIConnection: (apiKey) => ipcRenderer.invoke(IPC_TEST_OPENAI_CONNECTION, apiKey),

    // Azure Speech Services API
    testAzureConnection: (speechKey, speechRegion) => ipcRenderer.invoke(IPC_TEST_AZURE_CONNECTION, speechKey, speechRegion),

    // Recording control
    startRecordingManual: () => ipcRenderer.invoke(IPC_START_RECORDING_MANUAL),
    stopRecordingManual: () => ipcRenderer.invoke(IPC_STOP_RECORDING_MANUAL),
    getRecordingState: () => ipcRenderer.invoke(IPC_GET_RECORDING_STATE),

    // Overlay state management
    showTranscribingOverlay: () => ipcRenderer.invoke(IPC_SHOW_TRANSCRIBING_OVERLAY),
    hideOverlay: () => ipcRenderer.invoke(IPC_HIDE_OVERLAY),
    getOverlayState: () => ipcRenderer.invoke(IPC_GET_OVERLAY_STATE),

    // Clipboard
    copyToClipboard: (text) => ipcRenderer.invoke(IPC_COPY_TO_CLIPBOARD, text),

    // Shell
    openPath: (path) => ipcRenderer.invoke(IPC_OPEN_PATH, path),
    openExternal: (url) => ipcRenderer.invoke(IPC_OPEN_EXTERNAL, url),

    // File dialogs
    selectAudioFile: () => ipcRenderer.invoke(IPC_SELECT_AUDIO_FILE),
    selectOutputFolder: () => ipcRenderer.invoke(IPC_SELECT_OUTPUT_FOLDER),
    saveTranscript: (filePath, content) => ipcRenderer.invoke(IPC_SAVE_TRANSCRIPT, filePath, content),
    saveAudioFile: (audioData) => ipcRenderer.invoke(IPC_SAVE_AUDIO_FILE, audioData),
    deleteTempFile: (filePath) => ipcRenderer.invoke(IPC_DELETE_TEMP_FILE, filePath),
    copyFile: (sourcePath, destFolder) => ipcRenderer.invoke(IPC_COPY_FILE, sourcePath, destFolder),
    selectRecordingFolder: () => ipcRenderer.invoke(IPC_SELECT_RECORDING_FOLDER),
    saveRecordingOutput: (options) => ipcRenderer.invoke(IPC_SAVE_RECORDING_OUTPUT, options),

    // App info
    getAppInfo: () => ipcRenderer.invoke(IPC_GET_APP_INFO),

    // Events from main process
    // Each listener function returns a cleanup function to remove the listener
    onDownloadProgress: (callback) => {
        const handler = (_event, data) => callback(data);
        ipcRenderer.on(EVENT_DOWNLOAD_PROGRESS, handler);
        return () => ipcRenderer.removeListener(EVENT_DOWNLOAD_PROGRESS, handler);
    },
    onTestMicrophone: (callback) => {
        const handler = () => callback();
        ipcRenderer.on(EVENT_TEST_MICROPHONE, handler);
        return () => ipcRenderer.removeListener(EVENT_TEST_MICROPHONE, handler);
    },
    onRecordingStateChanged: (callback) => {
        const handler = (_event, isRecording) => callback(isRecording);
        ipcRenderer.on(EVENT_RECORDING_STATE_CHANGED, handler);
        return () => ipcRenderer.removeListener(EVENT_RECORDING_STATE_CHANGED, handler);
    },
    onStatusChanged: (callback) => {
        const handler = (_event, status) => callback(status);
        ipcRenderer.on(EVENT_STATUS_CHANGED, handler);
        return () => ipcRenderer.removeListener(EVENT_STATUS_CHANGED, handler);
    },
    onStartRecording: (callback) => {
        const handler = () => callback();
        ipcRenderer.on(EVENT_START_RECORDING, handler);
        return () => ipcRenderer.removeListener(EVENT_START_RECORDING, handler);
    },
    onStopRecording: (callback) => {
        const handler = () => callback();
        ipcRenderer.on(EVENT_STOP_RECORDING, handler);
        return () => ipcRenderer.removeListener(EVENT_STOP_RECORDING, handler);
    },

    // Auto-updater
    checkForUpdates: () => ipcRenderer.invoke(IPC_CHECK_FOR_UPDATES),
    downloadUpdate: () => ipcRenderer.invoke(IPC_DOWNLOAD_UPDATE),
    installUpdate: () => ipcRenderer.invoke(IPC_INSTALL_UPDATE),
    onUpdateAvailable: (callback) => {
        const handler = (_event, info) => callback(info);
        ipcRenderer.on(EVENT_UPDATE_AVAILABLE, handler);
        return () => ipcRenderer.removeListener(EVENT_UPDATE_AVAILABLE, handler);
    },
    onUpdateDownloadProgress: (callback) => {
        const handler = (_event, progress) => callback(progress);
        ipcRenderer.on(EVENT_UPDATE_DOWNLOAD_PROGRESS, handler);
        return () => ipcRenderer.removeListener(EVENT_UPDATE_DOWNLOAD_PROGRESS, handler);
    },
    onUpdateDownloaded: (callback) => {
        const handler = (_event, info) => callback(info);
        ipcRenderer.on(EVENT_UPDATE_DOWNLOADED, handler);
        return () => ipcRenderer.removeListener(EVENT_UPDATE_DOWNLOADED, handler);
    },

    // Remove listeners (kept for backwards compatibility)
    removeAllListeners: (channel) => {
        ipcRenderer.removeAllListeners(channel);
    }
});

// Expose platform info
contextBridge.exposeInMainWorld('platform', {
    isWindows: process.platform === 'win32',
    isMac: process.platform === 'darwin',
    isLinux: process.platform === 'linux',
    platform: process.platform,
    arch: process.arch
});
