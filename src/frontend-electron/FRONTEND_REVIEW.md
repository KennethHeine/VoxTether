# VoxTether Frontend Code Review & Implementation Plan

**Review Date:** 2024  
**Reviewer:** AI Code Review  
**Scope:** Comprehensive review of the Electron frontend (code quality, security, performance, bug hunting)

---

## Executive Summary

The VoxTether Electron frontend is a well-structured voice dictation application with good modular architecture and security practices. This review identified **28 actionable issues** across four priority levels. The codebase demonstrates consistent patterns, good documentation, and attention to security, but has accumulated technical debt that should be addressed.

### Quick Stats
- **Critical Issues:** 3
- **High Priority Issues:** 5  
- **Medium Priority Issues:** 7
- **Low Priority Issues:** 13

---

## Issue Catalog

### Legend
- 🔴 **Critical** - Must fix immediately, blocks functionality or causes data issues
- 🟠 **High** - Should fix soon, security or reliability concerns
- 🟡 **Medium** - Should fix, improves stability and maintainability
- 🟢 **Low** - Nice to have, code quality improvements

---

## 🔴 Critical Issues

### Issue #1: Duplicate Recording Module (Major Technical Debt)

**Status:** `TODO`  
**Effort:** Low (1-2 hours)  
**Risk:** Low  

**Files Affected:**
- `src/renderer/modules/recording.js` (565 lines) - TO DELETE
- `src/renderer/modules/recording/` directory - TO KEEP

**Problem:**  
There are TWO implementations of the recording functionality. The monolithic `recording.js` file appears to be legacy code that was refactored into the modular `recording/` directory, but was never deleted.

**Evidence:**
- `src/renderer/modules/index.js` imports from `./recording/index.js`, not `./recording.js`
- The modular version has better error handling (compare `reader.onerror` implementations)
- Functions are duplicated with slight variations

**Impact:**
- Maintenance burden - changes must potentially be made in two places
- Confusion for developers
- Risk of inconsistent behavior

**Implementation Plan:**
1. Verify `recording.js` is not imported anywhere:
   ```bash
   grep -r "from './recording.js'" src/renderer/
   grep -r "from \"./recording.js\"" src/renderer/
   ```
2. Compare functionality between both versions to ensure nothing is lost
3. Delete `src/renderer/modules/recording.js`
4. Run tests to verify nothing breaks

---

### Issue #2: State Mutation Without Immutability

**Status:** `TODO`  
**Effort:** Medium (2-3 hours)  
**Risk:** Medium (could affect existing code that mutates state)  

**File:** `src/renderer/modules/state.js`

**Problem:**  
State objects are returned by reference, allowing direct mutation that bypasses the pub/sub notification system.

**Current Code (Lines 108-110):**
```javascript
export function getSettings() {
    return state.settings;  // Returns mutable reference!
}
```

**Impact:**
- Code can accidentally mutate state without triggering notifications
- Hard-to-debug issues when state changes unexpectedly
- Breaks the pub/sub pattern

**Implementation Plan:**

1. Update getter functions to return copies:

```javascript
// Settings
export function getSettings() {
    return { ...state.settings };
}

// Recording state (shallow copy sufficient for this structure)
export function getRecordingState() {
    return { ...state.recording };
}

// Mic test state
export function getMicTestState() {
    return { ...state.micTest };
}

// Statistics
export function getStatistics() {
    return { ...state.statistics };
}

// History (return copy of array)
export function getHistoryItems() {
    return [...state.historyItems];
}
```

2. Update code that relies on mutation patterns (search for patterns like `state.xxx = yyy` after getting state)

3. Consider using `Object.freeze()` in development mode to catch mutations early

---

### Issue #3: Missing IPC Listener Cleanup (Memory Leak)

**Status:** `TODO`  
**Effort:** Medium (2-3 hours)  
**Risk:** Low  

**File:** `src/preload.js`

**Problem:**  
Event listeners are registered via `ipcRenderer.on()` but never cleaned up. If the renderer reloads, listeners accumulate.

**Current Code (Lines 57-88):**
```javascript
onDownloadProgress: (callback) => {
    ipcRenderer.on('download-progress', (event, data) => callback(data));
},
```

**Implementation Plan:**

1. Update preload.js to return cleanup functions:

