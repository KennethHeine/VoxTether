/**
 * VoxTether Models Module
 *
 * Handles model management, loading, downloading, and display.
 * Displays all available models (both downloaded and not downloaded)
 * with options to download or load them.
 */

import { MODEL_INFO } from './state.js';
import { showNotification } from './notifications.js';
import { formatSize } from './utils.js';

// Track active download state
let isDownloading = false;

// Track if download listener has been initialized
let downloadListenerInitialized = false;

/**
 * Load and display all available models (downloaded and not downloaded).
 * Populates both the model selector dropdown and the models grid.
 */
export async function loadModels() {
    const modelsGrid = document.getElementById('models-grid');
    const modelSelect = document.getElementById('model-select');

    if (!modelsGrid || !modelSelect) return;

    try {
        const result = await window.voxtether.getModels();

        if (!result.success) {
            // Clear and show error using DOM methods
            modelsGrid.innerHTML = '';
            const errorCard = document.createElement('div');
            errorCard.className = 'model-card';
            const errorName = document.createElement('div');
            errorName.className = 'model-name';
            errorName.textContent = 'Backend not available';
            const errorDesc = document.createElement('div');
            errorDesc.className = 'model-description';
            errorDesc.textContent = 'Start the backend server to view and download models';
            errorCard.appendChild(errorName);
            errorCard.appendChild(errorDesc);
            modelsGrid.appendChild(errorCard);
            return;
        }

        const models = result.data.models || [];
        const currentModel = result.data.current_model;

        // Update model select dropdown using DOM methods to prevent XSS
        modelSelect.innerHTML = '';
        const downloadedModels = models.filter(m => m.downloaded);
        if (downloadedModels.length === 0) {
            const option = document.createElement('option');
            option.value = '';
            option.textContent = 'No models downloaded yet';
            modelSelect.appendChild(option);
        } else {
            for (const m of downloadedModels) {
                const option = document.createElement('option');
                option.value = m.name;
                option.textContent = m.display_name;
                if (m.name === currentModel) {
                    option.selected = true;
                }
                modelSelect.appendChild(option);
            }
        }

        // Update models grid - show ALL models (downloaded and not downloaded)
        modelsGrid.innerHTML = '';

        if (models.length === 0) {
            const noModelsCard = document.createElement('div');
            noModelsCard.className = 'model-card';
            const titleDiv = document.createElement('div');
            titleDiv.className = 'model-name';
            titleDiv.textContent = 'No Models Available';
            noModelsCard.appendChild(titleDiv);
            modelsGrid.appendChild(noModelsCard);
            return;
        }

        for (const model of models) {
            const modelInfo = MODEL_INFO[model.name] || { displayName: model.display_name, sizeMb: model.size_mb, description: model.description };
            const isActive = model.name === currentModel;
            const isDownloaded = model.downloaded;
            const displayName = modelInfo.displayName || model.display_name;

            // Create card using DOM methods to prevent XSS
            const card = document.createElement('div');
            card.className = `model-card ${isActive ? 'active' : ''} ${!isDownloaded ? 'not-downloaded' : ''}`;
            card.id = `model-card-${model.name}`;

            const nameDiv = document.createElement('div');
            nameDiv.className = 'model-name';
            nameDiv.textContent = displayName;
            card.appendChild(nameDiv);

            const descDiv = document.createElement('div');
            descDiv.className = 'model-description';
            descDiv.textContent = modelInfo.description || model.description;
            card.appendChild(descDiv);

            const sizeDiv = document.createElement('div');
            sizeDiv.className = 'model-size';
            sizeDiv.textContent = `~${formatSize((modelInfo.sizeMb || model.size_mb) * 1024 * 1024)}`;
            card.appendChild(sizeDiv);

            const statusDiv = document.createElement('div');
            statusDiv.className = `model-status ${isDownloaded ? 'downloaded' : 'not-downloaded'}`;
            statusDiv.id = `model-status-${model.name}`;
            if (isDownloaded) {
                statusDiv.textContent = isActive ? '✓ Active' : '✓ Downloaded';
            } else {
                statusDiv.textContent = '○ Not Downloaded';
            }
            card.appendChild(statusDiv);

            const actionsDiv = document.createElement('div');
            actionsDiv.className = 'model-actions';
            actionsDiv.id = `model-actions-${model.name}`;

            if (isDownloaded) {
                if (!isActive) {
                    const loadBtn = document.createElement('button');
                    loadBtn.className = 'btn btn-primary btn-small';
                    loadBtn.textContent = 'Load Model';
                    loadBtn.setAttribute('aria-label', `Load ${displayName} model`);
                    loadBtn.addEventListener('click', () => loadModel(model.name));
                    actionsDiv.appendChild(loadBtn);
                } else {
                    const activeSpan = document.createElement('span');
                    activeSpan.className = 'btn btn-secondary btn-small';
                    activeSpan.style.opacity = '0.7';
                    activeSpan.textContent = '✓ Currently Active';
                    actionsDiv.appendChild(activeSpan);
                }
            } else {
                // Not downloaded - show download button
                const downloadBtn = document.createElement('button');
                downloadBtn.className = 'btn btn-primary btn-small';
                downloadBtn.id = `download-btn-${model.name}`;
                downloadBtn.textContent = '⬇ Download';
                downloadBtn.setAttribute('aria-label', `Download ${displayName} model`);
                downloadBtn.disabled = isDownloading;
                downloadBtn.addEventListener('click', () => downloadModel(model.name));
                actionsDiv.appendChild(downloadBtn);
            }

            card.appendChild(actionsDiv);
            modelsGrid.appendChild(card);
        }
    } catch (error) {
        console.error('Failed to load models:', error);
        // Clear and show error using DOM methods
        modelsGrid.innerHTML = '';
        const errorCard = document.createElement('div');
        errorCard.className = 'model-card';
        const errorName = document.createElement('div');
        errorName.className = 'model-name';
        errorName.textContent = 'Error loading models';
        errorCard.appendChild(errorName);
        modelsGrid.appendChild(errorCard);
    }
}

