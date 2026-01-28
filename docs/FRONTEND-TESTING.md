# Frontend Testing Guide

This document provides an in-depth guide to testing the VoxTether Electron frontend application.

## Overview

VoxTether's Electron frontend uses **Playwright** for end-to-end (E2E) testing. Playwright is a modern testing framework that supports Electron applications natively through its `_electron` API, allowing tests to interact with the actual application rather than a simulated environment.

## Testing Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| Playwright | 1.55.x | E2E testing framework |
| Node.js | 20.x | Runtime |
| npm | 10.x | Package manager |

## Prerequisites

Before running tests, ensure you have:

1. **Node.js 20.x** or higher installed
2. **npm 10.x** or higher installed
3. Dependencies installed via `npm install`

## Running Tests

### Basic Commands

```bash
# Navigate to the frontend directory
cd src/frontend-electron

# Install dependencies (first time only)
npm install

# Run all Playwright tests
npm test

# Run tests with Playwright UI (interactive mode)
npm run test:ui

# Run tests in headed mode (visible browser)
npm run test:headed
```

### CI Environment

For CI environments without a display (e.g., Linux servers), use xvfb to provide a virtual display:

```bash
xvfb-run --auto-servernum npm test
```

This is automatically configured in the GitHub Actions CI pipeline.

---

## Test Structure

Tests are located in `src/frontend-electron/tests/` and follow the naming convention `*.spec.js`.

### Test Files

| File | Purpose |
|------|---------|
| `electron.spec.js` | Main E2E tests for UI functionality and navigation |
| `screenshots.spec.js` | Screenshot capture tests for visual documentation |

### Test Organization

Tests are organized using Playwright's `test.describe()` blocks:

```javascript
test.describe('VoxTether Electron App', () => {
  // Test suite for main application functionality
});

test.describe('Navigation State', () => {
  // Test suite for navigation behavior
});

test.describe('Screenshot Capture', () => {
  // Test suite for capturing screenshots
});
```

---

## Playwright Configuration

The Playwright configuration is defined in `playwright.config.js`:

```javascript
module.exports = defineConfig({
  testDir: './tests',
  testMatch: '**/*.spec.js',
  timeout: 60000,              // 60 second timeout for Electron startup
  fullyParallel: false,        // Run tests sequentially (required for Electron)
  workers: 1,                  // Single worker for Electron tests
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  use: {
    trace: 'on-first-retry',           // Capture trace on failure
    screenshot: 'only-on-failure',     // Screenshot on failure
  },
  // Retry failed tests in CI
  retries: process.env.CI ? 2 : 0,
});
```

### Key Configuration Options

| Option | Value | Reason |
|--------|-------|--------|
| `timeout` | 60000ms | Electron apps need longer startup time |
| `fullyParallel` | false | Electron tests must run sequentially |
| `workers` | 1 | Only one Electron instance at a time |
| `retries` | 2 (CI) / 0 (local) | Retry flaky tests only in CI |

---

## Electron-Specific Testing

### Launching the Application

Playwright provides the `_electron` API for testing Electron apps:

```javascript
const { test, expect, _electron: electron } = require('@playwright/test');

const electronLaunchOptions = {
  args: [
    electronMain,
    '--no-sandbox',      // Required for CI compatibility
    '--disable-gpu',     // Required for headless testing
  ],
  timeout: 30000,
};

// Launch the app
electronApp = await electron.launch(electronLaunchOptions);

// Get the first window
window = await electronApp.firstWindow();

// Wait for content to load
await window.waitForLoadState('domcontentloaded');
```

### Test Lifecycle

Each test follows a consistent lifecycle:

```javascript
test.beforeEach(async () => {
  // Launch Electron app
  electronApp = await electron.launch(electronLaunchOptions);
  window = await electronApp.firstWindow();
  await window.waitForLoadState('domcontentloaded');
});

test.afterEach(async () => {
  // Clean up: close the app
  if (electronApp) {
    await electronApp.close();
  }
});
```

---

## Test Categories

### 1. Application Launch Tests

Verify that the application starts correctly:

```javascript
test('should launch the application successfully', async () => {
  expect(electronApp).toBeDefined();
  expect(window).toBeDefined();
});

test('should display the VoxTether Settings window title', async () => {
  const title = await window.title();
  expect(title).toContain('VoxTether');
});
```

### 2. Navigation Tests

Verify page navigation functionality:

