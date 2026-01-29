/**
 * VoxTether Settings Module
 *
 * Handles loading, saving, and applying settings.
 */

import { getSettings, setSettings, updateSettings as updateState } from './state.js';
import { showNotification } from './notifications.js';
import { applyTheme } from './theme.js';

/**
 * Load settings from main process
 */
export async function loadSettings() {
    try {
        const settings = await window.voxtether.getSettings();
        setSettings(settings);
        applySettingsToUI();
    } catch (error) {
        console.error('Failed to load settings:', error);
        showNotification('Failed to load settings', 'error');
    }
}

/**
 * Apply current settings to UI elements
 */
export function applySettingsToUI() {
    const settings = getSettings();

    // General settings
    const hotkeyInput = document.getElementById('hotkey-input');
    if (hotkeyInput) hotkeyInput.value = settings.hotkey || 'Ctrl+Shift+Space';

    const windowToggleInput = document.getElementById('window-toggle-hotkey-input');
    if (windowToggleInput) windowToggleInput.value = settings.windowToggleHotkey || 'Ctrl+Shift+V';

    const languageSelect = document.getElementById('language-select');
    if (languageSelect) languageSelect.value = settings.language || 'auto';

    const outputModeSelect = document.getElementById('output-mode-select');
    if (outputModeSelect) outputModeSelect.value = settings.outputMode || 'ClipboardAndPaste';

    const notificationsToggle = document.getElementById('notifications-toggle');
    if (notificationsToggle) notificationsToggle.checked = settings.showNotifications !== false;

    const recordingIndicatorToggle = document.getElementById('recording-indicator-toggle');
    if (recordingIndicatorToggle) recordingIndicatorToggle.checked = settings.showRecordingIndicator !== false;

    const transcriptionPreviewToggle = document.getElementById('transcription-preview-toggle');
    if (transcriptionPreviewToggle) transcriptionPreviewToggle.checked = settings.showTranscriptionPreview === true;

    const startWithWindowsToggle = document.getElementById('start-with-windows-toggle');
    if (startWithWindowsToggle) startWithWindowsToggle.checked = settings.startWithWindows === true;

    const startMinimizedToggle = document.getElementById('start-minimized-toggle');
    if (startMinimizedToggle) startMinimizedToggle.checked = settings.startMinimized !== false;

    const themeSelect = document.getElementById('theme-select');
    if (themeSelect) themeSelect.value = settings.theme || 'system';

    // Recording output settings
    const recordingOutputFolder = document.getElementById('recording-output-folder');
    if (recordingOutputFolder) recordingOutputFolder.value = settings.recordingOutputFolder || '';

    const saveRecordingAudioToggle = document.getElementById('save-recording-audio-toggle');
    if (saveRecordingAudioToggle) saveRecordingAudioToggle.checked = settings.saveRecordingAudio === true;

    const saveRecordingTranscriptToggle = document.getElementById('save-recording-transcript-toggle');
    if (saveRecordingTranscriptToggle) saveRecordingTranscriptToggle.checked = settings.saveRecordingTranscript === true;

    // Audio settings
    const clipboardDelayInput = document.getElementById('clipboard-delay-input');
    if (clipboardDelayInput) clipboardDelayInput.value = settings.clipboardDelayMs || 50;

    const audioDeviceSelect = document.getElementById('audio-device-select');
    if (audioDeviceSelect) audioDeviceSelect.value = String(settings.audioDeviceId || -1);
}

/**
 * Save settings to main process
 * @param {Object} newSettings - Settings to save
 * @returns {Promise<boolean>}
 */
export async function saveSettings(newSettings) {
    try {
        updateState(newSettings);
        const settings = getSettings();
        const success = await window.voxtether.saveSettings(settings);
        if (success) {
            showNotification('Settings saved successfully', 'success');
            applyTheme(settings.theme);
        } else {
            showNotification('Failed to save settings', 'error');
        }
        return success;
    } catch (error) {
        console.error('Failed to save settings:', error);
        showNotification('Failed to save settings', 'error');
        return false;
    }
}

/**
 * Helper to get element value safely
 * @param {string} id - Element ID
 * @param {string} defaultValue - Default value if element not found
 * @returns {string}
 */
function getElementValue(id, defaultValue = '') {
    const el = document.getElementById(id);
    return el ? el.value : defaultValue;
}

/**
 * Helper to get element checked state safely
 * @param {string} id - Element ID
 * @param {boolean} defaultValue - Default value if element not found
 * @returns {boolean}
 */
function getElementChecked(id, defaultValue = false) {
    const el = document.getElementById(id);
    return el ? el.checked : defaultValue;
}

/**
 * Save general settings from the UI
 */
export async function saveGeneralSettings() {
    const newSettings = {
        hotkey: getElementValue('hotkey-input', 'Ctrl+Shift+Space'),
        windowToggleHotkey: getElementValue('window-toggle-hotkey-input', 'Ctrl+Shift+V'),
        language: getElementValue('language-select', 'auto'),
        outputMode: getElementValue('output-mode-select', 'ClipboardAndPaste'),
        showNotifications: getElementChecked('notifications-toggle', true),
        showRecordingIndicator: getElementChecked('recording-indicator-toggle', true),
        showTranscriptionPreview: getElementChecked('transcription-preview-toggle', false),
        startWithWindows: getElementChecked('start-with-windows-toggle', false),
        startMinimized: getElementChecked('start-minimized-toggle', true),
        theme: getElementValue('theme-select', 'system'),
        recordingOutputFolder: getElementValue('recording-output-folder', ''),
        saveRecordingAudio: getElementChecked('save-recording-audio-toggle', false),
        saveRecordingTranscript: getElementChecked('save-recording-transcript-toggle', false)
    };

    await saveSettings(newSettings);
}

/**
 * Save audio settings from the UI
 */
export async function saveAudioSettings() {
    const newSettings = {
        clipboardDelayMs: parseInt(getElementValue('clipboard-delay-input', '50')) || 50,
        audioDeviceId: parseInt(getElementValue('audio-device-select', '-1'))
    };

    await saveSettings(newSettings);
}

/**
 * Select recording output folder
 */
export async function selectRecordingFolder() {
    try {
        const result = await window.voxtether.selectRecordingFolder();
        if (result && result.success && result.folderPath) {
            document.getElementById('recording-output-folder').value = result.folderPath;
        }
    } catch (error) {
        console.error('Failed to select folder:', error);
        showNotification('Failed to select folder', 'error');
    }
}

/**
 * Clear recording output folder
 */
export function clearRecordingFolder() {
    const el = document.getElementById('recording-output-folder');
    if (el) el.value = '';
}
