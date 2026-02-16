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
const { BACKEND_HOST, BACKEND_PORT } = require('../shared/constants.js');

// OpenAI file size limit (25 MB)
const OPENAI_MAX_FILE_SIZE_MB = 25;

// Azure file size limit (audio must be less than 60 seconds for REST API, ~10 MB practical limit)
const AZURE_MAX_FILE_SIZE_MB = 10;

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
    // Remove or replace characters that could break headers or enable traversal
    return filename
        .replace(/["\r\n\\/]/g, '_')  // Remove quotes, CRLF, backslashes, forward slashes
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
            hostname: BACKEND_HOST,
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
            resolve({ success: false, error: `Audio file size (${fileSizeMB.toFixed(2)}MB) exceeds OpenAI's ${OPENAI_MAX_FILE_SIZE_MB}MB limit` });
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
 * Transcribe using Azure Speech-to-Text REST API
 * @param {string} audioPath - Path to audio file
 * @param {string} language - Language code (e.g., 'en-US', 'auto')
 * @param {string} speechKey - Azure Speech Services subscription key
 * @param {string} speechRegion - Azure Speech Services region (e.g., 'eastus')
 * @returns {Promise<{success: boolean, data?: object, error?: string}>}
 */
async function transcribeAzure(audioPath, language, speechKey, speechRegion) {
    return new Promise((resolve) => {
        if (!speechKey) {
            resolve({ success: false, error: 'Azure Speech key not configured' });
            return;
        }

        if (!speechRegion) {
            resolve({ success: false, error: 'Azure Speech region not configured' });
            return;
        }

        // Validate region format (alphanumeric and hyphens only)
        if (!/^[a-zA-Z0-9-]+$/.test(speechRegion)) {
            resolve({ success: false, error: 'Invalid Azure region format' });
            return;
        }

        // Enforce file size limit
        const stats = fs.statSync(audioPath);
        const fileSizeMB = stats.size / (1024 * 1024);
        if (fileSizeMB > AZURE_MAX_FILE_SIZE_MB) {
            resolve({ success: false, error: `Audio file size (${fileSizeMB.toFixed(2)}MB) exceeds Azure REST API's ${AZURE_MAX_FILE_SIZE_MB}MB limit` });
            return;
        }

        const audioData = fs.readFileSync(audioPath);

        // Map language codes: Azure expects BCP-47 format (e.g., 'en-US')
        // If 'auto' or short code, default to 'en-US'
        let azureLanguage = language;
        if (!language || language === 'auto') {
            azureLanguage = 'en-US';
        } else if (language.length === 2) {
            // Map common 2-letter codes to BCP-47
            const langMap = {
                'en': 'en-US', 'es': 'es-ES', 'fr': 'fr-FR', 'de': 'de-DE',
                'it': 'it-IT', 'pt': 'pt-BR', 'nl': 'nl-NL', 'ru': 'ru-RU',
                'zh': 'zh-CN', 'ja': 'ja-JP', 'ko': 'ko-KR', 'ar': 'ar-SA',
                'hi': 'hi-IN', 'pl': 'pl-PL', 'sv': 'sv-SE', 'da': 'da-DK',
                'fi': 'fi-FI', 'no': 'nb-NO', 'tr': 'tr-TR', 'cs': 'cs-CZ',
                'uk': 'uk-UA', 'el': 'el-GR', 'he': 'he-IL', 'th': 'th-TH',
                'vi': 'vi-VN', 'id': 'id-ID', 'ms': 'ms-MY', 'ro': 'ro-RO',
                'hu': 'hu-HU', 'bg': 'bg-BG', 'hr': 'hr-HR', 'sk': 'sk-SK',
                'sl': 'sl-SI', 'ca': 'ca-ES', 'ta': 'ta-IN'
            };
            azureLanguage = langMap[language] || 'en-US';
        }

        const queryParams = `language=${encodeURIComponent(azureLanguage)}&format=detailed`;

        const options = {
            hostname: `${speechRegion}.stt.speech.microsoft.com`,
            path: `/speech/recognition/conversation/cognitiveservices/v1?${queryParams}`,
            method: 'POST',
            timeout: REQUEST_TIMEOUT_MS,
            headers: {
                'Ocp-Apim-Subscription-Key': speechKey,
                'Content-Type': 'audio/wav; codecs=audio/pcm',
                'Content-Length': audioData.length
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
                            error: result.Message || result.error?.message || `Azure API error: ${res.statusCode}`
                        });
                        return;
                    }

                    if (result.RecognitionStatus !== 'Success') {
                        const status = result.RecognitionStatus || 'Unknown';
                        if (status === 'NoMatch') {
                            resolve({
                                success: true,
                                data: {
                                    text: '',
                                    language: azureLanguage,
                                    duration: 0,
                                    success: true
                                }
                            });
                        } else {
                            resolve({ success: false, error: `Recognition failed: ${status}` });
                        }
                        return;
                    }

                    // Extract the best result from NBest array
                    const bestResult = result.NBest && result.NBest.length > 0
                        ? result.NBest[0]
                        : null;

                    const text = bestResult
                        ? bestResult.Display || bestResult.Lexical || ''
                        : result.DisplayText || '';

                    // Duration is in ticks (100-nanosecond units), convert to seconds
                    const durationSeconds = result.Duration
                        ? result.Duration / 10000000
                        : 0;

                    resolve({
                        success: true,
                        data: {
                            text: text,
                            language: azureLanguage,
                            duration: durationSeconds,
                            success: true
                        }
                    });
                } catch {
                    resolve({ success: false, error: 'Failed to parse Azure response' });
                }
            });
        });

        req.on('timeout', () => {
            req.destroy();
            resolve({ success: false, error: 'Azure request timed out' });
        });

        req.on('error', (error) => {
            resolve({ success: false, error: `Azure API error: ${error.message}` });
        });

        req.write(audioData);
        req.end();
    });
}

