/**
 * VoxTether Status Module
 *
 * Handles status indicator updates in the sidebar.
 */

/**
 * Update the status indicator
 * @param {string} text - Status text to display
 * @param {'ready'|'recording'|'transcribing'|'error'} state - Current state
 */
export function updateStatus(text, state = 'ready') {
    const indicator = document.getElementById('status-indicator');
    if (indicator) {
        indicator.className = `status-indicator status-${state}`;
        const textEl = indicator.querySelector('.status-text');
        if (textEl) {
            textEl.textContent = text;
        }
        // Update the status dot class
        const dotEl = indicator.querySelector('.status-dot');
        if (dotEl) {
            dotEl.className = `status-dot ${state}`;
        }
    }
}

/**
 * Update the recording status indicator
 * @param {'ready'|'recording'|'transcribing'|'error'} status - Status to display
 */
export function updateRecordingStatus(status) {
    const statusSpan = document.getElementById('recording-status');
    if (!statusSpan) return;

    switch (status) {
    case 'recording':
        statusSpan.textContent = '🔴 Recording...';
        statusSpan.className = 'recording-status recording';
        break;
    case 'transcribing':
        statusSpan.textContent = '⏳ Transcribing...';
        statusSpan.className = 'recording-status transcribing';
        break;
    case 'error':
        statusSpan.textContent = '❌ Error';
        statusSpan.className = 'recording-status';
        break;
    case 'ready':
    default:
        statusSpan.textContent = '';
        statusSpan.className = 'recording-status';
        break;
    }
}
