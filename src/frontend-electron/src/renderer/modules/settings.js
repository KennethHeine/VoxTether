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
    const windowToggleInput = document.getElementById('window-toggle-hotkey-input');
    if (windowToggleInput) windowToggleInput.value = settings.windowToggleHotkey || 'Ctrl+Shift+V';

    const toggleRecordingInput = document.getElementById('toggle-recording-hotkey-input');
    if (toggleRecordingInput) toggleRecordingInput.value = settings.toggleRecordingHotkey || 'Ctrl+Shift+R';

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

    // Transcription Provider settings
    const transcriptionProviderSelect = document.getElementById('transcription-provider-select');
    if (transcriptionProviderSelect) {
        transcriptionProviderSelect.value = settings.transcriptionProvider || 'local';
        // Show/hide backend settings based on provider
        updateBackendSettingsVisibility(settings.transcriptionProvider || 'local');
    }

    const openaiApiKeyInput = document.getElementById('openai-api-key-input');
    if (openaiApiKeyInput) openaiApiKeyInput.value = settings.openaiApiKey || '';

    const openaiModelSelect = document.getElementById('openai-model-select');
    if (openaiModelSelect) openaiModelSelect.value = settings.openaiModel || 'whisper-1';
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
        windowToggleHotkey: getElementValue('window-toggle-hotkey-input', 'Ctrl+Shift+V'),
        toggleRecordingHotkey: getElementValue('toggle-recording-hotkey-input', 'Ctrl+Shift+R'),
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
        saveRecordingTranscript: getElementChecked('save-recording-transcript-toggle', false),
        // Transcription Provider settings
        transcriptionProvider: getElementValue('transcription-provider-select', 'local'),
        openaiApiKey: getElementValue('openai-api-key-input', ''),
        openaiModel: getElementValue('openai-model-select', 'whisper-1')
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

/**
 * Update backend settings visibility based on provider selection
 * @param {string} provider - The selected provider ('local' or 'openai')
 */
export function updateBackendSettingsVisibility(provider) {
    const localSettings = document.getElementById('local-backend-settings');
    const openaiSettings = document.getElementById('openai-backend-settings');

    if (localSettings) {
        localSettings.classList.toggle('hidden', provider !== 'local');
    }
    if (openaiSettings) {
        openaiSettings.classList.toggle('hidden', provider !== 'openai');
    }
}

/**
 * Toggle API key visibility
 */
export function toggleApiKeyVisibility() {
    const apiKeyInput = document.getElementById('openai-api-key-input');
    const toggleBtn = document.getElementById('toggle-api-key-visibility');

    if (apiKeyInput && toggleBtn) {
        if (apiKeyInput.type === 'password') {
            apiKeyInput.type = 'text';
            toggleBtn.textContent = 'Hide';
        } else {
            apiKeyInput.type = 'password';
            toggleBtn.textContent = 'Show';
        }
    }
}

/**
 * Test OpenAI API connection
 */
export async function testOpenAIConnection() {
    const apiKeyInput = document.getElementById('openai-api-key-input');
    const statusEl = document.getElementById('openai-test-status');

    if (!apiKeyInput || !statusEl) return;

    const apiKey = apiKeyInput.value.trim();
    if (!apiKey) {
        statusEl.textContent = 'Please enter an API key';
        statusEl.className = 'test-status error';
        return;
    }

    // Show testing state
    statusEl.textContent = 'Testing...';
    statusEl.className = 'test-status testing';

    try {
        const result = await window.voxtether.testOpenAIConnection(apiKey);
        if (result.success) {
            statusEl.textContent = '✓ Connection successful';
            statusEl.className = 'test-status success';
        } else {
            statusEl.textContent = `✗ ${result.error || 'Connection failed'}`;
            statusEl.className = 'test-status error';
        }
    } catch (error) {
        statusEl.textContent = `✗ ${error.message || 'Connection failed'}`;
        statusEl.className = 'test-status error';
    }
}

/**
 * Initialize OpenAI settings event handlers
 */
export function initializeOpenAISettings() {
    // Provider selection change handler
    const providerSelect = document.getElementById('transcription-provider-select');
    if (providerSelect) {
        providerSelect.addEventListener('change', (e) => {
            updateBackendSettingsVisibility(e.target.value);
        });
    }

    // API key visibility toggle
    const toggleVisibilityBtn = document.getElementById('toggle-api-key-visibility');
    if (toggleVisibilityBtn) {
        toggleVisibilityBtn.addEventListener('click', toggleApiKeyVisibility);
    }

    // Test connection button
    const testBtn = document.getElementById('test-openai-btn');
    if (testBtn) {
        testBtn.addEventListener('click', testOpenAIConnection);
    }

    // External links
    const platformLink = document.getElementById('openai-platform-link');
    if (platformLink) {
        platformLink.addEventListener('click', (e) => {
            e.preventDefault();
            window.voxtether.openExternal('https://platform.openai.com/api-keys');
        });
    }

    const pricingLink = document.getElementById('openai-pricing-link');
    if (pricingLink) {
        pricingLink.addEventListener('click', (e) => {
            e.preventDefault();
            window.voxtether.openExternal('https://openai.com/api/pricing/');
        });
    }

    // Save backend settings button
    const saveBackendBtn = document.getElementById('save-backend-btn');
    if (saveBackendBtn) {
        saveBackendBtn.addEventListener('click', saveGeneralSettings);
    }
}