```javascript
// Events from main process
onDownloadProgress: (callback) => {
    const handler = (event, data) => callback(data);
    ipcRenderer.on('download-progress', handler);
    return () => ipcRenderer.removeListener('download-progress', handler);
},

onTestMicrophone: (callback) => {
    const handler = () => callback();
    ipcRenderer.on('test-microphone', handler);
    return () => ipcRenderer.removeListener('test-microphone', handler);
},

onRecordingStateChanged: (callback) => {
    const handler = (event, isRecording) => callback(isRecording);
    ipcRenderer.on('recording-state-changed', handler);
    return () => ipcRenderer.removeListener('recording-state-changed', handler);
},

// ... apply same pattern to all event listeners
```

2. Update `src/renderer/modules/index.js` to store and call cleanup functions:

```javascript
// Store cleanup functions
const cleanupFunctions = [];

function setupIPCListeners() {
    cleanupFunctions.push(
        window.voxtether.onRecordingStateChanged((isRecording) => { ... }),
        window.voxtether.onStatusChanged((status) => { ... }),
        // ... etc
    );
}

// Call on unload
window.addEventListener('beforeunload', () => {
    cleanupFunctions.forEach(cleanup => cleanup?.());
});
```

---

## 🟠 High Priority Issues

### Issue #4: No Input Validation on Model Name in Download Handler

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**File:** `src/main/ipc-handlers.js`, Lines 157-197

**Problem:**  
The `modelName` parameter is used directly in URL construction without validation.

**Implementation Plan:**

1. Add validation at the start of the handler:

```javascript
const VALID_MODELS = ['tiny', 'base', 'small', 'medium', 'large-v3', 'large-v3-turbo', 'distil-large-v3'];

ipcMain.handle(IPC_DOWNLOAD_MODEL, async (event, modelName) => {
    // Validate model name
    if (!modelName || typeof modelName !== 'string' || !VALID_MODELS.includes(modelName)) {
        return { success: false, error: 'Invalid model name' };
    }
    // ... rest of handler
});
```

2. Also add validation to `IPC_LOAD_MODEL` and `IPC_DELETE_MODEL` handlers

---

### Issue #5: SimulateTyping Not Implemented

**Status:** `TODO`  
**Effort:** Medium (requires decision)  
**Risk:** Low  

**Files:**
- `src/renderer/modules/recording/transcription.js`, Lines 199-208
- `src/renderer/index.html` (UI dropdown)

**Problem:**  
The "SimulateTyping" output mode falls through to clipboard copy without actually simulating typing.

**Implementation Plan:**

**Option A: Remove the option**
1. Remove from HTML dropdown:
   ```html
   <!-- Remove this line -->
   <option value="SimulateTyping">Simulate Typing</option>
   ```

**Option B: Implement the feature**
1. This would require using `robotjs` or similar native module to simulate keystrokes
2. Add to main process (not renderer) for security
3. Implement character-by-character typing with configurable delay

**Recommendation:** Option A (remove) for now, as typing simulation requires native dependencies and is complex to implement correctly.

---

### Issue #6: Unhandled Shell Injection Risk

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Medium  

**File:** `src/main/ipc-handlers.js`, Lines 294-300

**Problem:**  
`shell.openPath` and `shell.openExternal` are called without validation.

**Implementation Plan:**

```javascript
ipcMain.handle(IPC_OPEN_PATH, async (event, pathToOpen) => {
    // Validate path is within expected directories
    const userDataPath = getUserDataPath();
    const modelsPath = getModelsPath();
    const logsPath = getLogsPath();
    
    const normalizedPath = path.normalize(pathToOpen);
    const allowedPaths = [userDataPath, modelsPath, logsPath];
    
    const isAllowed = allowedPaths.some(allowed => 
        normalizedPath.startsWith(path.normalize(allowed))
    );
    
    if (!isAllowed) {
        console.warn('Attempted to open path outside allowed directories:', pathToOpen);
        return { success: false, error: 'Path not allowed' };
    }
    
    await shell.openPath(normalizedPath);
    return { success: true };
});

ipcMain.handle(IPC_OPEN_EXTERNAL, async (event, url) => {
    // Only allow HTTPS URLs and specific domains
    const allowedDomains = ['github.com', 'voxtether.com'];
    
    try {
        const urlObj = new URL(url);
        
        if (urlObj.protocol !== 'https:') {
            return { success: false, error: 'Only HTTPS URLs allowed' };
        }
        
        const isAllowedDomain = allowedDomains.some(domain => 
            urlObj.hostname === domain || urlObj.hostname.endsWith('.' + domain)
        );
        
        if (!isAllowedDomain) {
            console.warn('Attempted to open URL from non-allowed domain:', url);
            // Optionally still allow but log it
        }
        
        await shell.openExternal(url);
        return { success: true };
    } catch (error) {
        return { success: false, error: 'Invalid URL' };
    }
});
```

