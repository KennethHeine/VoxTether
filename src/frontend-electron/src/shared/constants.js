/**
 * VoxTether Electron - Shared Constants
 *
 * Single source of truth for IPC channel names, events, and configuration.
 * Used by main process (via require), preload script (via require), and
 * renderer process (via import) to ensure consistency.
 *
 * ## Module Format
 * This file uses ES module `export` syntax but is compatible with both
 * `require()` (CommonJS, used by main/preload) and `import` (ESM, used by renderer).
 *
 * ## Naming Conventions
 * - `IPC_*`    → Invoke/Handle channels (renderer calls main via ipcRenderer.invoke)
 *               Registered in: ipc-handlers.js (ipcMain.handle)
 *               Called from:   preload.js (ipcRenderer.invoke)
 * - `EVENT_*`  → One-way event channels (main sends to renderer via webContents.send)
 *               Sent from:     main process modules (e.g., recording.js, updater.js)
 *               Listened in:   preload.js (ipcRenderer.on)
 *
 * ## How to Add a New IPC Channel
 * 1. Add the constant here with the appropriate prefix (IPC_ or EVENT_)
 * 2. For IPC_ channels: add handler in ipc-handlers.js, add bridge method in preload.js
 * 3. For EVENT_ channels: send from main process, add listener in preload.js
 * 4. The renderer accesses everything through window.voxtether (defined in preload.js)
 *
 * ## File Dependencies
 * This file is imported by:
 * - src/main/index.js, ipc-handlers.js, backend-client.js, recording.js,
 *   overlay.js, settings-manager.js, updater.js (via require)
 * - src/preload.js (via require)
 * - src/renderer/modules/* (via import, indirectly through preload bridge)
 */

// ============================================================================
// IPC Channel Names (Invoke/Handle)
// Used with ipcMain.handle() in ipc-handlers.js and ipcRenderer.invoke() in preload.js
// ============================================================================

// Settings
export const IPC_GET_SETTINGS = 'get-settings';
export const IPC_SAVE_SETTINGS = 'save-settings';

// Backend communication
export const IPC_BACKEND_HEALTH = 'backend-health';
export const IPC_GET_DEVICES = 'get-devices';
export const IPC_GET_MODELS = 'get-models';
export const IPC_DOWNLOAD_MODEL = 'download-model';
export const IPC_LOAD_MODEL = 'load-model';
export const IPC_DELETE_MODEL = 'delete-model';
export const IPC_TRANSCRIBE = 'transcribe';
export const IPC_TEST_OPENAI_CONNECTION = 'test-openai-connection';
export const IPC_TEST_AZURE_CONNECTION = 'test-azure-connection';

// Recording control
export const IPC_START_RECORDING_MANUAL = 'start-recording-manual';
export const IPC_STOP_RECORDING_MANUAL = 'stop-recording-manual';
export const IPC_GET_RECORDING_STATE = 'get-recording-state';

// Overlay state management
export const IPC_SHOW_TRANSCRIBING_OVERLAY = 'show-transcribing-overlay';
export const IPC_HIDE_OVERLAY = 'hide-overlay';
export const IPC_GET_OVERLAY_STATE = 'get-overlay-state';

// Clipboard
export const IPC_COPY_TO_CLIPBOARD = 'copy-to-clipboard';

// Shell
export const IPC_OPEN_PATH = 'open-path';
export const IPC_OPEN_EXTERNAL = 'open-external';

// File dialogs
export const IPC_SELECT_AUDIO_FILE = 'select-audio-file';
export const IPC_SELECT_OUTPUT_FOLDER = 'select-output-folder';
export const IPC_SAVE_TRANSCRIPT = 'save-transcript';
export const IPC_SAVE_AUDIO_FILE = 'save-audio-file';
export const IPC_DELETE_TEMP_FILE = 'delete-temp-file';
export const IPC_COPY_FILE = 'copy-file';
export const IPC_SELECT_RECORDING_FOLDER = 'select-recording-folder';
export const IPC_SAVE_RECORDING_OUTPUT = 'save-recording-output';

// App info
export const IPC_GET_APP_INFO = 'get-app-info';

