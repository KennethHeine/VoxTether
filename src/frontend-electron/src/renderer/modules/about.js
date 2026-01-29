/**
 * VoxTether About Module
 *
 * Handles the About page display.
 */

import { updateStatisticsDisplay } from './statistics.js';

/**
 * Load and display about page information
 */
export async function loadAboutInfo() {
    try {
        const appInfo = await window.voxtether.getAppInfo();

        const appVersion = document.getElementById('app-version');
        if (appVersion) {
            appVersion.textContent = `Version ${appInfo.version}`;
        }

        const platformInfo = document.getElementById('platform-info');
        if (platformInfo) {
            platformInfo.textContent = window.platform.isWindows ? 'Windows' :
                window.platform.isMac ? 'macOS' : 'Linux';
        }

        const electronVersion = document.getElementById('electron-version');
        if (electronVersion) {
            electronVersion.textContent = process.versions?.electron || '-';
        }

        const dataPath = document.getElementById('data-path');
        if (dataPath) {
            dataPath.textContent = appInfo.userDataPath;
            dataPath.addEventListener('click', () => window.voxtether.openPath(appInfo.userDataPath));
        }

        const modelsPath = document.getElementById('models-path');
        if (modelsPath) {
            modelsPath.textContent = appInfo.modelsPath;
            modelsPath.addEventListener('click', () => window.voxtether.openPath(appInfo.modelsPath));
        }

        // Update statistics display
        updateStatisticsDisplay();
    } catch (error) {
        console.error('Failed to load app info:', error);
    }
}

/**
 * Initialize about page event listeners
 */
export function initializeAboutListeners() {
    const githubLink = document.getElementById('github-link');
    if (githubLink) {
        githubLink.addEventListener('click', () => {
            window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether');
        });
    }

    const docsLink = document.getElementById('docs-link');
    if (docsLink) {
        docsLink.addEventListener('click', () => {
            window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether/tree/main/docs');
        });
    }

    const releasesLink = document.getElementById('releases-link');
    if (releasesLink) {
        releasesLink.addEventListener('click', () => {
            window.voxtether.openExternal('https://github.com/KennethHeine/VoxTether/releases');
        });
    }
}
