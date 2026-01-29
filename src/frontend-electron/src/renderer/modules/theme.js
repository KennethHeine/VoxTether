/**
 * VoxTether Theme Module
 *
 * Handles theme switching (system, light, dark).
 */

/**
 * Apply the specified theme
 * @param {string} theme - Theme name ('system', 'light', 'dark')
 */
export function applyTheme(theme) {
    const root = document.documentElement;

    if (theme === 'system') {
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        root.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
    } else {
        root.setAttribute('data-theme', theme);
    }
}

/**
 * Get the current effective theme
 * @returns {string} Current theme ('light' or 'dark')
 */
export function getCurrentTheme() {
    return document.documentElement.getAttribute('data-theme') || 'light';
}

/**
 * Set up system theme change listener
 * @param {Function} callback - Called when system theme changes
 */
export function setupSystemThemeListener(callback) {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    mediaQuery.addEventListener('change', (e) => {
        callback(e.matches ? 'dark' : 'light');
    });
}
