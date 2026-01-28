/**
 * VoxTether History Module
 *
 * Handles transcription history management.
 */

import {
    HISTORY_STORAGE_KEY,
    getHistoryItems,
    setHistoryItems,
    addHistoryItem,
    removeHistoryItem,
    clearHistoryItems
} from './state.js';
import { showNotification } from './notifications.js';
import { formatTimestamp, escapeHtml } from './utils.js';

/**
 * Load history from localStorage
 */
export function loadHistory() {
    try {
        const stored = localStorage.getItem(HISTORY_STORAGE_KEY);
        if (stored) {
            const items = JSON.parse(stored);
            setHistoryItems(items);
        }
    } catch (error) {
        console.error('Failed to load history:', error);
    }
    renderHistory();
}

/**
 * Save history to localStorage
 */
export function saveHistory() {
    try {
        const items = getHistoryItems();
        localStorage.setItem(HISTORY_STORAGE_KEY, JSON.stringify(items));
    } catch (error) {
        console.error('Failed to save history:', error);
    }
}

/**
 * Add a new item to history
 * @param {string} text - Transcription text
 * @param {number} durationMs - Recording duration in ms
 */
export function addToHistory(text, durationMs = 0) {
    if (!text || !text.trim()) return;

    const item = {
        id: Date.now().toString(),
        text: text.trim(),
        timestamp: new Date().toISOString(),
        durationMs: durationMs,
        charCount: text.trim().length
    };

    addHistoryItem(item);
    saveHistory();
    renderHistory();
}

/**
 * Render history items to the UI
 * @param {string} filter - Optional filter string
 */
export function renderHistory(filter = '') {
    const container = document.getElementById('history-list');
    if (!container) return;

    const items = getHistoryItems();
    const filterLower = filter.toLowerCase();

    // Filter items
    const filteredItems = filter
        ? items.filter(item => item.text.toLowerCase().includes(filterLower))
        : items;

    if (filteredItems.length === 0) {
        container.innerHTML = `
            <div class="history-empty">
                <p>${filter ? 'No matching transcriptions found' : 'No transcription history yet'}</p>
            </div>
        `;
        return;
    }

    container.innerHTML = '';
    filteredItems.forEach(item => {
        const element = createHistoryItemElement(item);
        container.appendChild(element);
    });
}

/**
 * Create a DOM element for a history item
 * @param {Object} item - History item
 * @returns {HTMLElement}
 */
function createHistoryItemElement(item) {
    const div = document.createElement('div');
    div.className = 'history-item';
    div.dataset.id = item.id;

    // Preview text (truncated)
    const preview = item.text.length > 150
        ? item.text.substring(0, 150) + '...'
        : item.text;

    div.innerHTML = `
        <div class="history-item-content">
            <div class="history-item-text">${escapeHtml(preview)}</div>
            <div class="history-item-meta">
                <span class="history-item-time">${formatTimestamp(item.timestamp)}</span>
                ${item.durationMs ? `<span class="history-item-duration">${formatDurationShort(item.durationMs)}</span>` : ''}
                <span class="history-item-chars">${item.charCount} chars</span>
            </div>
        </div>
        <div class="history-item-actions">
            <button class="btn-icon copy-history-btn" title="Copy to clipboard">📋</button>
            <button class="btn-icon delete-history-btn" title="Delete">🗑️</button>
        </div>
    `;

    // Add event listeners
    div.querySelector('.copy-history-btn').addEventListener('click', async (e) => {
        e.stopPropagation();
        await window.voxtether.copyToClipboard(item.text);
        showNotification('Copied to clipboard', 'success');
    });

    div.querySelector('.delete-history-btn').addEventListener('click', (e) => {
        e.stopPropagation();
        deleteHistoryItemById(item.id);
    });

    // Expand on click
    div.addEventListener('click', () => {
        div.classList.toggle('expanded');
        if (div.classList.contains('expanded')) {
            div.querySelector('.history-item-text').textContent = item.text;
        } else {
            div.querySelector('.history-item-text').textContent = preview;
        }
    });

    return div;
}

/**
 * Format duration in short format
 * @param {number} ms - Duration in milliseconds
 * @returns {string}
 */
function formatDurationShort(ms) {
    const seconds = Math.floor(ms / 1000);
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    return `${minutes}m ${seconds % 60}s`;
}

/**
 * Delete a history item by ID
 * @param {string} id - Item ID
 */
export function deleteHistoryItemById(id) {
    removeHistoryItem(id);
    saveHistory();
    renderHistory(document.getElementById('history-search')?.value || '');
    showNotification('Item deleted', 'info');
}

/**
 * Filter history based on search input
 */
export function filterHistory() {
    const searchInput = document.getElementById('history-search');
    const filter = searchInput ? searchInput.value : '';
    renderHistory(filter);
}

/**
 * Export history to a file
 */
export async function exportHistory() {
    const items = getHistoryItems();
    if (items.length === 0) {
        showNotification('No history to export', 'info');
        return;
    }

    try {
        const content = items.map(item =>
            `[${item.timestamp}]\n${item.text}\n---`
        ).join('\n\n');

        const result = await window.voxtether.selectOutputFolder();
        if (result && result.path) {
            const filePath = `${result.path}/voxtether_history_${Date.now()}.txt`;
            await window.voxtether.saveTranscript(filePath, content);
            showNotification('History exported successfully', 'success');
        }
    } catch (error) {
        console.error('Failed to export history:', error);
        showNotification('Failed to export history', 'error');
    }
}

/**
 * Clear all history
 */
export function clearHistory() {
    if (!confirm('Are you sure you want to clear all history?')) return;

    clearHistoryItems();
    saveHistory();
    renderHistory();
    showNotification('History cleared', 'info');
}
