/**
 * Transcription Provider Abstraction
 *
 * Provides a unified interface for different transcription backends.
 * Supports local backend (faster-whisper) and OpenAI API.
 */

const fs = require('fs');
const path = require('path');
const http = require('http');
const https = require('https');
const crypto = require('crypto');

// OpenAI file size limit (25 MB)
const OPENAI_MAX_FILE_SIZE_MB = 25;

// Request timeout (60 seconds)
const REQUEST_TIMEOUT_MS = 60000;

/**
 * Generate a unique boundary string for multipart form data
 * Uses both timestamp and cryptographic randomness for uniqueness
 * @returns {string} A unique boundary string
 */
function generateBoundary() {
    const timestamp = Date.now().toString(16);
    const random = crypto.randomBytes(16).toString('hex');
    return `----WebKitFormBoundary${timestamp}${random}`;
}

/**
 * Sanitize filename for use in Content-Disposition header
 * Removes potentially dangerous characters that could break multipart boundaries
 * @param {string} filename - The original filename
 * @returns {string} Sanitized filename
 */
function sanitizeFilename(filename) {
    // Remove or replace characters that could break headers
    return filename
        .replace(/["\r\n\\]/g, '_')  // Remove quotes, CRLF, backslashes
        .replace(/[^\x20-\x7E]/g, '_');  // Remove non-ASCII characters
}

/**
 * Transcribe using local backend
 * @param {string} audioPath - Path to audio file
 * @param {string} language - Language code (e.g., 'en', 'auto')
 * @param {number} backendPort - Local backend port
 * @returns {Promise<{success: boolean, data?: object, error?: string}>}
 */
async function transcribeLocal(audioPath, language, backendPort) {
    return new Promise((resolve) => {
        const boundary = generateBoundary();
        const audioData = fs.readFileSync(audioPath);
        const audioFileName = sanitizeFilename(path.basename(audioPath));

        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        const bodyEnd = `\r\n--${boundary}\r\n` +
            `Content-Disposition: form-data; name="language"\r\n\r\n${language || 'auto'}\r\n` +
            `--${boundary}--\r\n`;

        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        const options = {
            hostname: '127.0.0.1',
            port: backendPort,
            path: '/api/transcribe',
            method: 'POST',
            timeout: REQUEST_TIMEOUT_MS,
            headers: {
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': bodyBuffer.length
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve({ success: true, data: JSON.parse(data) });
                } catch {
                    resolve({ success: false, error: 'Failed to parse response' });
                }
            });
        });

        req.on('timeout', () => {
            req.destroy();
            resolve({ success: false, error: 'Transcription request timed out' });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: error.message });
        });

        req.write(bodyBuffer);
        req.end();
    });
}

/**
 * Transcribe using OpenAI API
 * @param {string} audioPath - Path to audio file
 * @param {string} language - Language code (e.g., 'en', 'auto')
 * @param {string} apiKey - OpenAI API key
 * @param {string} model - OpenAI model to use (default: 'whisper-1')
 * @returns {Promise<{success: boolean, data?: object, error?: string}>}
 */
