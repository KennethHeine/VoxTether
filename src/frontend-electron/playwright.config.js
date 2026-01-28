// @ts-check
const { defineConfig } = require('@playwright/test');

/**
 * Playwright configuration for testing the VoxTether Electron application.
 * 
 * For Electron testing, we don't use browser projects - instead we launch
 * Electron directly in the tests using the _electron API.
 * 
 * @see https://playwright.dev/docs/api/class-electronapplication
 */
module.exports = defineConfig({
  testDir: './tests',
  // Test file pattern
  testMatch: '**/*.spec.js',
  // Longer timeout for Electron app startup
  timeout: 60000,
  expect: {
    // Timeout for assertions
    timeout: 10000,
  },
  // Run tests in parallel
  fullyParallel: true,
  // Fail fast on CI
  forbidOnly: !!process.env.CI,
  // Retry on CI only
  retries: process.env.CI ? 2 : 0,
  // Number of workers for parallel test execution
  workers: 4,
  // Reporter to use
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  // Use a basic config for Electron tests (no browser needed)
  use: {
    // Capture trace on first retry
    trace: 'on-first-retry',
    // Take screenshot on failure
    screenshot: 'only-on-failure',
  },
});
