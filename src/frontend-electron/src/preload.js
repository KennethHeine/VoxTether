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

    // App info
    getAppInfo: () => ipcRenderer.invoke('get-app-info'),

    // Events from main process
    onDownloadProgress: (callback) => {
        ipcRenderer.on('download-progress', (event, data) => callback(data));
    },
    onTestMicrophone: (callback) => {
        ipcRenderer.on('test-microphone', () => callback());
    },
    onRecordingStateChanged: (callback) => {
        ipcRenderer.on('recording-state-changed', (event, isRecording) => callback(isRecording));
    },
    onStatusChanged: (callback) => {
        ipcRenderer.on('status-changed', (event, status) => callback(status));
    },
    onStartRecording: (callback) => {
        ipcRenderer.on('start-recording', () => callback());
    },
    onStopRecording: (callback) => {
        ipcRenderer.on('stop-recording', () => callback());
    },

    // Remove listeners
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
