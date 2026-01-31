/**
 * VoxTether Recording - Media Recorder Management
 *
 * Handles MediaRecorder setup and MIME type selection.
 */

/**
 * Get supported MIME type for MediaRecorder
 * @returns {string} Supported MIME type
 */
export function getSupportedMimeType() {
    const types = [
        'audio/webm;codecs=opus',
        'audio/webm',
        'audio/ogg;codecs=opus',
        'audio/mp4',
        'audio/wav'
    ];

    for (const type of types) {
        if (MediaRecorder.isTypeSupported(type)) {
            return type;
        }
    }

    return 'audio/webm'; // Default fallback
}

/**
 * Create and configure MediaRecorder
 * @param {MediaStream} stream - Audio stream from getUserMedia
 * @param {Function} onDataAvailable - Callback for data available event
 * @param {Function} onStop - Callback for stop event
 * @returns {MediaRecorder} Configured MediaRecorder instance
 */
export function createMediaRecorder(stream, onDataAvailable, onStop) {
    const mimeType = getSupportedMimeType();
    const mediaRecorder = new MediaRecorder(stream, {
        mimeType: mimeType
    });

    mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
            onDataAvailable(event.data);
        }
    };

    mediaRecorder.onstop = onStop;

    return mediaRecorder;
}

/**
 * Get audio constraints for getUserMedia
 * @param {string|undefined} deviceId - Audio device ID
 * @returns {object} Media constraints object
 */
export function getAudioConstraints(deviceId) {
    return {
        audio: deviceId ? {
            deviceId: { exact: deviceId },
            echoCancellation: false,
            noiseSuppression: false,
            autoGainControl: false
        } : {
            echoCancellation: false,
            noiseSuppression: false,
            autoGainControl: false
        }
    };
}
