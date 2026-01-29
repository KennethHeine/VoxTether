/**
 * VoxTether Models Module
 *
 * Handles model management, loading, and display.
 */

import { MODEL_INFO } from './state.js';
import { showNotification } from './notifications.js';
import { formatSize } from './utils.js';

/**
 * Load and display available models
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
            errorDesc.textContent = 'Start the backend server to view models';
            const errorHint = document.createElement('div');
            errorHint.className = 'model-description';
            errorHint.style.marginTop = '10px';
            errorHint.textContent = 'Run: python cli.py serve';
            errorCard.appendChild(errorName);
            errorCard.appendChild(errorDesc);
            errorCard.appendChild(errorHint);
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
            option.textContent = 'No models available - use CLI to download';
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

        // Update models grid - only show downloaded models
        modelsGrid.innerHTML = '';

        // Filter to only downloaded models
        const availableModels = models.filter(m => m.downloaded);

        if (availableModels.length === 0) {
            // Show message about using CLI
            const noModelsCard = document.createElement('div');
            noModelsCard.className = 'model-card';

            const titleDiv = document.createElement('div');
            titleDiv.className = 'model-name';
            titleDiv.textContent = 'No Models Downloaded';
            noModelsCard.appendChild(titleDiv);

            const msgDiv = document.createElement('div');
            msgDiv.className = 'model-description';
            msgDiv.textContent = 'Use the backend CLI to download models:';
            noModelsCard.appendChild(msgDiv);

            const cmdDiv = document.createElement('div');
            cmdDiv.className = 'model-size';
            cmdDiv.style.fontFamily = 'monospace';
            cmdDiv.style.marginTop = '10px';
            cmdDiv.textContent = 'python cli.py download small';
            noModelsCard.appendChild(cmdDiv);

            modelsGrid.appendChild(noModelsCard);
            return;
        }

        for (const model of availableModels) {
            const modelInfo = MODEL_INFO[model.name] || { displayName: model.display_name, sizeMb: model.size_mb, description: model.description };
            const isActive = model.name === currentModel;

            // Create card using DOM methods to prevent XSS
            const card = document.createElement('div');
            card.className = `model-card ${isActive ? 'active' : ''}`;

            const nameDiv = document.createElement('div');
            nameDiv.className = 'model-name';
            nameDiv.textContent = modelInfo.displayName || model.display_name;
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
            statusDiv.className = 'model-status downloaded';
            statusDiv.textContent = isActive ? '✓ Active' : '✓ Downloaded';
            card.appendChild(statusDiv);

            const actionsDiv = document.createElement('div');
            actionsDiv.className = 'model-actions';

            if (!isActive) {
                const loadBtn = document.createElement('button');
                loadBtn.className = 'btn btn-primary btn-small';
                loadBtn.textContent = 'Load Model';
                loadBtn.addEventListener('click', () => loadModel(model.name));
                actionsDiv.appendChild(loadBtn);
            } else {
                const activeSpan = document.createElement('span');
                activeSpan.className = 'btn btn-secondary btn-small';
                activeSpan.style.opacity = '0.7';
                activeSpan.textContent = '✓ Currently Active';
                actionsDiv.appendChild(activeSpan);
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
