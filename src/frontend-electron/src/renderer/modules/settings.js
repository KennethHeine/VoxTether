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
 * Save general settings from the UI
 */
export async function saveGeneralSettings() {
    const newSettings = {
        hotkey: document.getElementById('hotkey-input').value,
        windowToggleHotkey: document.getElementById('window-toggle-hotkey-input').value,
        language: document.getElementById('language-select').value,
        outputMode: document.getElementById('output-mode-select').value,
        showNotifications: document.getElementById('notifications-toggle').checked,
        showRecordingIndicator: document.getElementById('recording-indicator-toggle').checked,
        showTranscriptionPreview: document.getElementById('transcription-preview-toggle').checked,
        startWithWindows: document.getElementById('start-with-windows-toggle').checked,
        startMinimized: document.getElementById('start-minimized-toggle').checked,
        theme: document.getElementById('theme-select').value,
        recordingOutputFolder: document.getElementById('recording-output-folder').value,
        saveRecordingAudio: document.getElementById('save-recording-audio-toggle').checked,
        saveRecordingTranscript: document.getElementById('save-recording-transcript-toggle').checked
    };

    await saveSettings(newSettings);
}

/**
 * Save audio settings from the UI
 */
export async function saveAudioSettings() {
    const newSettings = {
        clipboardDelayMs: parseInt(document.getElementById('clipboard-delay-input').value) || 50,
        audioDeviceId: parseInt(document.getElementById('audio-device-select').value)
    };

    await saveSettings(newSettings);
}

/**
 * Select recording output folder
 */
export async function selectRecordingFolder() {
    try {
        const result = await window.voxtether.selectRecordingFolder();
        if (result && result.path) {
            document.getElementById('recording-output-folder').value = result.path;
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
    document.getElementById('recording-output-folder').value = '';
}
