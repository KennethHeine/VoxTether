/**
 * VoxTether Transcribe Module
 *
 * Handles file-based transcription functionality.
 */

import { showNotification } from './notifications.js';

// Transcribe state
const transcribeState = {
    audioFilePath: '',
    outputFolderPath: '',
    lastTranscription: ''
};

/**
 * Get current transcribe state
 * @returns {Object}
 */
export function getTranscribeState() {
    return transcribeState;
}

/**
 * Select an audio file to transcribe
 */
export async function selectAudioFile() {
    try {
        const result = await window.voxtether.selectAudioFile();

        if (result.success && result.filePath) {
            transcribeState.audioFilePath = result.filePath;
            document.getElementById('audio-file-path').value = result.filePath;
            updateTranscribeButton();
        }
    } catch (error) {
        console.error('Failed to select audio file:', error);
        showNotification('Failed to select audio file', 'error');
    }
}

/**
 * Select an output folder for saving transcripts
 */
export async function selectOutputFolder() {
    try {
        const result = await window.voxtether.selectOutputFolder();

        if (result.success && result.folderPath) {
            transcribeState.outputFolderPath = result.folderPath;
            document.getElementById('output-folder-path').value = result.folderPath;
        }
    } catch (error) {
        console.error('Failed to select output folder:', error);
        showNotification('Failed to select output folder', 'error');
    }
}

/**
 * Clear the output folder selection
 */
export function clearOutputFolder() {
    transcribeState.outputFolderPath = '';
    const el = document.getElementById('output-folder-path');
    if (el) el.value = '';
}

/**
 * Update the transcribe button state based on file selection
 */
export function updateTranscribeButton() {
    const transcribeBtn = document.getElementById('transcribe-file-btn');
    if (!transcribeBtn) return;

    const hasFile = transcribeState.audioFilePath || document.getElementById('audio-file-path').value;
    transcribeBtn.disabled = !hasFile;
}

/**
 * Transcribe the selected audio file
 */
export async function transcribeSelectedFile() {
    const audioFilePath = transcribeState.audioFilePath;
    const languageSelect = document.getElementById('transcribe-language-select');
    const language = languageSelect ? languageSelect.value : 'auto';

    if (!audioFilePath) {
        showNotification('Please select an audio file first', 'error');
        return;
    }

    // Show progress, hide result
    const progressDiv = document.getElementById('transcription-progress');
    const resultDiv = document.getElementById('transcription-result');
    const transcribeBtn = document.getElementById('transcribe-file-btn');

    if (progressDiv) progressDiv.classList.remove('hidden');
    if (resultDiv) resultDiv.classList.add('hidden');
    if (transcribeBtn) transcribeBtn.disabled = true;

    try {
        const result = await window.voxtether.transcribe(audioFilePath, language);

        if (result.success && result.data) {
            const transcription = result.data;
            transcribeState.lastTranscription = transcription.text || '';

            // Update result display
            const transcriptionText = document.getElementById('transcription-text');
            if (transcriptionText) {
                transcriptionText.value = transcription.text || '';
            }

            // Update meta info
            const metaDiv = document.getElementById('result-meta');
            if (metaDiv) {
                const duration = transcription.duration ? transcription.duration.toFixed(1) : 'N/A';
                const detectedLang = transcription.language || 'Unknown';
                metaDiv.textContent = `Duration: ${duration}s | Language: ${detectedLang}`;
            }

            // Show result
            if (resultDiv) resultDiv.classList.remove('hidden');

            // Auto-save if enabled
            await handleAutoSave(audioFilePath, transcription.text);

            showNotification('Transcription completed successfully', 'success');
        } else {
            const errorMsg = result.error || result.data?.error || 'Unknown error';
            showNotification(`Transcription failed: ${errorMsg}`, 'error');
        }
    } catch (error) {
        console.error('Transcription failed:', error);
        showNotification(`Transcription failed: ${error.message}`, 'error');
    } finally {
        if (progressDiv) progressDiv.classList.add('hidden');
        if (transcribeBtn) transcribeBtn.disabled = false;
    }
}

