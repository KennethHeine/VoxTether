/**
 * VoxTether Electron - Global Hotkeys
 *
 * Manages global hotkey registration for window toggle and recording control.
 */

const { globalShortcut } = require('electron');

let registeredWindowToggleHotkey = null;
let registeredToggleRecordingHotkey = null;

/**
 * Convert our hotkey format to Electron's accelerator format
 * @param {string} hotkey - Hotkey in our format (e.g., "Ctrl+Shift+Space")
 * @returns {string} Hotkey in Electron format
 */
function convertToElectronHotkey(hotkey) {
    // Our format: Ctrl+Shift+Space
    // Electron format: CommandOrControl+Shift+Space
    return hotkey
        .replace(/Ctrl/g, 'CommandOrControl')
        .replace(/Win/g, 'Super');
}

/**
 * Register the window toggle global hotkey
 * @param {string} hotkey - Hotkey string from settings
 * @param {Function} callback - Function to call when hotkey is pressed
 * @returns {boolean} True if registration successful
 */
function registerWindowToggleHotkey(hotkey, callback) {
    // Unregister previous hotkey if exists
    if (registeredWindowToggleHotkey) {
        try {
            globalShortcut.unregister(registeredWindowToggleHotkey);
        } catch (error) {
            console.warn('Failed to unregister previous window toggle hotkey:', error);
        }
        registeredWindowToggleHotkey = null;
    }

    if (!hotkey) {
        console.log('No window toggle hotkey configured');
        return false;
    }

    try {
        // Convert our hotkey format to Electron's format
        const electronHotkey = convertToElectronHotkey(hotkey);
        console.log(`Registering window toggle hotkey: ${hotkey} -> ${electronHotkey}`);

        const success = globalShortcut.register(electronHotkey, callback);

        if (success) {
            registeredWindowToggleHotkey = electronHotkey;
            console.log('Window toggle hotkey registered successfully');
            return true;
        } else {
            console.error('Failed to register window toggle hotkey');
            return false;
        }
    } catch (error) {
        console.error('Error registering window toggle hotkey:', error);
        return false;
    }
}

/**
 * Register the toggle recording global hotkey
 * @param {string} hotkey - Hotkey string from settings
 * @param {Function} callback - Function to call when hotkey is pressed
 * @returns {boolean} True if registration successful
 */
function registerToggleRecordingHotkey(hotkey, callback) {
    // Unregister previous hotkey if exists
    if (registeredToggleRecordingHotkey) {
        try {
            globalShortcut.unregister(registeredToggleRecordingHotkey);
        } catch (error) {
            console.warn('Failed to unregister previous toggle recording hotkey:', error);
        }
        registeredToggleRecordingHotkey = null;
    }

    if (!hotkey) {
        console.log('No toggle recording hotkey configured');
        return false;
    }

    try {
        // Convert our hotkey format to Electron's format
        const electronHotkey = convertToElectronHotkey(hotkey);
        console.log(`Registering toggle recording hotkey: ${hotkey} -> ${electronHotkey}`);

        const success = globalShortcut.register(electronHotkey, callback);

        if (success) {
            registeredToggleRecordingHotkey = electronHotkey;
            console.log('Toggle recording hotkey registered successfully');
            return true;
        } else {
            console.error('Failed to register toggle recording hotkey');
            return false;
        }
    } catch (error) {
        console.error('Error registering toggle recording hotkey:', error);
        return false;
    }
}

/**
 * Unregister all hotkeys
 */
function unregisterAllHotkeys() {
    globalShortcut.unregisterAll();
    registeredWindowToggleHotkey = null;
    registeredToggleRecordingHotkey = null;
}

module.exports = {
    registerWindowToggleHotkey,
    registerToggleRecordingHotkey,
    unregisterAllHotkeys,
    convertToElectronHotkey
};
