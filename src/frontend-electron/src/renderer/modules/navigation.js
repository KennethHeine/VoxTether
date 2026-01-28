/**
 * VoxTether Navigation Module
 *
 * Handles page navigation and active state management.
 */

import { stopMicTest, isMicTestRunning } from './mictest.js';

/**
 * Initialize navigation event listeners
 */
export function initializeNavigation() {
    const navItems = document.querySelectorAll('.nav-item');

    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const page = item.dataset.page;
            navigateTo(page);
        });
    });
}

/**
 * Navigate to a specific page
 * @param {string} pageName - The page to navigate to
 */
export function navigateTo(pageName) {
    // Stop mic test if leaving audio page and it's running
    if (isMicTestRunning()) {
        stopMicTest();
    }

    // Update nav items
    document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.toggle('active', item.dataset.page === pageName);
    });

    // Update pages
    document.querySelectorAll('.page').forEach(page => {
        page.classList.toggle('active', page.id === `page-${pageName}`);
    });
}