---

### Issue #7: Backend Health Polling Without Exponential Backoff

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Low  

**File:** `src/renderer/modules/index.js`, Lines 301-357

**Problem:**  
Health checks run every 5 seconds regardless of backend status, wasting resources when backend is offline.

**Implementation Plan:**

```javascript
// Health check configuration
const HEALTH_CHECK_CONFIG = {
    initialInterval: 5000,      // 5 seconds
    maxInterval: 60000,         // 1 minute max
    backoffMultiplier: 2,
    resetOnSuccess: true
};

let currentBackoffMs = HEALTH_CHECK_CONFIG.initialInterval;
let healthCheckTimeoutId = null;

async function checkBackendHealthWithBackoff() {
    const isHealthy = await checkBackendHealth();
    
    if (isHealthy) {
        // Reset backoff on success
        currentBackoffMs = HEALTH_CHECK_CONFIG.initialInterval;
    } else {
        // Increase backoff on failure
        currentBackoffMs = Math.min(
            currentBackoffMs * HEALTH_CHECK_CONFIG.backoffMultiplier,
            HEALTH_CHECK_CONFIG.maxInterval
        );
    }
    
    // Schedule next check
    healthCheckTimeoutId = setTimeout(checkBackendHealthWithBackoff, currentBackoffMs);
}

function startHealthMonitoring() {
    // Clear any existing timeout
    if (healthCheckTimeoutId) {
        clearTimeout(healthCheckTimeoutId);
    }
    
    // Start with initial check
    checkBackendHealthWithBackoff();
}

function stopHealthMonitoring() {
    if (healthCheckTimeoutId) {
        clearTimeout(healthCheckTimeoutId);
        healthCheckTimeoutId = null;
    }
}
```

---

### Issue #8: Recording State Race Condition

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Medium  

**File:** `src/renderer/modules/recording/index.js`, Lines 79-122

**Problem:**  
Recording state is checked then set with async operations in between, allowing potential double-starts.

**Implementation Plan:**

```javascript
export async function handleStartRecording() {
    const state = getRecordingState();
    if (state.isRecording) return;
    
    // Set flag IMMEDIATELY before any async work
    setRecordingState({ isRecording: true });
    
    try {
        const micSelect = document.getElementById('mic-device-select');
        const deviceId = micSelect ? micSelect.value : undefined;
        const constraints = getAudioConstraints(deviceId);

        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        
        // Update state with stream and recorder
        setRecordingState({
            stream: stream,
            audioChunks: [],
            startTime: Date.now()
        });
        
        const state = getRecordingState();
        state.mediaRecorder = createMediaRecorder(
            stream,
            (data) => {
                const currentState = getRecordingState();
                currentState.audioChunks.push(data);
            },
            async () => {
                await processRecording();
            }
        );

        state.mediaRecorder.start(100);
        setRecordingState(state);
        
        setupRecordingLevelMonitor();
        
        console.log('Recording started');
        updateRecordingStatus('recording');

    } catch (error) {
        // Reset state on failure
        setRecordingState({ 
            isRecording: false,
            stream: null,
            mediaRecorder: null,
            audioChunks: []
        });
        
        console.error('Failed to start recording:', error);
        showNotification('Failed to access microphone: ' + error.message, 'error');
        updateRecordingStatus('error');
        await window.voxtether.hideOverlay();
    }
}
```

---

## 🟡 Medium Priority Issues

### Issue #9: Inconsistent Error Handling in FileReader

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**File:** `src/renderer/modules/recording/transcription.js`, Lines 137-142

**Problem:**  
Good error handling exists in one file but not the other (duplicate file issue).

