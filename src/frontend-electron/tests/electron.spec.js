// @ts-check
/**
 * VoxTether Electron App - Playwright E2E Tests
 * 
 * These tests verify the Electron application's UI and functionality
 * using Playwright's Electron support.
 * 
 * @see https://playwright.dev/docs/api/class-electronapplication
 */
const { test, expect, _electron: electron } = require('@playwright/test');
const path = require('path');

// Path to the Electron main script (modular entry point as defined in package.json)
const electronMain = path.join(__dirname, '..', 'src', 'main', 'index.js');

/**
 * Common Electron launch options
 * In CI environments, we need to disable sandbox for compatibility
 */
const electronLaunchOptions = {
  args: [
    electronMain,
    // Disable sandbox for CI compatibility
    '--no-sandbox',
    // Disable GPU for headless testing
    '--disable-gpu',
  ],
  timeout: 30000,
  env: {
    ...process.env,
    ELECTRON_DISABLE_SANDBOX: '1',
    CHROME_DEVEL_SANDBOX: '0',
    ELECTRON_HEADLESS: '1'
  }
};

/**
 * Test suite for the VoxTether Electron application
 */
test.describe('VoxTether Electron App', () => {
  let electronApp;
  let window;

  test.beforeEach(async () => {
    // Launch Electron app
    electronApp = await electron.launch(electronLaunchOptions);
    
    // Wait for the first window to appear
    window = await electronApp.firstWindow();
    // Wait for the window to fully load
    await window.waitForLoadState('domcontentloaded');
  });

  test.afterEach(async () => {
    // Close the app after each test
    if (electronApp) {
      await electronApp.close();
    }
  });

  test('should launch the application successfully', async () => {
    // Verify the app launched
    expect(electronApp).toBeDefined();
    expect(window).toBeDefined();
  });

  test('should display the VoxTether Settings window title', async () => {
    const title = await window.title();
    expect(title).toContain('VoxTether');
  });

  test('should have the main navigation sidebar', async () => {
    // Wait for the sidebar to be visible
    const sidebar = window.locator('.sidebar');
    await expect(sidebar).toBeVisible();

    // Check navigation items exist
    const generalNav = window.locator('[data-page="general"]');
    const audioNav = window.locator('[data-page="audio"]');
    const modelsNav = window.locator('[data-page="models"]');
    const transcribeNav = window.locator('[data-page="transcribe"]');
    const historyNav = window.locator('[data-page="history"]');
    const aboutNav = window.locator('[data-page="about"]');

    await expect(generalNav).toBeVisible();
    await expect(audioNav).toBeVisible();
    await expect(modelsNav).toBeVisible();
    await expect(transcribeNav).toBeVisible();
    await expect(historyNav).toBeVisible();
    await expect(aboutNav).toBeVisible();
  });

  test('should display the logo in the sidebar', async () => {
    const logo = window.locator('.logo');
    await expect(logo).toBeVisible();
    
    const logoText = window.locator('.logo-text');
    await expect(logoText).toHaveText('VoxTether');
  });

  test('should show General Settings page by default', async () => {
    // General page should be active by default
    const generalPage = window.locator('#page-general');
    await expect(generalPage).toHaveClass(/active/);
    
    // Check for General Settings header
    const header = window.locator('#page-general h1');
    await expect(header).toHaveText('General Settings');
  });

  test('should navigate to Audio page when clicked', async () => {
    // Click on Audio navigation item
    const audioNav = window.locator('[data-page="audio"]');
    await audioNav.click();
    
    // Verify Audio page is now visible
    const audioPage = window.locator('#page-audio');
    await expect(audioPage).toHaveClass(/active/);
    
    // Check for Audio Settings header
    const header = window.locator('#page-audio h1');
    await expect(header).toHaveText('Audio Settings');
  });

  test('should navigate to Models page when clicked', async () => {
    // Click on Backend navigation item (formerly Models)
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Verify Backend page is now visible
    const modelsPage = window.locator('#page-models');
    await expect(modelsPage).toHaveClass(/active/);
    
    // Check for Backend header
    const header = window.locator('#page-models h1');
    await expect(header).toHaveText('Transcription Backend');
  });

  test('should navigate to About page when clicked', async () => {
    // Click on About navigation item
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();
    
    // Verify About page is now visible
    const aboutPage = window.locator('#page-about');
    await expect(aboutPage).toHaveClass(/active/);
    
    // Check for About header
    const header = window.locator('#page-about h1');
    await expect(header).toHaveText('About VoxTether');
  });

  test('should have toggle recording hotkey input field on General Settings page', async () => {
    const hotkeyInput = window.locator('#toggle-recording-hotkey-input');
    await expect(hotkeyInput).toBeVisible();
    
    const captureBtn = window.locator('#capture-toggle-recording-hotkey-btn');
    await expect(captureBtn).toBeVisible();
    await expect(captureBtn).toHaveText('Capture');
  });

  test('should have language selection dropdown', async () => {
    const languageSelect = window.locator('#language-select');
    await expect(languageSelect).toBeVisible();
    
    // Check for English option
    const englishOption = window.locator('#language-select option[value="en"]');
    await expect(englishOption).toHaveText('English');
  });

  test('should have output mode selection dropdown', async () => {
    const outputModeSelect = window.locator('#output-mode-select');
    await expect(outputModeSelect).toBeVisible();
  });

  test('should have toggle switches for notifications and other settings', async () => {
    // Note: The checkbox inputs are styled as hidden (opacity: 0, width/height: 0)
    // because the visual toggle is rendered via CSS on the .toggle-slider element.
    // We test for the presence of the toggle-switch labels instead.
    
    const notificationsToggle = window.locator('.toggle-switch:has(#notifications-toggle)');
    await expect(notificationsToggle).toBeVisible();
    
    const recordingIndicatorToggle = window.locator('.toggle-switch:has(#recording-indicator-toggle)');
    await expect(recordingIndicatorToggle).toBeVisible();
    
    const startWithWindowsToggle = window.locator('.toggle-switch:has(#start-with-windows-toggle)');
    await expect(startWithWindowsToggle).toBeVisible();
    
    const startMinimizedToggle = window.locator('.toggle-switch:has(#start-minimized-toggle)');
    await expect(startMinimizedToggle).toBeVisible();
  });

  test('should have theme selection dropdown', async () => {
    const themeSelect = window.locator('#theme-select');
    await expect(themeSelect).toBeVisible();
    
    // Check for system, light, and dark options
    const systemOption = window.locator('#theme-select option[value="system"]');
    const lightOption = window.locator('#theme-select option[value="light"]');
    const darkOption = window.locator('#theme-select option[value="dark"]');
    
    await expect(systemOption).toHaveText('System');
    await expect(lightOption).toHaveText('Light');
    await expect(darkOption).toHaveText('Dark');
  });

  test('should have Save Settings button on General page', async () => {
    const saveBtn = window.locator('#save-general-btn');
    await expect(saveBtn).toBeVisible();
    await expect(saveBtn).toHaveText('Save Settings');
  });

  test('should have recording output settings on General page', async () => {
    // Check for recording output folder input
    const outputFolderInput = window.locator('#recording-output-folder');
    await expect(outputFolderInput).toBeVisible();

    // Check for browse and clear buttons
    const selectFolderBtn = window.locator('#select-recording-folder-btn');
    await expect(selectFolderBtn).toBeVisible();
    await expect(selectFolderBtn).toHaveText('Browse...');

    const clearFolderBtn = window.locator('#clear-recording-folder-btn');
    await expect(clearFolderBtn).toBeVisible();
    await expect(clearFolderBtn).toHaveText('Clear');

    // Check for save audio and save transcript toggles
    const saveAudioToggle = window.locator('.toggle-switch:has(#save-recording-audio-toggle)');
    await expect(saveAudioToggle).toBeVisible();

    const saveTranscriptToggle = window.locator('.toggle-switch:has(#save-recording-transcript-toggle)');
    await expect(saveTranscriptToggle).toBeVisible();
  });

  test('should display status indicator in sidebar', async () => {
    const statusIndicator = window.locator('#status-indicator');
    await expect(statusIndicator).toBeVisible();
    
    const statusText = window.locator('#status-indicator .status-text');
    await expect(statusText).toBeVisible();
  });

  test('should have audio device selection on Audio page', async () => {
    // Navigate to Audio page
    const audioNav = window.locator('[data-page="audio"]');
    await audioNav.click();
    
    // Check for audio device select
    const audioDeviceSelect = window.locator('#audio-device-select');
    await expect(audioDeviceSelect).toBeVisible();
    
    // Check for refresh button
    const refreshBtn = window.locator('#refresh-devices-btn');
    await expect(refreshBtn).toBeVisible();
  });

  test('should have mic test controls on Audio page', async () => {
    // Navigate to Audio page
    const audioNav = window.locator('[data-page="audio"]');
    await audioNav.click();
    
    // Check for mic test elements
    const micDeviceSelect = window.locator('#mic-device-select');
    await expect(micDeviceSelect).toBeVisible();
    
    const startMicTestBtn = window.locator('#start-mic-test-btn');
    await expect(startMicTestBtn).toBeVisible();
    await expect(startMicTestBtn).toHaveText('🎤 Start Test');
    
    // Check for mic test status
    const micTestStatus = window.locator('#mic-test-status');
    await expect(micTestStatus).toBeVisible();
  });

  test('should display app version on About page', async () => {
    // Navigate to About page
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();
    
    // Check for version element
    const appVersion = window.locator('#app-version');
    await expect(appVersion).toBeVisible();
  });

  test('should have model selection on Backend page', async () => {
    // Navigate to Backend page (formerly Models)
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Check for model select dropdown
    const modelSelect = window.locator('#model-select');
    await expect(modelSelect).toBeVisible();
  });

  test('should display device info on Backend page', async () => {
    // Navigate to Backend page (formerly Models)
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();

    // Check for device info section
    const deviceInfo = window.locator('#device-info');
    await expect(deviceInfo).toBeVisible();
  });

  test('should navigate to Transcribe page when clicked', async () => {
    // Click on Transcribe navigation item
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Verify Transcribe page is now visible
    const transcribePage = window.locator('#page-transcribe');
    await expect(transcribePage).toHaveClass(/active/);

    // Check for Transcribe header
    const header = window.locator('#page-transcribe h1');
    await expect(header).toHaveText('Transcribe Audio File');
  });

  test('should have audio file selection on Transcribe page', async () => {
    // Navigate to Transcribe page
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Check for audio file input and browse button
    const audioFileInput = window.locator('#audio-file-path');
    await expect(audioFileInput).toBeVisible();

    const browseBtn = window.locator('#select-audio-file-btn');
    await expect(browseBtn).toBeVisible();
    await expect(browseBtn).toHaveText('Browse...');
  });

  test('should have output folder selection on Transcribe page', async () => {
    // Navigate to Transcribe page
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Check for output folder input and buttons
    const outputFolderInput = window.locator('#output-folder-path');
    await expect(outputFolderInput).toBeVisible();

    const selectFolderBtn = window.locator('#select-output-folder-btn');
    await expect(selectFolderBtn).toBeVisible();

    const clearBtn = window.locator('#clear-output-folder-btn');
    await expect(clearBtn).toBeVisible();
  });

  test('should have save options on Transcribe page', async () => {
    // Navigate to Transcribe page
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Check for save options checkboxes
    const saveTranscriptToggle = window.locator('#save-transcript-toggle');
    await expect(saveTranscriptToggle).toBeVisible();

    const saveAudioToggle = window.locator('#save-audio-copy-toggle');
    await expect(saveAudioToggle).toBeVisible();
  });

  test('should have transcribe button initially disabled', async () => {
    // Navigate to Transcribe page
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Check that transcribe button exists and is disabled
    const transcribeBtn = window.locator('#transcribe-file-btn');
    await expect(transcribeBtn).toBeVisible();
    await expect(transcribeBtn).toBeDisabled();
  });

  test('should have language selection on Transcribe page', async () => {
    // Navigate to Transcribe page
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Check for language select dropdown
    const languageSelect = window.locator('#transcribe-language-select');
    await expect(languageSelect).toBeVisible();

    // Check for auto detect option
    const autoOption = window.locator('#transcribe-language-select option[value="auto"]');
    await expect(autoOption).toHaveText('Auto Detect');
  });
});

