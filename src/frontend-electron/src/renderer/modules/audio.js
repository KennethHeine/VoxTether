/**
 * VoxTether Audio Module
 *
 * Handles audio device refreshing.
 */

import { showNotification } from './notifications.js';
import { loadMicDevices } from './mictest.js';

/**
 * Refresh audio devices in the dropdown
 */
export async function refreshAudioDevices() {
    await loadMicDevices();
    showNotification('Audio devices refreshed', 'success');
}