/**
 * Test Azure Speech Services connection by requesting a token
 * @param {string} speechKey - Azure Speech Services subscription key
 * @param {string} speechRegion - Azure Speech Services region
 * @returns {Promise<{success: boolean, error?: string}>}
 */
async function testAzureConnection(speechKey, speechRegion) {
    return new Promise((resolve) => {
        if (!speechKey) {
            resolve({ success: false, error: 'Speech key is required' });
            return;
        }

        if (!speechRegion) {
            resolve({ success: false, error: 'Speech region is required' });
            return;
        }

        // Validate region format
        if (!/^[a-zA-Z0-9-]+$/.test(speechRegion)) {
            resolve({ success: false, error: 'Invalid region format' });
            return;
        }

        // Test by issuing a token - this validates the key and region
        const options = {
            hostname: `${speechRegion}.api.cognitive.microsoft.com`,
            path: '/sts/v1.0/issueToken',
            method: 'POST',
            timeout: 30000,
            headers: {
                'Ocp-Apim-Subscription-Key': speechKey,
                'Content-Type': 'application/x-www-form-urlencoded',
                'Content-Length': 0
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
        backendPort = BACKEND_PORT,
        openaiApiKey = '',
        openaiModel = 'whisper-1',
        azureSpeechKey = '',
        azureSpeechRegion = ''
    } = options;

    if (provider === 'openai') {
        return transcribeOpenAI(audioPath, language, openaiApiKey, openaiModel);
    } else if (provider === 'azure') {
        return transcribeAzure(audioPath, language, azureSpeechKey, azureSpeechRegion);
    } else {
        return transcribeLocal(audioPath, language, backendPort);
    }
}

module.exports = {
    transcribe,
    transcribeLocal,
    transcribeOpenAI,
    transcribeAzure,
    testOpenAIConnection,
    testAzureConnection
};
