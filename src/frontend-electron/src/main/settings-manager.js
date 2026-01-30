/**
 * VoxTether Electron - Settings Manager
 *
 * Manages application settings persistence and defaults.
 */

const { app } = require('electron');
const path = require('path');
const fs = require('fs');
const { DEFAULT_SETTINGS } = require('../shared/constants.js');

// Paths
const userDataPath = app.getPath('userData');
const settingsPath = path.join(userDataPath, 'settings.json');

// Application settings state
let settings = null;

/**
 * Get the settings file path
 * @returns {string} Path to the settings file
 */
function getSettingsPath() {
    return settingsPath;
}

/**
 * Get the models directory path
 * @returns {string} Path to the models directory
 */
function getModelsPath() {
    return path.join(userDataPath, 'models');
}

/**
 * Get the logs directory path
 * @returns {string} Path to the logs directory
 */
function getLogsPath() {
    return path.join(userDataPath, 'logs');
}

/**
 * Get the user data directory path
 * @returns {string} Path to the user data directory
 */
function getUserDataPath() {
    return userDataPath;
}

/**
 * Load settings from file or create with defaults
 * @returns {object} Settings object
 */
function loadSettings() {
    try {
        if (fs.existsSync(settingsPath)) {
            const data = fs.readFileSync(settingsPath, 'utf8');
            settings = { ...DEFAULT_SETTINGS, ...JSON.parse(data) };
        } else {
            settings = { ...DEFAULT_SETTINGS };
            saveSettings();
        }
    } catch (error) {
        console.error('Failed to load settings:', error);
        settings = { ...DEFAULT_SETTINGS };
    }
    return settings;
}

/**
 * Save settings to file
 * @returns {boolean} True if settings were saved successfully
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
 * Get current settings
 * @returns {object} Current settings object
 */
function getSettings() {
    return settings;
}

/**
 * Update settings
 * @param {object} newSettings - New settings to merge
 * @returns {object} Updated settings object
 */
function updateSettings(newSettings) {
    settings = { ...settings, ...newSettings };
    return settings;
}

module.exports = {
    loadSettings,
    saveSettings,
    getSettings,
    updateSettings,
    getSettingsPath,
    getModelsPath,
    getLogsPath,
    getUserDataPath
};
