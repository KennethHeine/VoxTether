/**
 * VoxTether Utilities Module
 *
 * Common utility functions for DOM manipulation, formatting, and helpers.
 */

/**
 * Cache for DOM element references
 */
const elementCache = new Map();

/**
 * Get a DOM element by ID with caching
 * @param {string} id - Element ID
 * @returns {HTMLElement|null}
 */
export function getElement(id) {
    if (!elementCache.has(id)) {
        elementCache.set(id, document.getElementById(id));
    }
    return elementCache.get(id);
}

/**
 * Clear the element cache (useful after DOM changes)
 */
export function clearElementCache() {
    elementCache.clear();
}

/**
 * Format file size in human-readable format
 * @param {number} bytes - Size in bytes
 * @returns {string}
 */
export function formatSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}

/**
 * Format duration from milliseconds to human-readable format
 * @param {number} ms - Duration in milliseconds
 * @returns {string}
 */
export function formatDuration(ms) {
    if (!ms || ms < 1000) return '0s';

    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
        return `${hours}h ${minutes % 60}m`;
    }
    if (minutes > 0) {
        return `${minutes}m ${seconds % 60}s`;
    }
    return `${seconds}s`;
}

/**
 * Format a timestamp ISO string to readable format
 * @param {string} isoString - ISO timestamp string
 * @returns {string}
 */
export function formatTimestamp(isoString) {
    const date = new Date(isoString);
    const now = new Date();
    const diffMs = now - date;
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) {
        return 'Today ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
    if (diffDays === 1) {
        return 'Yesterday ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
    if (diffDays < 7) {
        return diffDays + ' days ago';
    }
    return date.toLocaleDateString();
}

/**
 * Convert Uint8Array to base64 string
 * @param {Uint8Array} uint8Array - Array to convert
 * @returns {string}
 */
export function uint8ArrayToBase64(uint8Array) {
    let binary = '';
    const len = uint8Array.byteLength;
    for (let i = 0; i < len; i++) {
        binary += String.fromCharCode(uint8Array[i]);
    }
    return btoa(binary);
}

/**
 * Get supported audio MIME type for MediaRecorder
 * @returns {string}
 */
export function getSupportedMimeType() {
    const mimeTypes = [
        'audio/webm;codecs=opus',
        'audio/webm',
        'audio/ogg;codecs=opus',
        'audio/ogg',
        'audio/mp4',
        'audio/wav'
    ];

    for (const mimeType of mimeTypes) {
        if (MediaRecorder.isTypeSupported(mimeType)) {
            return mimeType;
        }
    }

    console.warn('No supported audio MIME type found, using default');
    return '';
}

/**
 * Convert AudioBuffer to WAV format
 * @param {AudioBuffer} buffer - Audio buffer to convert
 * @returns {ArrayBuffer}
 */
export function audioBufferToWav(buffer) {
    const numChannels = 1;
    const sampleRate = buffer.sampleRate;
    const format = 1; // PCM
    const bitDepth = 16;

    // Get mono audio data
    let channelData;
    if (buffer.numberOfChannels === 1) {
        channelData = buffer.getChannelData(0);
    } else {
        channelData = new Float32Array(buffer.length);
        for (let i = 0; i < buffer.length; i++) {
            let sum = 0;
            for (let c = 0; c < buffer.numberOfChannels; c++) {
                sum += buffer.getChannelData(c)[i];
            }
            channelData[i] = sum / buffer.numberOfChannels;
        }
    }

    const bytesPerSample = bitDepth / 8;
    const blockAlign = numChannels * bytesPerSample;
    const byteRate = sampleRate * blockAlign;
    const dataSize = channelData.length * bytesPerSample;
    const headerSize = 44;
    const totalSize = headerSize + dataSize;

    const arrayBuffer = new ArrayBuffer(totalSize);
    const view = new DataView(arrayBuffer);

    // WAV header
    writeString(view, 0, 'RIFF');
    view.setUint32(4, totalSize - 8, true);
    writeString(view, 8, 'WAVE');
    writeString(view, 12, 'fmt ');
    view.setUint32(16, 16, true); // Subchunk1Size
    view.setUint16(20, format, true);
    view.setUint16(22, numChannels, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, byteRate, true);
    view.setUint16(32, blockAlign, true);
    view.setUint16(34, bitDepth, true);
    writeString(view, 36, 'data');
    view.setUint32(40, dataSize, true);

    // Write audio data
    let offset = 44;
    for (let i = 0; i < channelData.length; i++) {
        let sample = channelData[i];
        sample = Math.max(-1, Math.min(1, sample));
        sample = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;
        view.setInt16(offset, sample, true);
        offset += 2;
    }

    return arrayBuffer;
}

/**
 * Helper to write string to DataView
 * @param {DataView} view - DataView to write to
 * @param {number} offset - Byte offset
 * @param {string} string - String to write
 */
function writeString(view, offset, string) {
    for (let i = 0; i < string.length; i++) {
        view.setUint8(offset + i, string.charCodeAt(i));
    }
}

/**
 * Escape HTML entities to prevent XSS
 * @param {string} text - Text to escape
 * @returns {string}
 */
export function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Debounce a function
 * @param {Function} func - Function to debounce
 * @param {number} wait - Wait time in ms
 * @returns {Function}
 */
export function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}
