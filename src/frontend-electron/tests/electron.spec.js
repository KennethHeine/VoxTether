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

// Path to the Electron main script
const electronMain = path.join(__dirname, '..', 'src', 'main.js');

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
    const aboutNav = window.locator('[data-page="about"]');
    
    await expect(generalNav).toBeVisible();
    await expect(audioNav).toBeVisible();
    await expect(modelsNav).toBeVisible();
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
    // Click on Models navigation item
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Verify Models page is now visible
    const modelsPage = window.locator('#page-models');
    await expect(modelsPage).toHaveClass(/active/);
    
    // Check for Models header
    const header = window.locator('#page-models h1');
    await expect(header).toHaveText('Speech Recognition Models');
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

  test('should have hotkey input field on General Settings page', async () => {
    const hotkeyInput = window.locator('#hotkey-input');
    await expect(hotkeyInput).toBeVisible();
    
    const captureBtn = window.locator('#capture-hotkey-btn');
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

  test('should have model selection on Models page', async () => {
    // Navigate to Models page
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Check for model select dropdown
    const modelSelect = window.locator('#model-select');
    await expect(modelSelect).toBeVisible();
  });

  test('should display device info on Models page', async () => {
    // Navigate to Models page
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Check for device info section
    const deviceInfo = window.locator('#device-info');
    await expect(deviceInfo).toBeVisible();
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
    const aboutPage = window.locator('#page-about');
    
    await expect(generalPage).toHaveClass(/active/);
    await expect(audioPage).not.toHaveClass(/active/);
    await expect(modelsPage).not.toHaveClass(/active/);
    await expect(aboutPage).not.toHaveClass(/active/);
    
    // Navigate to About
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();
    
    // Now only About should be active
    await expect(generalPage).not.toHaveClass(/active/);
    await expect(audioPage).not.toHaveClass(/active/);
    await expect(modelsPage).not.toHaveClass(/active/);
    await expect(aboutPage).toHaveClass(/active/);
  });
});
