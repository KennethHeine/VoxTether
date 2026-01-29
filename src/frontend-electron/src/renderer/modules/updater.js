/**
 * VoxTether Auto-Updater Module
 *
 * Handles checking for updates and update notifications.
 */

import { setPendingUpdateInfo } from './state.js';
import { showNotification } from './notifications.js';

/**
 * Show notification when update is available
 * @param {Object} info - Update info object
 */
export function showUpdateNotification(info) {
    setPendingUpdateInfo(info);
    showNotification(`Update ${info.version} available. See the About page to download.`, 'info', 0);

    // Update the About page if it exists
    updateAboutPageUpdateStatus(info.version, 'available');
}

/**
 * Show notification when update is ready to install
 * @param {Object} info - Update info object
 */
export function showUpdateReadyNotification(info) {
    showNotification(`Update ${info.version} ready to install. Restart to apply.`, 'success', 0);

    // Update the About page
    updateAboutPageUpdateStatus(info.version, 'ready');
}

/**
 * Update the About page with update status
 * @param {string} version - Update version
 * @param {'available'|'ready'} status - Update status
 */
function updateAboutPageUpdateStatus(version, status) {
    const updateSection = document.getElementById('update-status-section');
    if (!updateSection) return;

    updateSection.classList.remove('hidden');

    const statusText = updateSection.querySelector('#update-status-text');
    const actionBtn = updateSection.querySelector('#update-action-btn');

    if (!statusText || !actionBtn) return;

    if (status === 'available') {
        statusText.textContent = `Version ${version} is available`;
        actionBtn.textContent = 'Download Update';
        actionBtn.classList.remove('hidden');
        actionBtn.onclick = async () => {
            actionBtn.textContent = 'Downloading...';
            actionBtn.disabled = true;
            await window.voxtether.downloadUpdate();
        };
    } else if (status === 'ready') {
        statusText.textContent = `Version ${version} is ready to install`;
        actionBtn.textContent = 'Restart & Install';
        actionBtn.classList.remove('hidden');
        actionBtn.disabled = false;
        actionBtn.onclick = () => {
            window.voxtether.installUpdate();
        };
    }
}

/**
 * Check for updates manually
 */
export async function checkForUpdates() {
    showNotification('Checking for updates...', 'info');
    try {
        const result = await window.voxtether.checkForUpdates();
        if (result.available && result.updateInfo) {
            showUpdateNotification(result.updateInfo);
        } else if (result.error) {
            showNotification(result.error, 'info');
        } else {
            showNotification('You are using the latest version', 'success');
        }
    } catch (_error) {
        showNotification('Failed to check for updates', 'error');
    }
}
