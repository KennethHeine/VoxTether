/**
 * VoxTether Audio Constants
 *
 * Centralized audio-related configuration values.
 * These constants control audio processing, visualization, and recording behavior.
 */

// ============================================================================
// Audio Analyser Configuration
// ============================================================================

/**
 * FFT size for recording level meter (smaller = faster, less detailed)
 * Must be a power of 2 between 32 and 32768
 */
export const RECORDING_FFT_SIZE = 256;

/**
 * FFT size for mic test waveform visualization (larger = more detailed)
 * Must be a power of 2 between 32 and 32768
 */
export const MIC_TEST_FFT_SIZE = 2048;

/**
 * Smoothing time constant for audio analyser (0 = no smoothing, 1 = max smoothing)
 * Higher values make visualization smoother but less responsive
 */
export const AUDIO_SMOOTHING_TIME_CONSTANT = 0.8;

// ============================================================================
// Audio Level Calculation
// ============================================================================

/**
 * Normalization factor for converting byte frequency data to percentage
 * Byte values range 0-255, so 128 is the midpoint for level calculation
 */
export const AUDIO_LEVEL_NORMALIZATION = 128;

/**
 * Maximum level percentage (caps the display at 100%)
 */
export const MAX_LEVEL_PERCENT = 100;

/**
 * RMS scaling factor for mic test visualization
 * Higher values make quiet sounds more visible
 */
export const RMS_SCALE_FACTOR = 3;

/**
 * Peak level decay rate (0-1, lower = slower decay)
 * Controls how fast the peak indicator falls after a loud sound
 */
export const PEAK_DECAY_RATE = 0.98;

// ============================================================================
// MediaRecorder Configuration
// ============================================================================

/**
 * Interval in milliseconds for collecting audio data chunks during recording
 */
export const MEDIA_RECORDER_TIMESLICE_MS = 100;

// ============================================================================
// WAV File Configuration
// ============================================================================

/**
 * Number of audio channels for WAV output (1 = mono)
 */
export const WAV_NUM_CHANNELS = 1;

/**
 * Audio format for WAV output (1 = PCM)
 */
export const WAV_FORMAT_PCM = 1;

/**
 * Bit depth for WAV output
 */
export const WAV_BIT_DEPTH = 16;

/**
 * WAV file header size in bytes
 */
export const WAV_HEADER_SIZE = 44;

// ============================================================================
// Visualization Colors (for canvas drawing)
// ============================================================================

export const WAVEFORM_COLORS = {
    dark: {
        background: '#202020',
        line: '#4682B4',
        centerLine: '#404040'
    },
    light: {
        background: '#f9f9f9',
        line: '#0078d4',
        centerLine: '#e0e0e0'
    }
};

/**
 * Line width for waveform visualization
 */
export const WAVEFORM_LINE_WIDTH = 2;

/**
 * Line width for center line in waveform
 */
export const CENTER_LINE_WIDTH = 1;