/**
 * Handle automatic saving of transcript and audio copy
 * @param {string} audioFilePath - Path to audio file
 * @param {string} transcriptText - Transcription text
 */
async function handleAutoSave(audioFilePath, transcriptText) {
    const saveTranscriptToggle = document.getElementById('save-transcript-toggle');
    const saveAudioCopyToggle = document.getElementById('save-audio-copy-toggle');

    const saveTranscript = saveTranscriptToggle ? saveTranscriptToggle.checked : false;
    const saveAudioCopy = saveAudioCopyToggle ? saveAudioCopyToggle.checked : false;

    if (!saveTranscript && !saveAudioCopy) {
        return;
    }

    // Determine output folder - use selected folder or same as audio file
    let outputFolder = transcribeState.outputFolderPath;
    if (!outputFolder) {
        // Get directory of audio file
        const lastSlash = Math.max(audioFilePath.lastIndexOf('/'), audioFilePath.lastIndexOf('\\'));
        outputFolder = audioFilePath.substring(0, lastSlash);
    }

    // Get base filename without extension
    const fileName = audioFilePath.substring(audioFilePath.lastIndexOf('/') + 1).replace(/\\/g, '/');
    const lastDot = fileName.lastIndexOf('.');
    const baseName = lastDot > 0 ? fileName.substring(0, lastDot) : fileName;

    try {
        // Save transcript
        if (saveTranscript && transcriptText) {
            const transcriptPath = `${outputFolder}/${baseName}.txt`;
            const result = await window.voxtether.saveTranscript(transcriptPath, transcriptText);
            if (!result.success) {
                console.warn('Failed to save transcript:', result.error);
            }
        }

        // Copy audio file
        if (saveAudioCopy && transcribeState.outputFolderPath) {
            const result = await window.voxtether.copyFile(audioFilePath, transcribeState.outputFolderPath);
            if (!result.success) {
                console.warn('Failed to copy audio file:', result.error);
            }
        }
    } catch (error) {
        console.error('Auto-save failed:', error);
    }
}

/**
 * Copy transcription text to clipboard
 */
export async function copyTranscription() {
    const transcriptionText = document.getElementById('transcription-text');
    const text = transcriptionText ? transcriptionText.value : '';

    if (!text) {
        showNotification('No transcription to copy', 'error');
        return;
    }

    try {
        await window.voxtether.copyToClipboard(text);
        showNotification('Transcription copied to clipboard', 'success');
    } catch (error) {
        console.error('Failed to copy to clipboard:', error);
        showNotification('Failed to copy to clipboard', 'error');
    }
}

/**
 * Save transcription to a file manually
 */
export async function saveTranscriptionToFile() {
    const transcriptionText = document.getElementById('transcription-text');
    const text = transcriptionText ? transcriptionText.value : '';

    if (!text) {
        showNotification('No transcription to save', 'error');
        return;
    }

    // Determine output folder
    let outputFolder = transcribeState.outputFolderPath;
    if (!outputFolder && transcribeState.audioFilePath) {
        const lastSlash = Math.max(transcribeState.audioFilePath.lastIndexOf('/'), transcribeState.audioFilePath.lastIndexOf('\\'));
        outputFolder = transcribeState.audioFilePath.substring(0, lastSlash);
    }

    if (!outputFolder) {
        showNotification('Please select an output folder first', 'error');
        return;
    }

    // Generate filename
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
    const transcriptPath = `${outputFolder}/transcription-${timestamp}.txt`;

    try {
        const result = await window.voxtether.saveTranscript(transcriptPath, text);
        if (result.success) {
            showNotification('Transcription saved successfully', 'success');
        } else {
            showNotification(`Failed to save: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to save transcription:', error);
        showNotification(`Failed to save: ${error.message}`, 'error');
    }
}