**Resolution:** This will be resolved when Issue #1 (duplicate file) is fixed.

---

### Issue #10: No Timeout on Backend Transcription Request

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**File:** `src/main/ipc-handlers.js`, Lines 218-285

**Implementation Plan:**

```javascript
ipcMain.handle(IPC_TRANSCRIBE, async (event, audioPath, language) => {
    return new Promise((resolve, _reject) => {
        try {
            // ... existing validation code ...
            
            const req = http.request(options, (res) => {
                // ... existing response handling ...
            });

            // Add timeout (2 minutes for long audio files)
            req.setTimeout(120000, () => {
                req.destroy();
                resolve({ success: false, error: 'Transcription timeout - audio file may be too long' });
            });

            req.on('error', (error) => {
                resolve({ success: false, error: error.message });
            });

            req.write(bodyBuffer);
            req.end();
        } catch (error) {
            resolve({ success: false, error: error.message });
        }
    });
});
```

---

### Issue #11: Element Cache Never Invalidated

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**File:** `src/renderer/modules/utils.js`

**Implementation Plan:**

```javascript
// Add cache invalidation function
export function clearElementCache() {
    elementCache.clear();
}

// Optionally add cache with WeakRef for auto-cleanup
// (Only if targeting modern browsers)
```

---

### Issue #12: Magic Numbers in Audio Processing

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**Files:**
- `src/renderer/modules/recording/audio-processing.js`
- `src/renderer/modules/mictest.js`

**Implementation Plan:**

Create `src/renderer/modules/audio-constants.js`:

```javascript
/**
 * Audio Processing Configuration Constants
 */
export const AUDIO_CONFIG = {
    // FFT (Fast Fourier Transform) size - must be power of 2
    // Larger = more frequency resolution but slower
    FFT_SIZE: 256,
    
    // Smoothing time constant (0-1)
    // Higher = smoother visualization but slower response
    SMOOTHING_TIME_CONSTANT: 0.8,
    
    // Normalization factor (half of max byte value 255)
    NORMALIZATION_FACTOR: 128,
    
    // MediaRecorder data collection interval (ms)
    DATA_COLLECTION_INTERVAL: 100,
    
    // Volume meter max percentage
    MAX_VOLUME_PERCENT: 100
};
```

Then update the audio processing files to import and use these constants.

---

### Issue #13: Tests Use Explicit Waits

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Low  

**File:** `tests/electron.spec.js`, Line 557

**Current Code:**
```javascript
await window.waitForTimeout(2000);
```

**Implementation Plan:**

```javascript
// Instead of:
await window.waitForTimeout(2000);
const statusText = window.locator('#status-indicator .status-text');
await expect(statusText).toHaveText('Backend Offline');

// Use assertion-based waiting:
const statusText = window.locator('#status-indicator .status-text');
await expect(statusText).toHaveText('Backend Offline', { timeout: 10000 });
```

---

### Issue #14: No Unit Tests

**Status:** `TODO`  
**Effort:** High (4-8 hours)  
**Risk:** Low  

**Problem:**  
Only E2E tests exist. Critical modules lack unit test coverage.

**Implementation Plan:**

1. Set up Jest or Vitest for unit testing
2. Add to `package.json`:
   ```json
   {
     "scripts": {
       "test:unit": "vitest run",
       "test:unit:watch": "vitest"
     },
     "devDependencies": {
       "vitest": "^1.0.0"
     }
   }
   ```

3. Create unit tests for:
   - `src/renderer/modules/state.js` - State management
   - `src/renderer/modules/utils.js` - Utility functions
   - `src/main/settings-manager.js` - Settings persistence
   - `src/shared/constants.js` - Constants validation

4. Example test for state.js:
   ```javascript
   // tests/unit/state.test.js
   import { describe, it, expect, beforeEach } from 'vitest';
   import { getSettings, setSettings, subscribe } from '../../src/renderer/modules/state.js';
   
   describe('State Management', () => {
       beforeEach(() => {
           // Reset state before each test
       });
       
       it('should return immutable settings copy', () => {
           setSettings({ language: 'en' });
           const settings1 = getSettings();
           const settings2 = getSettings();
           expect(settings1).not.toBe(settings2);
           expect(settings1).toEqual(settings2);
       });
       
       it('should notify subscribers on change', () => {
           const callback = vi.fn();
           subscribe('settings', callback);
           setSettings({ language: 'en' });
           expect(callback).toHaveBeenCalledWith({ language: 'en' });
       });
   });
   ```

