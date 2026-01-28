# VoxTether Frontend - Suggested Features

This document outlines potential features and improvements that could be added to the VoxTether Electron frontend.

---

## High Priority

### 1. Toast Notification System

**Current State:** Errors display via browser `alert()`, success messages only log to console.

**Suggestion:** Implement a toast notification system for non-blocking user feedback.

| Notification Type | Use Case |
|-------------------|----------|
| Success | Settings saved, transcription complete |
| Error | Connection failed, transcription failed |
| Info | Model loading, backend status changes |
| Warning | Low disk space, unsaved changes |

**Implementation Notes:**
- Position in bottom-right corner
- Auto-dismiss after 3-5 seconds
- Stacking for multiple notifications
- Click to dismiss

---

### 2. Recording History

**Current State:** Transcriptions are ephemeral; only the last push-to-talk result is retained in memory.

**Suggestion:** Add a history panel showing recent transcriptions.

| Feature | Description |
|---------|-------------|
| History List | Last 20-50 transcriptions with timestamps |
| Quick Copy | One-click copy to clipboard |
| Search | Filter history by text content |
| Export | Export history to file |
| Clear | Option to clear history |

**Storage:** Use local storage or settings file with configurable retention.

---

### 3. Recording Indicator Overlay

**Current State:** Recording indicator only visible in the settings window sidebar.

**Suggestion:** Add a floating overlay indicator visible over all windows.

| Feature | Description |
|---------|-------------|
| Position | Configurable corner of screen |
| Size | Small, medium, large options |
| Opacity | Adjustable transparency |
| Animation | Pulsing red dot during recording |

---

### 4. Keyboard Shortcut for Window Toggle

**Current State:** No keyboard shortcut to show/hide the settings window.

**Suggestion:** Add a global shortcut (e.g., `Ctrl+Shift+V`) to toggle window visibility.

---

## Medium Priority

### 5. Audio Device Hot-Swap Detection

**Current State:** Audio devices must be manually refreshed.

**Suggestion:** Automatically detect when audio devices are connected/disconnected.

| Feature | Description |
|---------|-------------|
| Auto-refresh | Update device list on hardware changes |
| Notification | Alert when active device disconnects |
| Fallback | Automatically switch to default device |

---

### 6. Multiple Hotkey Profiles

**Current State:** Single global hotkey configuration.

**Suggestion:** Support multiple hotkey profiles for different use cases.

| Profile Example | Hotkey | Language | Output Mode |
|-----------------|--------|----------|-------------|
| Default | Ctrl+Shift+Space | Auto | Clipboard+Paste |
| Quick English | Ctrl+Alt+E | English | Paste |
| Spanish Notes | Ctrl+Alt+S | Spanish | Clipboard |

---

### 7. Transcription Corrections

**Current State:** Transcriptions cannot be edited before pasting.

**Suggestion:** Add a confirmation dialog with edit capability.

| Feature | Description |
|---------|-------------|
| Preview | Show transcription before inserting |
| Edit | Allow quick corrections |
| Insert | Confirm and insert text |
| Cancel | Discard transcription |

**Note:** Make this optional via settings for users who prefer instant paste.

---

### 8. Audio Level Indicator During Recording

**Current State:** No visual feedback during push-to-talk recording (only in mic test).

**Suggestion:** Show real-time audio level during actual recordings.

| Feature | Description |
|---------|-------------|
| Mini meter | Small volume indicator in tray/overlay |
| Peak warning | Visual warning if audio is clipping |
| Silence detection | Alert if no audio detected |

---

### 9. Custom Vocabulary/Dictionary

**Current State:** No way to add custom words or names.

**Suggestion:** Allow users to define custom vocabulary for better recognition.

| Feature | Description |
|---------|-------------|
| Word list | Add names, acronyms, technical terms |
| Phonetic hints | Help with pronunciation of unusual words |
| Import/Export | Share vocabulary lists |

**Note:** Requires backend support for vocabulary integration with faster-whisper.