async function transcribeOpenAI(audioPath, language, apiKey, model = 'whisper-1') {
    return new Promise((resolve) => {
        if (!apiKey) {
            resolve({ success: false, error: 'OpenAI API key not configured' });
            return;
        }

        // Enforce OpenAI's 25 MB audio file size limit before uploading
        const stats = fs.statSync(audioPath);
        const fileSizeMB = stats.size / (1024 * 1024);
        if (fileSizeMB > OPENAI_MAX_FILE_SIZE_MB) {
            resolve({ success: false, error: `Audio file exceeds OpenAI's ${OPENAI_MAX_FILE_SIZE_MB}MB limit` });
            return;
        }

        const audioData = fs.readFileSync(audioPath);
        const audioFileName = sanitizeFilename(path.basename(audioPath));
        const boundary = generateBoundary();

        // Build multipart form data
        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="${audioFileName}"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';

        let bodyEnd = `\r\n--${boundary}\r\n`;
        bodyEnd += `Content-Disposition: form-data; name="model"\r\n\r\n${model}\r\n`;

        if (language && language !== 'auto') {
            bodyEnd += `--${boundary}\r\n`;
            bodyEnd += `Content-Disposition: form-data; name="language"\r\n\r\n${language}\r\n`;
        }

        bodyEnd += `--${boundary}\r\n`;
        bodyEnd += 'Content-Disposition: form-data; name="response_format"\r\n\r\nverbose_json\r\n';
        bodyEnd += `--${boundary}--\r\n`;

        const bodyBuffer = Buffer.concat([
            Buffer.from(body),
            audioData,
            Buffer.from(bodyEnd)
        ]);

        const options = {
            hostname: 'api.openai.com',
            path: '/v1/audio/transcriptions',
            method: 'POST',
            timeout: REQUEST_TIMEOUT_MS,
            headers: {
                'Authorization': `Bearer ${apiKey}`,
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': bodyBuffer.length
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    const result = JSON.parse(data);

                    if (res.statusCode !== 200) {
                        resolve({
                            success: false,
                            error: result.error?.message || `API error: ${res.statusCode}`
                        });
                        return;
                    }

                    // Validate required fields in response
                    if (!result.text) {
                        resolve({ success: false, error: 'Invalid response: missing text field' });
                        return;
                    }

                    resolve({
                        success: true,
                        data: {
                            text: result.text,
                            language: result.language || 'unknown',
                            duration: result.duration || 0,
                            success: true
                        }
                    });
                } catch {
                    resolve({ success: false, error: 'Failed to parse OpenAI response' });
                }
            });
        });

        req.on('timeout', () => {
            req.destroy();
            resolve({ success: false, error: 'OpenAI request timed out' });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: `OpenAI API error: ${error.message}` });
        });

        req.write(bodyBuffer);
        req.end();
    });
}

/**
 * Test OpenAI API connection by making a minimal request
 * @param {string} apiKey - OpenAI API key
 * @returns {Promise<{success: boolean, error?: string}>}
 */
async function testOpenAIConnection(apiKey) {
    return new Promise((resolve) => {
        if (!apiKey) {
            resolve({ success: false, error: 'API key is required' });
            return;
        }

        // Test by checking the models endpoint
        const options = {
            hostname: 'api.openai.com',
            path: '/v1/models',
            method: 'GET',
            timeout: 30000, // 30 second timeout for connection test
            headers: {
                'Authorization': `Bearer ${apiKey}`
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode === 200) {
                    resolve({ success: true });
                } else {
                    try {
                        const result = JSON.parse(data);
                        resolve({
                            success: false,
                            error: result.error?.message || `API error: ${res.statusCode}`
                        });
                    } catch {
                        resolve({ success: false, error: `API error: ${res.statusCode}` });
                    }
                }
            });
        });

        req.on('timeout', () => {
            req.destroy();
            resolve({ success: false, error: 'Connection test timed out' });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: `Connection error: ${error.message}` });
        });

        req.end();
    });
}

/**
 * Main transcription function - routes to appropriate provider
 * @param {string} audioPath - Path to audio file
 * @param {object} options - Transcription options
 * @returns {Promise<{success: boolean, data?: object, error?: string}>}
 */
async function transcribe(audioPath, options = {}) {
    const {
        provider = 'local',
        language = 'auto',
        backendPort = 5678,
        openaiApiKey = '',
        openaiModel = 'whisper-1'
    } = options;

    if (provider === 'openai') {
        return transcribeOpenAI(audioPath, language, openaiApiKey, openaiModel);
    } else {
        return transcribeLocal(audioPath, language, backendPort);
    }
}

module.exports = {
    transcribe,
    transcribeLocal,
    transcribeOpenAI,
    testOpenAIConnection
};