---

### Issue #15: Inconsistent Async/Await Usage

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Low  

**File:** `src/main/ipc-handlers.js`

**Implementation Plan:**

Update all handlers to be consistent:

```javascript
// Before (inconsistent)
ipcMain.handle(IPC_OPEN_PATH, (event, pathToOpen) => {
    shell.openPath(pathToOpen);
});

// After (consistent)
ipcMain.handle(IPC_OPEN_PATH, async (event, pathToOpen) => {
    try {
        await shell.openPath(pathToOpen);
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});
```

---

## 🟢 Low Priority Issues

### Issue #16: Large IPC Handlers File

**Status:** `TODO`  
**Effort:** Medium (2-3 hours)  
**Risk:** Low  

**File:** `src/main/ipc-handlers.js` (594 lines)

**Implementation Plan:**

Split into multiple files:
```
src/main/ipc-handlers/
├── index.js              # Main export, combines all handlers
├── settings.js           # Settings-related handlers
├── recording.js          # Recording control handlers
├── files.js              # File dialog and operations
├── backend.js            # Backend communication handlers
└── updater.js            # Auto-update handlers
```

---

### Issue #17: Circular Dependency Workarounds

**Status:** `TODO`  
**Effort:** Medium (2 hours)  
**Risk:** Medium  

**Files:**
- `src/renderer/modules/recording/preview.js`
- `src/renderer/modules/recording/transcription.js`

**Current Code:**
```javascript
const { showTranscriptionPreviewModal } = await import('./preview.js');
```

**Implementation Plan:**

Extract shared output logic to a separate module:
```
src/renderer/modules/recording/
├── index.js
├── output.js              # NEW: Contains performTranscriptionOutput
├── preview.js             # Imports from output.js
├── transcription.js       # Imports from output.js
└── ...
```

---

### Issue #18: Missing JSDoc Return Types

**Status:** `TODO`  
**Effort:** Medium (2-3 hours)  
**Risk:** Low  

Add consistent JSDoc annotations across all modules. Consider using TypeScript in the future.

---

### Issue #19: Console Logs in Production

**Status:** `TODO`  
**Effort:** Low (1 hour)  
**Risk:** Low  

**Implementation Plan:**

Create `src/shared/logger.js`:

```javascript
const isDebug = process.env.NODE_ENV === 'development' || 
                process.argv.includes('--debug');

export const logger = {
    debug: (...args) => isDebug && console.log('[DEBUG]', ...args),
    info: (...args) => console.log('[INFO]', ...args),
    warn: (...args) => console.warn('[WARN]', ...args),
    error: (...args) => console.error('[ERROR]', ...args)
};
```

---

### Issue #20: Hardcoded Strings in UI

**Status:** `TODO`  
**Effort:** High (4-6 hours)  
**Risk:** Low  

**Implementation Plan:**

For future i18n support, extract strings to:
```
src/renderer/i18n/
├── index.js
├── en.js
└── ...
```

---

### Issue #21: CSS Lacks Scoping

**Status:** `TODO`  
**Effort:** Medium (3-4 hours)  
**Risk:** Low  

Consider adopting BEM naming convention for class names.

---

### Issue #22: Missing `rel="noopener"` Consideration

**Status:** `N/A`  
**Notes:** Not applicable as links use `shell.openExternal`

---

### Issue #23: No Error Boundary for Async Operations

**Status:** `TODO`  
**Effort:** Low (30 min)  
**Risk:** Low  

**File:** `src/renderer/modules/index.js`

**Implementation Plan:**

```javascript
document.addEventListener('DOMContentLoaded', async () => {
    try {
        await initialize();
    } catch (error) {
        console.error('Critical initialization error:', error);
        updateStatus('Initialization Failed', 'error');
        showNotification('Failed to initialize application: ' + error.message, 'error');
    }
});
```

---

### Issue #24: Missing ARIA Labels for Dynamic Content

**Status:** `TODO`  
**Effort:** Medium (2 hours)  
**Risk:** Low  