// Auto-updater
export const IPC_CHECK_FOR_UPDATES = 'check-for-updates';
export const IPC_DOWNLOAD_UPDATE = 'download-update';
export const IPC_INSTALL_UPDATE = 'install-update';

// ============================================================================
// Event Channel Names (On/Send)
// Sent from main process via webContents.send(), received in preload.js via ipcRenderer.on()
// ============================================================================

// Download progress
export const EVENT_DOWNLOAD_PROGRESS = 'download-progress';

// Microphone test
export const EVENT_TEST_MICROPHONE = 'test-microphone';

// Recording state changes
export const EVENT_RECORDING_STATE_CHANGED = 'recording-state-changed';
export const EVENT_STATUS_CHANGED = 'status-changed';
export const EVENT_START_RECORDING = 'start-recording';
export const EVENT_STOP_RECORDING = 'stop-recording';

// Auto-updater events
export const EVENT_UPDATE_AVAILABLE = 'update-available';
export const EVENT_UPDATE_DOWNLOAD_PROGRESS = 'update-download-progress';
export const EVENT_UPDATE_DOWNLOADED = 'update-downloaded';

// ============================================================================
// Backend Configuration
// The Python FastAPI backend runs separately (see github.com/KennethHeine/VoxTether-backend)
// ============================================================================

export const BACKEND_PORT = 5678;
export const BACKEND_HOST = '127.0.0.1';
export const BACKEND_URL = `http://${BACKEND_HOST}:${BACKEND_PORT}`;

// ============================================================================
// Default Settings
// Persisted to userData/settings.json by settings-manager.js
// All settings keys must be listed here with their default values
// ============================================================================

export const DEFAULT_SETTINGS = {
    windowToggleHotkey: 'Ctrl+Shift+V',
    toggleRecordingHotkey: 'Ctrl+Shift+R',
    modelName: 'large-v3-turbo',
    language: 'auto',
    outputMode: 'ClipboardAndPaste',
    showNotifications: true,
    showRecordingIndicator: true,
    audioDeviceId: -1,
    clipboardDelayMs: 50,
    firstRunCompleted: false,
    backendPort: BACKEND_PORT,
    backendHost: BACKEND_HOST,
    startMinimized: true,
    startWithWindows: false,
    theme: 'system',
    recordingOutputFolder: '',
    saveRecordingAudio: false,
    saveRecordingTranscript: false,
    showTranscriptionPreview: false,
    // Transcription Provider Settings
    transcriptionProvider: 'local',  // 'local', 'openai', or 'azure'
    openaiApiKey: '',                 // OpenAI API key
    openaiModel: 'whisper-1',         // OpenAI model to use
    azureSpeechKey: '',               // Azure Speech Services subscription key
    azureSpeechRegion: ''             // Azure Speech Services region (e.g., 'eastus')
};

// ============================================================================
// Overlay States
// Used by overlay.js for the recording indicator bar at the top of the screen
// ============================================================================

export const OVERLAY_STATE_HIDDEN = 'hidden';
export const OVERLAY_STATE_RECORDING = 'recording';
export const OVERLAY_STATE_TRANSCRIBING = 'transcribing';

export const VALID_OVERLAY_STATES = [
    OVERLAY_STATE_RECORDING,
    OVERLAY_STATE_TRANSCRIBING
];

// ============================================================================
// Valid Model Names
// Used by ipc-handlers.js to validate model name parameters before passing to backend
// ============================================================================

export const VALID_MODEL_NAMES = [
    'tiny',
    'base',
    'small',
    'medium',
    'large-v3',
    'large-v3-turbo',
    'distil-large-v3'
];

// ============================================================================
// Allowed External URLs (whitelist for security)
// Used by ipc-handlers.js to validate URLs before opening with shell.openExternal()
// ============================================================================

export const ALLOWED_EXTERNAL_URL_PATTERNS = [
    /^https:\/\/github\.com\/kennethsolberg\//i,
    /^https:\/\/github\.com\/[^/]+\/voxtether(?:\/|$)/i,
    /^https:\/\/platform\.openai\.com\//i,
    /^https:\/\/openai\.com\//i,
    /^https:\/\/azure\.microsoft\.com\//i,
    /^https:\/\/portal\.azure\.com\//i,
    /^https:\/\/learn\.microsoft\.com\//i
];
