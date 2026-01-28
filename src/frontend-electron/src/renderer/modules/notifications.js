/**
 * VoxTether Notifications Module
 *
 * Handles toast notifications display.
 */

/**
 * Show a toast notification
 * @param {string} message - Notification message
 * @param {'info'|'success'|'error'|'warning'} type - Notification type
 * @param {number} duration - Duration in ms (0 = persistent)
 */
export function showNotification(message, type = 'info', duration = 4000) {
    const container = document.getElementById('toast-container');
    if (!container) {
        console.warn('Toast container not found');
        return;
    }

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;

    // Icon based on type
    let icon = '';
    switch (type) {
    case 'success':
        icon = '✓';
        break;
    case 'error':
        icon = '✕';
        break;
    case 'warning':
        icon = '⚠';
        break;
    default:
        icon = 'ℹ';
    }

    toast.innerHTML = `
        <span class="toast-icon">${icon}</span>
        <span class="toast-message">${message}</span>
        <button class="toast-close">×</button>
    `;

    // Close button handler
    toast.querySelector('.toast-close').addEventListener('click', () => {
        removeToast(toast);
    });

    container.appendChild(toast);

    // Trigger animation
    requestAnimationFrame(() => {
        toast.classList.add('toast-visible');
    });

    // Auto-remove after duration
    if (duration > 0) {
        setTimeout(() => {
            removeToast(toast);
        }, duration);
    }
}

/**
 * Remove a toast notification
 * @param {HTMLElement} toast - Toast element to remove
 */
function removeToast(toast) {
    toast.classList.remove('toast-visible');
    toast.classList.add('toast-hiding');

    setTimeout(() => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }, 300);
}
