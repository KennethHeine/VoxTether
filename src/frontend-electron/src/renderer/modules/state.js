/**
 * VoxTether State Manager
 *
 * Centralized state management for the application.
 * Provides getters, setters, and event-based state change notifications.
 */

// Model information (static data)
export const MODEL_INFO = {
    tiny: { name: 'tiny', displayName: 'Tiny', sizeMb: 75, description: 'Quick notes, low-resource systems' },
    base: { name: 'base', displayName: 'Base', sizeMb: 142, description: 'General use' },
    small: { name: 'small', displayName: 'Small', sizeMb: 466, description: 'Recommended for most users' },
    medium: { name: 'medium', displayName: 'Medium', sizeMb: 1500, description: 'When accuracy is important' },
    'large-v3': { name: 'large-v3', displayName: 'Large v3', sizeMb: 3000, description: 'When accuracy is critical' },
    'large-v3-turbo': { name: 'large-v3-turbo', displayName: 'Large v3 Turbo', sizeMb: 1600, description: 'Best balance of speed and accuracy' },
    'distil-large-v3': { name: 'distil-large-v3', displayName: 'Distil Large v3', sizeMb: 1100, description: 'Fast high-quality transcription' }
};

// History constants
export const HISTORY_STORAGE_KEY = 'voxtether_history';
export const MAX_HISTORY_ITEMS = 50;
export const STATS_STORAGE_KEY = 'voxtether_stats';

// Application state - internal storage
const state = {
    settings: {},
    isCapturingHotkey: false,
    capturingHotkeyType: null, // 'windowToggle' or 'toggleRecording'

    // Recording state
    recording: {
        isRecording: false,
        mediaRecorder: null,
        audioChunks: [],
        stream: null,
        startTime: null,
        // Audio level monitoring
        audioContext: null,
        analyser: null,
        audioData: null,
        levelAnimationId: null
    },

    // Mic test state
    micTest: {
        isRunning: false,
        stream: null,
        audioContext: null,
        analyser: null,
        animationId: null,
        peakLevel: 0,
        audioData: null,
        // Cached DOM elements for animation loop performance
        elements: {
            volumeBar: null,
            volumePeak: null,
            peakLabel: null,
            canvas: null,
            canvasCtx: null
        }
    },

    // History
    historyItems: [],

    // Statistics
    statistics: {
        totalRecordings: 0,
        totalDurationMs: 0,
        totalCharacters: 0,
        lastRecordingDate: null
    },

    // Update info
    pendingUpdateInfo: null,
    pendingPreviewDuration: 0
};

// Event listeners for state changes
const listeners = new Map();

/**
 * Subscribe to state changes
 * @param {string} key - State key to watch
 * @param {Function} callback - Callback function
 * @returns {Function} Unsubscribe function
 */
export function subscribe(key, callback) {
    if (!listeners.has(key)) {
        listeners.set(key, new Set());
    }
    listeners.get(key).add(callback);
    return () => listeners.get(key).delete(callback);
}

/**
 * Notify listeners of state change
 * @param {string} key - State key that changed
 * @param {*} value - New value
 */
function notify(key, value) {
    if (listeners.has(key)) {
        listeners.get(key).forEach(cb => cb(value));
    }
}

// Settings
export function getSettings() {
    return { ...state.settings };
}

export function setSettings(newSettings) {
    state.settings = newSettings;
    notify('settings', state.settings);
}

export function updateSettings(partialSettings) {
    Object.assign(state.settings, partialSettings);
    notify('settings', state.settings);
}

// Hotkey capture state
export function isCapturingHotkey() {
    return state.isCapturingHotkey;
}

export function setCapturingHotkey(capturing, type = null) {
    state.isCapturingHotkey = capturing;
    state.capturingHotkeyType = type;
}

export function getCapturingHotkeyType() {
    return state.capturingHotkeyType;
}

// Recording state
// NOTE: Returns direct reference because recording state contains live objects
// (MediaRecorder, Stream, AudioContext) that cannot be shallow-copied.
// Use setRecordingState() for all modifications to ensure proper notifications.
export function getRecordingState() {
    return state.recording;
}

export function setRecordingState(updates) {
    Object.assign(state.recording, updates);
    notify('recording', state.recording);
}

export function isRecording() {
    return state.recording.isRecording;
}

// Mic test state
// NOTE: Returns direct reference because mic test state contains live objects
// (Stream, AudioContext, AnalyserNode) that cannot be shallow-copied.
// Use setMicTestState() for all modifications.
export function getMicTestState() {
    return state.micTest;
}

export function setMicTestState(updates) {
    Object.assign(state.micTest, updates);
}

// History
export function getHistoryItems() {
    return [...state.historyItems];
}

export function setHistoryItems(items) {
    state.historyItems = items;
    notify('history', state.historyItems);
}

export function addHistoryItem(item) {
    state.historyItems.unshift(item);
    // Trim to max items
    if (state.historyItems.length > MAX_HISTORY_ITEMS) {
        state.historyItems = state.historyItems.slice(0, MAX_HISTORY_ITEMS);
    }
    notify('history', state.historyItems);
}

export function removeHistoryItem(id) {
    state.historyItems = state.historyItems.filter(item => item.id !== id);
    notify('history', state.historyItems);
}

export function clearHistoryItems() {
    state.historyItems = [];
    notify('history', state.historyItems);
}

// Statistics
export function getStatistics() {
    return { ...state.statistics };
}

export function setStatistics(stats) {
    state.statistics = { ...state.statistics, ...stats };
    notify('statistics', state.statistics);
}

export function updateStatisticsValues(durationMs, characterCount) {
    state.statistics.totalRecordings += 1;
    state.statistics.totalDurationMs += durationMs;
    state.statistics.totalCharacters += characterCount;
    state.statistics.lastRecordingDate = new Date().toISOString();
    notify('statistics', state.statistics);
}

export function resetStatisticsValues() {
    state.statistics = {
        totalRecordings: 0,
        totalDurationMs: 0,
        totalCharacters: 0,
        lastRecordingDate: null
    };
    notify('statistics', state.statistics);
}

// Update info
export function getPendingUpdateInfo() {
    return state.pendingUpdateInfo;
}

export function setPendingUpdateInfo(info) {
    state.pendingUpdateInfo = info;
}

// Preview duration
export function getPendingPreviewDuration() {
    return state.pendingPreviewDuration;
}

export function setPendingPreviewDuration(duration) {
    state.pendingPreviewDuration = duration;
}
