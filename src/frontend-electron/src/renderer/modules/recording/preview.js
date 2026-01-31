/**
 * VoxTether Recording - Transcription Preview
 *
 * Handles the transcription preview modal for reviewing and editing
 * transcriptions before inserting them.
 */

import { showNotification } from '../notifications.js';

// Store pending duration for statistics
let _pendingPreviewDuration = 0;

/**
 * Show the transcription preview modal
 * @param {string} text - Transcribed text to preview
 * @param {number} durationMs - Recording duration in milliseconds
 */
export function showTranscriptionPreviewModal(text, durationMs = 0) {
    _pendingPreviewDuration = durationMs;
    const modal = document.getElementById('transcription-preview-modal');
    const textarea = document.getElementById('preview-text');

    if (!modal || !textarea) return;

    textarea.value = text;
    modal.classList.remove('hidden');
    textarea.focus();
    textarea.select();
}

/**
 * Close the preview modal (cancel)
 */
export function closePreviewModal() {
    const modal = document.getElementById('transcription-preview-modal');
    if (!modal) return;

    modal.classList.add('hidden');
    document.getElementById('preview-text').value = '';
    _pendingPreviewDuration = 0;
}

/**
 * Copy only without inserting (from preview modal)
 */
export async function previewCopyOnly() {
    const text = document.getElementById('preview-text').value.trim();
    if (text) {
        await window.voxtether.copyToClipboard(text);
        showNotification('Copied to clipboard', 'success');
    }
    closePreviewModal();
}

/**
 * Insert the text (copy to clipboard and close)
 */
export async function previewInsert() {
    const text = document.getElementById('preview-text').value.trim();
    if (text) {
        // Dynamically import to avoid circular dependency
        const { performTranscriptionOutput } = await import('./transcription.js');
        await performTranscriptionOutput(text);
        showNotification('Transcription inserted', 'success');
    }
    closePreviewModal();
}

/**
 * Get the pending preview duration
 * @returns {number} Duration in milliseconds
 */
export function getPendingPreviewDuration() {
    return _pendingPreviewDuration;
}
