/**
 * VoxTether Statistics Module
 *
 * Handles usage statistics tracking and display.
 */

import {
    STATS_STORAGE_KEY,
    getStatistics,
    setStatistics,
    updateStatisticsValues,
    resetStatisticsValues
} from './state.js';
import { showNotification } from './notifications.js';
import { formatDuration } from './utils.js';

/**
 * Load statistics from localStorage
 */
export function loadStatistics() {
    try {
        const stored = localStorage.getItem(STATS_STORAGE_KEY);
        if (stored) {
            const stats = JSON.parse(stored);
            setStatistics(stats);
        }
    } catch (error) {
        console.error('Failed to load statistics:', error);
    }
    updateStatisticsDisplay();
}

/**
 * Save statistics to localStorage
 */
export function saveStatistics() {
    try {
        const stats = getStatistics();
        localStorage.setItem(STATS_STORAGE_KEY, JSON.stringify(stats));
    } catch (error) {
        console.error('Failed to save statistics:', error);
    }
}

/**
 * Update statistics with new recording data
 * @param {number} durationMs - Recording duration in ms
 * @param {number} characterCount - Number of characters transcribed
 */
export function updateStatistics(durationMs, characterCount) {
    updateStatisticsValues(durationMs, characterCount);
    saveStatistics();
    updateStatisticsDisplay();
}

/**
 * Reset all statistics
 */
export function resetStatistics() {
    if (!confirm('Are you sure you want to reset all statistics?')) return;

    resetStatisticsValues();
    saveStatistics();
    updateStatisticsDisplay();
    showNotification('Statistics reset', 'info');
}

/**
 * Update the statistics display in the UI
 */
export function updateStatisticsDisplay() {
    const stats = getStatistics();

    const totalRecordingsEl = document.getElementById('stat-total-recordings');
    if (totalRecordingsEl) {
        totalRecordingsEl.textContent = stats.totalRecordings.toString();
    }

    const totalDurationEl = document.getElementById('stat-total-duration');
    if (totalDurationEl) {
        totalDurationEl.textContent = formatDuration(stats.totalDurationMs);
    }

    const totalCharsEl = document.getElementById('stat-total-characters');
    if (totalCharsEl) {
        totalCharsEl.textContent = stats.totalCharacters.toLocaleString();
    }
}
