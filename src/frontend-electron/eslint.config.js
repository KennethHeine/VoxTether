import js from "@eslint/js";

export default [
    js.configs.recommended,
    {
        files: ["src/**/*.js"],
        languageOptions: {
            ecmaVersion: 2022,
            sourceType: "commonjs",
            globals: {
                require: "readonly",
                module: "readonly",
                process: "readonly",
                __dirname: "readonly",
                console: "readonly",
                Buffer: "readonly",
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
                btoa: "readonly"
            }
        },
        rules: {
            "no-unused-vars": ["warn", { "argsIgnorePattern": "^_", "varsIgnorePattern": "^_", "caughtErrorsIgnorePattern": "^_" }],
            "no-console": "off",
            "semi": ["error", "always"],
            "quotes": ["warn", "single", { "avoidEscape": true }],
            "indent": ["warn", 4],
            "no-trailing-spaces": "warn",
            "eol-last": "warn"
        }
    }
];
