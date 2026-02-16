import js from "@eslint/js";

// Shared rules for all files
const sharedRules = {
    "no-unused-vars": ["warn", { "argsIgnorePattern": "^_", "varsIgnorePattern": "^_", "caughtErrorsIgnorePattern": "^_" }],
    "no-console": "off",
    "semi": ["error", "always"],
    "quotes": ["warn", "single", { "avoidEscape": true }],
    "indent": ["warn", 4],
    "no-trailing-spaces": "warn",
    "eol-last": "warn"
};

// Shared browser globals
const browserGlobals = {
    console: "readonly",
    setTimeout: "readonly",
    setInterval: "readonly",
    clearTimeout: "readonly",
    clearInterval: "readonly",
    Promise: "readonly",
    URL: "readonly",
    FormData: "readonly",
    confirm: "readonly",
    alert: "readonly",
    document: "readonly",
    window: "readonly",
    navigator: "readonly",
    requestAnimationFrame: "readonly",
    cancelAnimationFrame: "readonly",
    AudioContext: "readonly",
    webkitAudioContext: "readonly",
    MediaRecorder: "readonly",
    Blob: "readonly",
    FileReader: "readonly",
    btoa: "readonly",
    localStorage: "readonly"
};

export default [
    js.configs.recommended,
    // CommonJS files (main process and preload)
    {
        files: ["src/main/**/*.js", "src/preload.js"],
        languageOptions: {
            ecmaVersion: 2022,
            sourceType: "commonjs",
            globals: {
                require: "readonly",
                module: "readonly",
                process: "readonly",
                __dirname: "readonly",
                Buffer: "readonly",
                ...browserGlobals
            }
        },
        rules: sharedRules
    },
    // ES Modules (renderer modules and shared constants)
    {
        files: ["src/renderer/**/*.js", "src/shared/**/*.js"],
        languageOptions: {
            ecmaVersion: 2022,
            sourceType: "module",
            globals: {
                process: "readonly",
                ...browserGlobals
            }
        },
        rules: sharedRules
    }
];