/**
 * Download a model from the backend
 * @param {string} modelName - Name of the model to download
 */
export async function downloadModel(modelName) {
    if (isDownloading) {
        showNotification('A download is already in progress', 'warning');
        return;
    }

    try {
        isDownloading = true;

        // Update UI to show downloading state
        const downloadBtn = document.getElementById(`download-btn-${modelName}`);
        if (downloadBtn) {
            downloadBtn.textContent = 'Downloading...';
            downloadBtn.disabled = true;
        }

        // Show download progress container
        showDownloadProgress(modelName);

        // Disable other download buttons
        disableAllDownloadButtons();

        showNotification(`Downloading ${modelName} model...`, 'info');

        // Start the download - the progress will be updated via IPC events
        const result = await window.voxtether.downloadModel(modelName);

        if (result.success) {
            showNotification(`Model ${modelName} downloaded successfully!`, 'success');
        } else {
            showNotification(`Failed to download model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to download model:', error);
        showNotification(`Failed to download model: ${error.message}`, 'error');
    } finally {
        isDownloading = false;
        hideDownloadProgress();
        // Re-enable download buttons and reload UI
        await loadModels();
    }
}

/**
 * Show download progress UI
 * @param {string} modelName - Model being downloaded
 */
function showDownloadProgress(modelName) {
    const container = document.getElementById('download-progress-container');
    const modelNameEl = document.getElementById('download-model-name');

    if (container && modelNameEl) {
        modelNameEl.textContent = `Downloading ${modelName}...`;
        container.classList.remove('hidden');
    }
}

/**
 * Hide download progress UI
 */
function hideDownloadProgress() {
    const container = document.getElementById('download-progress-container');
    if (container) {
        container.classList.add('hidden');
    }
}

/**
 * Update download progress UI
 * @param {Object} progress - Progress data from backend
 */
export function updateDownloadProgress(progress) {
    if (!progress || typeof progress !== 'object') return;

    const percentEl = document.getElementById('download-percent');
    const progressBar = document.getElementById('download-progress-bar');
    const sizeEl = document.getElementById('download-size');
    const speedEl = document.getElementById('download-speed');

    if (percentEl && progressBar) {
        const percent = Math.round(Number(progress.progress) || 0);
        percentEl.textContent = `${percent}%`;
        progressBar.style.width = `${percent}%`;
    }

    if (sizeEl) {
        const downloaded = Number(progress.downloaded_mb) || 0;
        const total = Number(progress.total_mb) || 0;
        sizeEl.textContent = `${downloaded.toFixed(1)} MB / ${total.toFixed(1)} MB`;
    }

    if (speedEl) {
        const speed = Number(progress.speed_mbps);
        if (!isNaN(speed) && speed > 0) {
            speedEl.textContent = `${speed.toFixed(1)} MB/s`;
        } else {
            speedEl.textContent = '';
        }
    }
}

/**
 * Disable all download buttons during a download
 */
function disableAllDownloadButtons() {
    const downloadButtons = document.querySelectorAll('[id^="download-btn-"]');
    downloadButtons.forEach(btn => {
        btn.disabled = true;
    });
}

/**
 * Load a specific model
 * @param {string} modelName - Name of the model to load
 */
export async function loadModel(modelName) {
    try {
        showNotification(`Loading model ${modelName}...`, 'info');
        const result = await window.voxtether.loadModel(modelName);
        if (result.success) {
            showNotification(`Model ${modelName} loaded successfully`, 'success');
            await loadModels();
        } else {
            showNotification(`Failed to load model: ${result.error}`, 'error');
        }
    } catch (error) {
        console.error('Failed to load model:', error);
        showNotification(`Failed to load model: ${error.message}`, 'error');
    }
}

/**
 * Check and display device info (CPU/GPU)
 */
export async function checkDeviceInfo() {
    const deviceInfo = document.getElementById('device-info');
    if (!deviceInfo) return;

    const deviceText = deviceInfo.querySelector('.device-text');
    const deviceIcon = deviceInfo.querySelector('.device-icon');

    if (!deviceText || !deviceIcon) return;

    try {
        const result = await window.voxtether.getDevices();

        if (result.success && result.data) {
            const data = result.data;
            if (data.cuda_available) {
                deviceIcon.textContent = '🎮';
                deviceText.textContent = `GPU: ${data.device_name || 'NVIDIA'} (CUDA ${data.cuda_version || ''})`;
            } else {
                deviceIcon.textContent = '💻';
                deviceText.textContent = 'CPU Mode (No CUDA GPU detected)';
            }
        } else {
            deviceIcon.textContent = '⚠️';
            deviceText.textContent = 'Backend not available';
        }
    } catch (_error) {
        deviceIcon.textContent = '⚠️';
        deviceText.textContent = 'Could not detect device';
    }
}

/**
 * Initialize download progress listener.
 * This should only be called once during initialization.
 * The listener updates the progress UI during downloads.
 */
export function initializeDownloadListener() {
    // Prevent duplicate listener registration
    if (downloadListenerInitialized) {
        return;
    }
    downloadListenerInitialized = true;

    window.voxtether.onDownloadProgress((data) => {
        if (!data || typeof data !== 'object') return;

        if (data.status === 'downloading') {
            updateDownloadProgress(data);
        }
        // Note: 'complete' and 'error' statuses are handled by the downloadModel function
        // to avoid race conditions with the finally block cleanup
    });
}
