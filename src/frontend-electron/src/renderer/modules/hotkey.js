/**
 * VoxTether Hotkey Module
 *
 * Handles hotkey capture functionality.
 */

import {
    isCapturingHotkey,
    setCapturingHotkey,
    getCapturingHotkeyType
} from './state.js';

/**
 * Start capturing window toggle hotkey
 */
export function startWindowToggleHotkeyCapture() {
    setCapturingHotkey(true, 'windowToggle');
    const input = document.getElementById('window-toggle-hotkey-input');
    const btn = document.getElementById('capture-window-toggle-hotkey-btn');
    if (input) {
        input.value = 'Press hotkey combination...';
        input.classList.add('capturing');
    }
    if (btn) {
        btn.textContent = 'Listening...';
    }
}

/**
 * Start capturing toggle recording hotkey
 */
export function startToggleRecordingHotkeyCapture() {
    setCapturingHotkey(true, 'toggleRecording');
    const input = document.getElementById('toggle-recording-hotkey-input');
    const btn = document.getElementById('capture-toggle-recording-hotkey-btn');
    if (input) {
        input.value = 'Press hotkey combination...';
        input.classList.add('capturing');
    }
    if (btn) {
        btn.textContent = 'Listening...';
    }
}

/**
 * Handle keyboard event for hotkey capture
 * @param {KeyboardEvent} event - Keyboard event
 */
export function handleHotkeyCapture(event) {
    if (!isCapturingHotkey()) return;

    event.preventDefault();
    event.stopPropagation();

    // Build hotkey string
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');

    // Get key name
    let key = event.key;
    if (key === ' ') key = 'Space';
    else if (key.length === 1) key = key.toUpperCase();
    else if (key.startsWith('Arrow')) key = key.replace('Arrow', '');

    // Only complete if we have modifiers + a non-modifier key
    if (parts.length > 0 && !['Control', 'Alt', 'Shift', 'Meta'].includes(key)) {
        parts.push(key);
        const hotkey = parts.join('+');

        if (getCapturingHotkeyType() === 'windowToggle') {
            const input = document.getElementById('window-toggle-hotkey-input');
            if (input) input.value = hotkey;
        } else if (getCapturingHotkeyType() === 'toggleRecording') {
            const input = document.getElementById('toggle-recording-hotkey-input');
            if (input) input.value = hotkey;
        }
        stopHotkeyCapture();
    }
}

/**
 * Stop hotkey capture mode
 */
export function stopHotkeyCapture() {
    const type = getCapturingHotkeyType();
    setCapturingHotkey(false, null);

    if (type === 'windowToggle') {
        const input = document.getElementById('window-toggle-hotkey-input');
        const btn = document.getElementById('capture-window-toggle-hotkey-btn');
        if (input) input.classList.remove('capturing');
        if (btn) btn.textContent = 'Capture';
    } else if (type === 'toggleRecording') {
        const input = document.getElementById('toggle-recording-hotkey-input');
        const btn = document.getElementById('capture-toggle-recording-hotkey-btn');
        if (input) input.classList.remove('capturing');
        if (btn) btn.textContent = 'Capture';
    }
}
