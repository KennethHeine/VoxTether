// @ts-check
/**
 * VoxTether Electron App - Screenshot Tests
 * 
 * These tests capture screenshots of each page of the application.
 * Screenshots are saved to the 'screenshots' directory.
 * 
 * Run with: npm test -- --grep "Screenshot"
 */
const { test, expect, _electron: electron } = require('@playwright/test');
const path = require('path');
const fs = require('fs');

// Path to the Electron main script (modular entry point as defined in package.json)
const electronMain = path.join(__dirname, '..', 'src', 'main', 'index.js');

// Screenshots output directory
const screenshotsDir = path.join(__dirname, '..', 'screenshots');

/**
 * Common Electron launch options
 */
const electronLaunchOptions = {
  args: [
    electronMain,
    '--no-sandbox',
    '--disable-gpu',
  ],
  timeout: 30000,
};

/**
 * Test suite for capturing screenshots of the VoxTether application
 */
test.describe('Screenshot Capture', () => {
  let electronApp;
  let window;

  test.beforeAll(async () => {
    // Ensure screenshots directory exists
    if (!fs.existsSync(screenshotsDir)) {
      fs.mkdirSync(screenshotsDir, { recursive: true });
    }
  });

  test.beforeEach(async () => {
    electronApp = await electron.launch(electronLaunchOptions);
    window = await electronApp.firstWindow();
    await window.waitForLoadState('domcontentloaded');
    // Wait for any animations to settle
    await window.waitForTimeout(1000);
  });

  test.afterEach(async () => {
    if (electronApp) {
      await electronApp.close();
    }
  });

  test('Screenshot: 1 - Audio Settings page', async () => {
    // Navigate to Audio page
    const audioNav = window.locator('[data-page="audio"]');
    await audioNav.click();
    
    // Wait for page transition
    const audioPage = window.locator('#page-audio');
    await expect(audioPage).toHaveClass(/active/);
    await window.waitForTimeout(300);
    
    // Take screenshot
    await window.screenshot({ 
      path: path.join(screenshotsDir, 'audio-settings.png'),
      timeout: 60000
    });
  });

  test('Screenshot: 2 - Models page', async () => {
    // Navigate to Models page
    const modelsNav = window.locator('[data-page="models"]');
    await modelsNav.click();
    
    // Wait for page transition
    const modelsPage = window.locator('#page-models');
    await expect(modelsPage).toHaveClass(/active/);
    await window.waitForTimeout(300);
    
    // Take screenshot
    await window.screenshot({ 
      path: path.join(screenshotsDir, 'models.png'),
      timeout: 60000
    });
  });

  test('Screenshot: 3 - About page', async () => {
    // Navigate to About page
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();
    
    // Wait for page transition
    const aboutPage = window.locator('#page-about');
    await expect(aboutPage).toHaveClass(/active/);
    await window.waitForTimeout(300);
    
    // Take screenshot
    await window.screenshot({ 
      path: path.join(screenshotsDir, 'about.png'),
      timeout: 60000
    });
  });

  test('Screenshot: 4 - General Settings page', async () => {
    // Navigate away first then back to General
    const aboutNav = window.locator('[data-page="about"]');
    await aboutNav.click();
    await window.waitForTimeout(300);
    
    // Navigate to General page
    const generalNav = window.locator('[data-page="general"]');
    await generalNav.click();
    
    // Wait for page transition
    const generalPage = window.locator('#page-general');
    await expect(generalPage).toHaveClass(/active/);
    await window.waitForTimeout(500);
    
    // Take screenshot with timeout option
    await window.screenshot({ 
      path: path.join(screenshotsDir, 'general-settings.png'),
      timeout: 60000
    });
  });
});
