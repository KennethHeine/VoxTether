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
 * Generic helper to register a global hotkey
 * @param {string} hotkey - Hotkey string from settings
 * @param {Function} callback - Function to call when hotkey is pressed
 * @param {string} label - Human-readable label for logging
 * @param {string|null} previousHotkey - Previously registered hotkey to unregister
 * @returns {{ success: boolean, registeredHotkey: string|null }}
 */
function registerHotkey(hotkey, callback, label, previousHotkey) {
    // Unregister previous hotkey if exists
    if (previousHotkey) {
        try {
            globalShortcut.unregister(previousHotkey);
        } catch (error) {
            console.warn(`Failed to unregister previous ${label} hotkey:`, error);
        }
    }

    if (!hotkey) {
        console.log(`No ${label} hotkey configured`);
        return { success: false, registeredHotkey: null };
    }

    try {
        const electronHotkey = convertToElectronHotkey(hotkey);
        console.log(`Registering ${label} hotkey: ${hotkey} -> ${electronHotkey}`);

        const success = globalShortcut.register(electronHotkey, callback);

        if (success) {
            console.log(`${label} hotkey registered successfully`);
            return { success: true, registeredHotkey: electronHotkey };
        } else {
            console.error(`Failed to register ${label} hotkey`);
            return { success: false, registeredHotkey: null };
        }
    } catch (error) {
        console.error(`Error registering ${label} hotkey:`, error);
        return { success: false, registeredHotkey: null };
    }
}

/**
 * Register the window toggle global hotkey
 * @param {string} hotkey - Hotkey string from settings
 * @param {Function} callback - Function to call when hotkey is pressed
 * @returns {boolean} True if registration successful
 */
function registerWindowToggleHotkey(hotkey, callback) {
    const result = registerHotkey(hotkey, callback, 'window toggle', registeredWindowToggleHotkey);
    registeredWindowToggleHotkey = result.registeredHotkey;
    return result.success;
}

/**
 * Register the toggle recording global hotkey
 * @param {string} hotkey - Hotkey string from settings
 * @param {Function} callback - Function to call when hotkey is pressed
 * @returns {boolean} True if registration successful
 */
function registerToggleRecordingHotkey(hotkey, callback) {
    const result = registerHotkey(hotkey, callback, 'toggle recording', registeredToggleRecordingHotkey);
    registeredToggleRecordingHotkey = result.registeredHotkey;
    return result.success;
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