```javascript
test('should navigate to Audio page when clicked', async () => {
  const audioNav = window.locator('[data-page="audio"]');
  await audioNav.click();
  
  const audioPage = window.locator('#page-audio');
  await expect(audioPage).toHaveClass(/active/);
  
  const header = window.locator('#page-audio h1');
  await expect(header).toHaveText('Audio Settings');
});
```

### 3. UI Component Tests

Verify that UI components are present and functional:

```javascript
test('should have hotkey input field on General Settings page', async () => {
  const hotkeyInput = window.locator('#hotkey-input');
  await expect(hotkeyInput).toBeVisible();
  
  const captureBtn = window.locator('#capture-hotkey-btn');
  await expect(captureBtn).toBeVisible();
  await expect(captureBtn).toHaveText('Capture');
});

test('should have toggle switches for notifications', async () => {
  const notificationsToggle = window.locator('.toggle-switch:has(#notifications-toggle)');
  await expect(notificationsToggle).toBeVisible();
});
```

### 4. Navigation State Tests

Verify that navigation state is correctly maintained:

```javascript
test('should highlight active navigation item', async () => {
  const generalNav = window.locator('[data-page="general"]');
  await expect(generalNav).toHaveClass(/active/);
  
  const audioNav = window.locator('[data-page="audio"]');
  await audioNav.click();
  
  await expect(audioNav).toHaveClass(/active/);
  await expect(generalNav).not.toHaveClass(/active/);
});

test('should only show one page at a time', async () => {
  const generalPage = window.locator('#page-general');
  const audioPage = window.locator('#page-audio');
  
  await expect(generalPage).toHaveClass(/active/);
  await expect(audioPage).not.toHaveClass(/active/);
});
```

### 5. Screenshot Tests

Capture screenshots for visual documentation:

```javascript
test('Screenshot: General Settings page', async () => {
  // Wait for any animations to settle before capturing
  // Note: waitForTimeout is used here for CSS animations that don't
  // have a reliable completion indicator
  const generalPage = window.locator('#page-general');
  await expect(generalPage).toHaveClass(/active/);
  await window.waitForLoadState('domcontentloaded');
  
  await window.screenshot({ 
    path: path.join(screenshotsDir, 'general-settings.png'),
    timeout: 60000
  });
});
```

---

## Page-by-Page Test Coverage

### General Settings Page

| Test | What it verifies |
|------|-----------------|
| Default active state | General page is shown by default |
| Hotkey input | Hotkey input field and capture button exist |
| Language dropdown | Language selection dropdown is visible |
| Output mode dropdown | Output mode selection is visible |
| Toggle switches | Notifications, recording indicator, startup toggles exist |
| Theme selection | System/Light/Dark theme options exist |
| Save button | Save Settings button is visible |

### Audio Settings Page

| Test | What it verifies |
|------|-----------------|
| Navigation | Clicking Audio nav shows Audio page |
| Device dropdown | Audio device selection dropdown exists |
| Refresh button | Refresh devices button exists |
| Test microphone | Test microphone button exists |

### Models Page

| Test | What it verifies |
|------|-----------------|
| Navigation | Clicking Models nav shows Models page |
| Model selection | Model dropdown exists |
| Device info | Device info section (GPU/CPU) is visible |

### About Page

| Test | What it verifies |
|------|-----------------|
| Navigation | Clicking About nav shows About page |
| Version display | App version is displayed |

---

## Best Practices

### 1. Use Stable Selectors

Prefer data attributes and IDs over CSS classes:

```javascript
// Good - stable selector
window.locator('[data-page="audio"]')
window.locator('#hotkey-input')

// Avoid - fragile selector
window.locator('.nav-item:nth-child(2)')
```

### 2. Wait for Elements

Always wait for elements to be in the expected state:

```javascript
// Wait for element to be visible (preferred - uses built-in retries)
await expect(element).toBeVisible();

// Wait for page load
await window.waitForLoadState('domcontentloaded');

// Wait for network to be idle
await window.waitForLoadState('networkidle');

// For CSS animations without completion events, waitForTimeout may be needed
// but should be avoided when possible. Prefer explicit element checks.
await window.waitForTimeout(300); // Use sparingly
```

### 3. Clean Up Resources

Always close the Electron app after tests:

```javascript
test.afterEach(async () => {
  if (electronApp) {
    await electronApp.close();
  }
});
```

### 4. Use Descriptive Test Names

Test names should clearly describe what is being tested:

```javascript
// Good
test('should navigate to Audio page when clicked', async () => {});

// Avoid
test('test audio', async () => {});
```