/**
 * Test suite for navigation highlighting
 */
test.describe('Navigation State', () => {
  let electronApp;
  let window;

  test.beforeEach(async () => {
    electronApp = await electron.launch(electronLaunchOptions);
    window = await electronApp.firstWindow();
    await window.waitForLoadState('domcontentloaded');
  });

  test.afterEach(async () => {
    if (electronApp) {
      await electronApp.close();
    }
  });

  test('should highlight active navigation item', async () => {
    // General should be active by default
    const generalNav = window.locator('[data-page="general"]');
    await expect(generalNav).toHaveClass(/active/);
    
    // Click on Audio
    const audioNav = window.locator('[data-page="audio"]');
    await audioNav.click();
    
    // Audio should now be active
    await expect(audioNav).toHaveClass(/active/);
    // General should no longer be active
    await expect(generalNav).not.toHaveClass(/active/);
  });

  test('should only show one page at a time', async () => {
    // Initially only General page should have active class
    const generalPage = window.locator('#page-general');
    const audioPage = window.locator('#page-audio');
    const modelsPage = window.locator('#page-models');
    const transcribePage = window.locator('#page-transcribe');
    const aboutPage = window.locator('#page-about');

    await expect(generalPage).toHaveClass(/active/);
    await expect(audioPage).not.toHaveClass(/active/);
    await expect(modelsPage).not.toHaveClass(/active/);
    await expect(transcribePage).not.toHaveClass(/active/);
    await expect(aboutPage).not.toHaveClass(/active/);

    // Navigate to Transcribe
    const transcribeNav = window.locator('[data-page="transcribe"]');
    await transcribeNav.click();

    // Now only Transcribe should be active
    await expect(generalPage).not.toHaveClass(/active/);
    await expect(audioPage).not.toHaveClass(/active/);
    await expect(modelsPage).not.toHaveClass(/active/);
    await expect(transcribePage).toHaveClass(/active/);
    await expect(aboutPage).not.toHaveClass(/active/);
  });
});

