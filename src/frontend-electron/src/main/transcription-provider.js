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

/**
 * Transcribe using local backend
 * @param {string} audioPath - Path to audio file
 * @param {string} language - Language code (e.g., 'en', 'auto')
 * @param {number} backendPort - Local backend port
 * @returns {Promise<{success: boolean, data?: object, error?: string}>}
 */
async function transcribeLocal(audioPath, language, backendPort) {
    return new Promise((resolve) => {
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;
        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);

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

        const audioData = fs.readFileSync(audioPath);
        const audioFileName = path.basename(audioPath);
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;

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

                    resolve({
                        success: true,
                        data: {
                            text: result.text,
                            language: result.language,
                            duration: result.duration,
                            success: true
                        }
                    });
                } catch {
                    resolve({ success: false, error: 'Failed to parse OpenAI response' });
                }
            });
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