Add appropriate ARIA attributes to:
- Model cards (`role="listitem"`, `aria-label`)
- History items (`role="article"`)
- Dynamic status updates (`aria-live="polite"`)

---

### Issue #25: Unused `_stopHealthMonitoring` Function

**Status:** `TODO`  
**Effort:** Low (15 min)  
**Risk:** Low  

**File:** `src/renderer/modules/index.js`

**Implementation Plan:**

Either use it in cleanup:
```javascript
window.addEventListener('beforeunload', () => {
    stopHealthMonitoring();
    // ... other cleanup
});
```

Or remove if truly unused.

---

### Issue #26: Test Coverage Gaps

**Status:** `TODO`  
**Effort:** High (6-8 hours)  
**Risk:** Low  

Add E2E tests for:
- Error states (mock backend failures)
- Theme switching visual effects
- Settings persistence (save and reload)
- Toast notification display and dismissal

---

### Issue #27: No Request Retry Logic

**Status:** `TODO`  
**Effort:** Medium (2 hours)  
**Risk:** Low  

**File:** `src/main/backend-client.js`

**Implementation Plan:**

```javascript
async function backendRequestWithRetry(method, endpoint, body = null, retries = 3) {
    for (let attempt = 1; attempt <= retries; attempt++) {
        try {
            return await backendRequest(method, endpoint, body);
        } catch (error) {
            if (attempt === retries) throw error;
            
            // Exponential backoff
            const delay = Math.min(1000 * Math.pow(2, attempt - 1), 10000);
            await new Promise(resolve => setTimeout(resolve, delay));
        }
    }
}
```

---

### Issue #28: Preview Modal Duration Not Passed Correctly

**Status:** `TODO`  
**Effort:** Low  
**Risk:** Low  

**Resolution:** This will be automatically resolved when Issue #1 (duplicate file deletion) is completed.

---

## Implementation Roadmap

### Phase 1: Critical Fixes (Week 1)
| Issue | Task | Est. Time |
|-------|------|-----------|
| #1 | Delete duplicate recording.js | 1 hour |
| #2 | Fix state immutability | 2-3 hours |
| #3 | Fix IPC listener memory leaks | 2-3 hours |

### Phase 2: Security & Reliability (Week 2)
| Issue | Task | Est. Time |
|-------|------|-----------|
| #4 | Add model name validation | 30 min |
| #5 | Remove/implement SimulateTyping | 1 hour |
| #6 | Add shell/URL validation | 1 hour |
| #7 | Implement health check backoff | 1 hour |
| #8 | Fix recording race condition | 1 hour |
| #10 | Add transcription timeout | 30 min |

### Phase 3: Code Quality (Week 3)
| Issue | Task | Est. Time |
|-------|------|-----------|
| #12 | Extract audio constants | 30 min |
| #13 | Fix test explicit waits | 1 hour |
| #15 | Fix async/await consistency | 1 hour |
| #23 | Add error boundary | 30 min |
| #25 | Use/remove unused function | 15 min |

### Phase 4: Architecture (Week 4)
| Issue | Task | Est. Time |
|-------|------|-----------|
| #14 | Add unit tests | 6-8 hours |
| #16 | Split IPC handlers | 2-3 hours |
| #17 | Fix circular dependencies | 2 hours |

### Phase 5: Polish (Future)
| Issue | Task | Est. Time |
|-------|------|-----------|
| #18 | Add JSDoc types | 2-3 hours |
| #19 | Add logging utility | 1 hour |
| #24 | Add ARIA labels | 2 hours |
| #26 | Expand test coverage | 6-8 hours |
| #27 | Add retry logic | 2 hours |

---

## Verification Checklist

After implementing fixes, verify:

- [ ] `npm run lint` passes with no errors
- [ ] `npm test` passes all E2E tests
- [ ] Application starts without errors
- [ ] Recording workflow functions correctly
- [ ] Settings save and persist across restarts
- [ ] Backend offline state is handled gracefully
- [ ] No console errors in DevTools
- [ ] Memory usage is stable over extended use

---

## Notes

- All time estimates are rough and may vary based on implementation complexity
- Some issues may reveal additional problems during implementation
- Consider creating a feature branch for large changes
- Run the full test suite after each major change

---

*Generated from automated code review. Manual verification recommended before implementation.*