---

## Debugging Tests

### Interactive Mode

Run tests with the Playwright UI for debugging:

```bash
npm run test:ui
```

### Headed Mode

See the actual Electron window during tests:

```bash
npm run test:headed
```

### Traces and Screenshots

On test failure, Playwright captures:
- **Screenshots** - Visual state at failure
- **Traces** - Step-by-step recording (on first retry)

View trace files with (check the actual path in your `test-results/` directory):

```bash
# Example - actual path may vary based on test name
npx playwright show-trace test-results/<test-name>/trace.zip
```

### Console Logging

Add logging to tests for debugging:

```javascript
test('debug test', async () => {
  const title = await window.title();
  console.log('Window title:', title);
  
  // Get inner text of an element
  const sidebarText = await window.locator('.sidebar').innerText();
  console.log('Sidebar text:', sidebarText);
  
  // Get HTML content of an element
  const sidebarHtml = await window.locator('.sidebar').evaluate(el => el.innerHTML);
  console.log('Sidebar HTML:', sidebarHtml);
});
```

---

## CI/CD Integration

### GitHub Actions Workflow

The CI pipeline runs frontend E2E tests on every pull request:

```yaml
test-frontend-e2e:
  name: Test Frontend (Playwright E2E)
  runs-on: ubuntu-latest

  steps:
  - uses: actions/checkout@v4

  - name: Setup Node.js
    uses: actions/setup-node@v4
    with:
      node-version: '20'
      cache: 'npm'
      cache-dependency-path: 'src/frontend-electron/package-lock.json'

  - name: Install dependencies
    run: |
      cd src/frontend-electron
      npm install

  - name: Run Playwright tests
    run: |
      cd src/frontend-electron
      xvfb-run --auto-servernum npm test

  - name: Upload screenshots
    uses: actions/upload-artifact@v4
    if: always()
    with:
      name: playwright-screenshots
      path: src/frontend-electron/screenshots/
      retention-days: 7
```

### CI Artifacts

The CI pipeline uploads:
- **playwright-screenshots** - Screenshots captured during tests
- **playwright-test-results** - Test results on failure

---

## Writing New Tests

### 1. Create a Test File

Create a new file in `src/frontend-electron/tests/` with `.spec.js` extension:

```javascript
// @ts-check
const { test, expect, _electron: electron } = require('@playwright/test');
const path = require('path');

const electronMain = path.join(__dirname, '..', 'src', 'main.js');

const electronLaunchOptions = {
  args: [electronMain, '--no-sandbox', '--disable-gpu'],
  timeout: 30000,
};

test.describe('My New Test Suite', () => {
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

  test('my new test', async () => {
    // Test implementation
  });
});
```

### 2. Run Specific Tests

Run only your new tests:

```bash
npm test -- --grep "My New Test Suite"
```

### 3. Verify in CI

Push changes and verify tests pass in the GitHub Actions workflow.

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Tests timeout | Increase timeout in config or test |
| App doesn't launch | Check `--no-sandbox` flag is set |
| Cannot find element | Add `await expect(element).toBeVisible()` wait |
| Tests flaky in CI | Add explicit waits, use `retries` option |
| Screenshot tests fail | Ensure `screenshots/` directory exists |

### xvfb Errors on Linux

If you see display-related errors on Linux:

```bash
# Install xvfb
sudo apt-get install xvfb

# Run with xvfb-run
xvfb-run --auto-servernum npm test
```

### Electron Launch Failures

If Electron fails to launch:

1. Check Node.js version (20.x required)
2. Verify `npm install` completed successfully
3. Check for conflicting processes using the same port
4. Try running with `--disable-gpu` flag

---

## Related Documentation

- [Architecture](ARCHITECTURE.md) - System architecture overview
- [Frontend Installation](FRONTEND-INSTALLATION.md) - Frontend setup guide
- [Running Locally](RUNNING-LOCALLY.md) - Local development guide
- [Playwright Documentation](https://playwright.dev/docs/api/class-electronapplication) - Official Playwright Electron docs

---

## Summary

VoxTether's frontend testing uses Playwright for comprehensive E2E testing of the Electron application. Tests verify:

- ✅ Application launch and window creation
- ✅ Navigation between settings pages
- ✅ UI component presence and visibility
- ✅ Navigation state management
- ✅ Visual appearance (screenshots)

Tests run automatically in CI on every pull request, ensuring the frontend remains stable and functional.
