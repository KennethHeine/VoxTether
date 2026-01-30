/**
 * VoxTether Electron - Backend Client
 *
 * HTTP client for communicating with the Python FastAPI backend server.
 */

const http = require('http');
const { BACKEND_URL } = require('../shared/constants.js');

/**
 * Check if backend server is available
 * @returns {Promise<boolean>} True if backend is available
 */
async function checkBackendConnection() {
    return new Promise((resolve) => {
        let resolved = false;

        const req = http.get(`${BACKEND_URL}/api/health`, (res) => {
            resolved = true;
            if (res.statusCode === 200) {
                console.log('Backend server is available');
                resolve(true);
            } else {
                console.warn('Backend server returned non-200 status');
                resolve(false);
            }
        });

        req.on('error', () => {
            if (!resolved) {
                resolved = true;
                console.warn('Backend server not available at', BACKEND_URL);
                resolve(false);
            }
        });

        req.setTimeout(5000, () => {
            if (!resolved) {
                resolved = true;
                req.destroy();
                console.warn('Backend server connection timeout');
                resolve(false);
            }
        });
    });
}

/**
 * Make HTTP request to backend
 * @param {string} method - HTTP method (GET, POST, etc.)
 * @param {string} endpoint - API endpoint path
 * @param {object} body - Request body (optional)
 * @returns {Promise<any>} Response data
 */
function backendRequest(method, endpoint, body = null) {
    return new Promise((resolve, reject) => {
        const url = new URL(endpoint, BACKEND_URL);
        const options = {
            hostname: url.hostname,
            port: url.port,
            path: url.pathname,
            method: method,
            headers: {
                'Content-Type': 'application/json'
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(data));
                } catch {
                    resolve(data);
                }
            });
        });

        req.on('error', reject);
        req.setTimeout(30000, () => {
            req.destroy();
            reject(new Error('Request timeout'));
        });

        if (body) {
            req.write(JSON.stringify(body));
        }
        req.end();
    });
}

module.exports = {
    checkBackendConnection,
    backendRequest
};