/**
 * Test suite for new features
 */
test.describe('New Features', () => {
  let electronApp;
  let window;

  test.beforeEach(async () => {
    electronApp = await electron.launch(electronLaunchOptions);
    window = await electronApp.firstWindow();
    await window.waitForLoadState('domcontentloaded');
  });

  test.afterEach(async () => {
    if (electronApp) {
      await electronApp.close();
    }
  });

  test('should navigate to History page when clicked', async () => {
    // Click on History navigation item
    const historyNav = window.locator('[data-page="history"]');
    await historyNav.click();

    // Verify History page is now visible
    const historyPage = window.locator('#page-history');
    await expect(historyPage).toHaveClass(/active/);

    // Check for History header
    const header = window.locator('#page-history h1');
    await expect(header).toHaveText('Transcription History');
  });

  test('should have history controls on History page', async () => {
    // Navigate to History page
    const historyNav = window.locator('[data-page="history"]');
    await historyNav.click();

    // Check for history controls
    const searchInput = window.locator('#history-search');
    await expect(searchInput).toBeVisible();

    const exportBtn = window.locator('#export-history-btn');
    await expect(exportBtn).toBeVisible();

    const clearBtn = window.locator('#clear-history-btn');
    await expect(clearBtn).toBeVisible();
  });

  test('should have window toggle hotkey input on General page', async () => {
    const windowToggleInput = window.locator('#window-toggle-hotkey-input');
    await expect(windowToggleInput).toBeVisible();

    const captureBtn = window.locator('#capture-window-toggle-hotkey-btn');
    await expect(captureBtn).toBeVisible();
    await expect(captureBtn).toHaveText('Capture');
  });

  test('should have transcription preview toggle on General page', async () => {
    const previewToggle = window.locator('.toggle-switch:has(#transcription-preview-toggle)');
    await expect(previewToggle).toBeVisible();
  });

  test('should have statistics section on About page', async () => {
    // Navigate to About page
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();

    // Check for statistics container
    const statsContainer = window.locator('#stats-container');
    await expect(statsContainer).toBeVisible();

    // Check for statistics elements
    const totalRecordings = window.locator('#stat-total-recordings');
    await expect(totalRecordings).toBeVisible();

    const totalDuration = window.locator('#stat-total-duration');
    await expect(totalDuration).toBeVisible();

    const totalCharacters = window.locator('#stat-total-characters');
    await expect(totalCharacters).toBeVisible();

    // Check for reset button
    const resetBtn = window.locator('#reset-stats-btn');
    await expect(resetBtn).toBeVisible();
  });

  test('should have check for updates button on About page', async () => {
    // Navigate to About page
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();

    // Check for updates button
    const updateBtn = window.locator('#check-updates-btn');
    await expect(updateBtn).toBeVisible();
    await expect(updateBtn).toContainText('Check for Updates');
  });

  test('should have toast notification container', async () => {
    // Toast container exists but may not be visible until toasts are shown
    const toastContainer = window.locator('#toast-container');
    await expect(toastContainer).toHaveCount(1);
  });

  test('should have transcription preview modal', async () => {
    // The modal should exist but be hidden
    const modal = window.locator('#transcription-preview-modal');
    await expect(modal).toHaveClass(/hidden/);
  });

  test('should show Backend Offline status when backend is not running', async () => {
    // Since tests run without the backend, the status indicator should show "Backend Offline"
    // Wait for the status text to update (avoid explicit timeout waits)
    const statusIndicator = window.locator('#status-indicator');
    await expect(statusIndicator).toBeVisible();
    
    // Check that the status text shows "Backend Offline" - Playwright will auto-retry until condition is met
    const statusText = window.locator('#status-indicator .status-text');
    await expect(statusText).toHaveText('Backend Offline', { timeout: 10000 });
    
    // Check that the status dot has the error class
    const statusDot = window.locator('#status-indicator .status-dot');
    await expect(statusDot).toHaveClass(/error/);
  });
});