---

### 10. Statistics Dashboard

**Current State:** No usage statistics tracked.

**Suggestion:** Add optional usage statistics on the About page.

| Statistic | Description |
|-----------|-------------|
| Total recordings | Number of push-to-talk sessions |
| Total duration | Cumulative recording time |
| Characters transcribed | Total text output |
| Average accuracy | If self-correction data available |
| Model usage | Breakdown by model used |

---

## Low Priority / Future Considerations

### 11. Plugins/Extensions System

**Suggestion:** Allow third-party plugins for extended functionality.

| Plugin Type | Examples |
|-------------|----------|
| Output formatters | Markdown, HTML, formatted notes |
| Post-processors | Auto-punctuation, spell check |
| Integrations | Send to specific apps, cloud sync |

---

### 12. Voice Commands

**Suggestion:** Recognize special voice commands during dictation.

| Command | Action |
|---------|--------|
| "New line" | Insert line break |
| "New paragraph" | Insert paragraph break |
| "Delete that" | Remove last phrase |
| "Scratch that" | Clear current recording |

---

### 13. Multi-Language in Single Session

**Current State:** Language must be set before recording.

**Suggestion:** Detect language switches within a single recording.

---

### 14. Batch Transcription

**Current State:** Transcribe page handles one file at a time.

**Suggestion:** Allow selecting multiple files for batch processing.

| Feature | Description |
|---------|-------------|
| Multi-select | Choose multiple audio files |
| Queue display | Show processing queue |
| Progress | Overall and per-file progress |
| Bulk export | Export all transcripts at once |

---

### 15. Cloud Backup (Optional)

**Suggestion:** Optional backup of settings and history to cloud storage.

| Feature | Description |
|---------|-------------|
| Provider | OneDrive, Google Drive, Dropbox |
| Sync | Settings, history, custom vocabulary |
| Privacy | End-to-end encryption |

**Note:** Must remain fully optional to maintain offline-first philosophy.

---

### 16. Onboarding Wizard

**Current State:** First-time users must figure out setup themselves.

**Suggestion:** Add a first-run wizard for new users.

| Step | Content |
|------|---------|
| Welcome | Introduction to VoxTether |
| Microphone | Select and test microphone |
| Model | Download first model |
| Hotkey | Configure push-to-talk key |
| Tutorial | Quick interactive demo |

---

### 17. Accessibility Improvements

**Suggestion:** Enhanced accessibility features.

| Feature | Description |
|---------|-------------|
| Screen reader announcements | ARIA live regions for status changes |
| Reduced motion | Option to disable animations |
| Font size | Adjustable UI font size |
| High contrast | Enhanced high contrast mode |

---

### 18. Electron Auto-Updater

**Current State:** Updates require manual download.

**Suggestion:** Implement automatic update checking and installation.

| Feature | Description |
|---------|-------------|
| Check on startup | Look for new versions |
| Notification | Alert when update available |
| Background download | Download update in background |
| Install on quit | Apply update when exiting |

---

## Implementation Considerations

### Backward Compatibility
- All new features should be optional
- Default settings should match current behavior
- Settings migration for upgrades

### Performance Impact
- Monitor startup time impact
- Test with large history/vocabulary
- Profile animation performance

### Testing
- Add E2E tests for new features
- Test across Windows 10/11
- Test with various screen sizes and DPI

### Documentation
- Update FRONTEND-FEATURES.md for each new feature
- Add user guides for complex features
- Include keyboard shortcuts reference

---

## Community Feedback

This list is based on typical dictation software features and potential user needs. Actual implementation priority should be driven by:

1. User feedback and feature requests
2. Issue frequency and severity
3. Implementation complexity
4. Backend dependencies

---

## See Also

- [Frontend Features](FRONTEND-FEATURES.md) - Current feature documentation
- [Architecture](ARCHITECTURE.md) - System design
- [GitHub Issues](https://github.com/KennethHeine/VoxTether/issues) - Feature requests
