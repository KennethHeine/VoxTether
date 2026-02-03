/**
 * VoxTether Electron - Preload Script
 *
 * This script runs in the renderer process before the web page loads.
 * It exposes a safe subset of Electron APIs to the renderer via contextBridge.
 */

const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('voxtether', {
    // Settings
    getSettings: () => ipcRenderer.invoke('get-settings'),
    saveSettings: (settings) => ipcRenderer.invoke('save-settings', settings),

    // Backend communication
    backendHealth: () => ipcRenderer.invoke('backend-health'),
    getDevices: () => ipcRenderer.invoke('get-devices'),
    getModels: () => ipcRenderer.invoke('get-models'),
    downloadModel: (modelName) => ipcRenderer.invoke('download-model', modelName),
    loadModel: (modelName) => ipcRenderer.invoke('load-model', modelName),
    deleteModel: (modelName) => ipcRenderer.invoke('delete-model', modelName),
    transcribe: (audioPath, language) => ipcRenderer.invoke('transcribe', audioPath, language),

    // Recording control
    startRecordingManual: () => ipcRenderer.invoke('start-recording-manual'),
    stopRecordingManual: () => ipcRenderer.invoke('stop-recording-manual'),
    getRecordingState: () => ipcRenderer.invoke('get-recording-state'),

    // Overlay state management
    showTranscribingOverlay: () => ipcRenderer.invoke('show-transcribing-overlay'),
    hideOverlay: () => ipcRenderer.invoke('hide-overlay'),
    getOverlayState: () => ipcRenderer.invoke('get-overlay-state'),

    // Clipboard
    copyToClipboard: (text) => ipcRenderer.invoke('copy-to-clipboard', text),

    // Shell
    openPath: (path) => ipcRenderer.invoke('open-path', path),
    openExternal: (url) => ipcRenderer.invoke('open-external', url),

    // File dialogs
    selectAudioFile: () => ipcRenderer.invoke('select-audio-file'),
    selectOutputFolder: () => ipcRenderer.invoke('select-output-folder'),
    saveTranscript: (filePath, content) => ipcRenderer.invoke('save-transcript', filePath, content),
    saveAudioFile: (audioData) => ipcRenderer.invoke('save-audio-file', audioData),
    deleteTempFile: (filePath) => ipcRenderer.invoke('delete-temp-file', filePath),
    copyFile: (sourcePath, destFolder) => ipcRenderer.invoke('copy-file', sourcePath, destFolder),
    selectRecordingFolder: () => ipcRenderer.invoke('select-recording-folder'),
    saveRecordingOutput: (options) => ipcRenderer.invoke('save-recording-output', options),

    // App info
    getAppInfo: () => ipcRenderer.invoke('get-app-info'),

    // Events from main process
    // Each listener function returns a cleanup function to remove the listener
    onDownloadProgress: (callback) => {
        const handler = (event, data) => callback(data);
        ipcRenderer.on('download-progress', handler);
        return () => ipcRenderer.removeListener('download-progress', handler);
    },
    onTestMicrophone: (callback) => {
        const handler = () => callback();
        ipcRenderer.on('test-microphone', handler);
        return () => ipcRenderer.removeListener('test-microphone', handler);
    },
    onRecordingStateChanged: (callback) => {
        const handler = (event, isRecording) => callback(isRecording);
        ipcRenderer.on('recording-state-changed', handler);
        return () => ipcRenderer.removeListener('recording-state-changed', handler);
    },
    onStatusChanged: (callback) => {
        const handler = (event, status) => callback(status);
        ipcRenderer.on('status-changed', handler);
        return () => ipcRenderer.removeListener('status-changed', handler);
    },
    onStartRecording: (callback) => {
        const handler = () => callback();
        ipcRenderer.on('start-recording', handler);
        return () => ipcRenderer.removeListener('start-recording', handler);
    },
    onStopRecording: (callback) => {
        const handler = () => callback();
        ipcRenderer.on('stop-recording', handler);
        return () => ipcRenderer.removeListener('stop-recording', handler);
    },

    // Auto-updater (Feature 18)
    checkForUpdates: () => ipcRenderer.invoke('check-for-updates'),
    downloadUpdate: () => ipcRenderer.invoke('download-update'),
    installUpdate: () => ipcRenderer.invoke('install-update'),
    onUpdateAvailable: (callback) => {
        const handler = (event, info) => callback(info);
        ipcRenderer.on('update-available', handler);
        return () => ipcRenderer.removeListener('update-available', handler);
    },
    onUpdateDownloadProgress: (callback) => {
        const handler = (event, progress) => callback(progress);
        ipcRenderer.on('update-download-progress', handler);
        return () => ipcRenderer.removeListener('update-download-progress', handler);
    },
    onUpdateDownloaded: (callback) => {
        const handler = (event, info) => callback(info);
        ipcRenderer.on('update-downloaded', handler);
        return () => ipcRenderer.removeListener('update-downloaded', handler);
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
