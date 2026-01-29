/**
 * VoxTether Audio Module
 *
 * Handles audio device detection and refreshing.
 */

import { showNotification } from './notifications.js';
import { loadMicDevices } from './mictest.js';

/**
 * Set up audio device change detection
 */
export function setupAudioDeviceDetection() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.addEventListener) {
        console.log('Audio device detection not supported');
        return;
    }

    navigator.mediaDevices.addEventListener('devicechange', handleDeviceChange);
    console.log('Audio device change detection enabled');
}

/**
 * Handle audio device changes (connect/disconnect)
 */
async function handleDeviceChange() {
    console.log('Audio device change detected');

    // Refresh device lists
    await loadMicDevices();

    // Show notification
    showNotification('Audio devices changed', 'info');

    // If on audio page, show updated message
    const audioPage = document.getElementById('page-audio');
    if (audioPage && audioPage.classList.contains('active')) {
        showNotification('Device list updated', 'info');
    }
}

/**
 * Refresh audio devices in the dropdown
 */
export async function refreshAudioDevices() {
    await loadMicDevices();
    showNotification('Audio devices refreshed', 'success');
}
