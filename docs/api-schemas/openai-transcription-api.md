# OpenAI Transcription API Schema

This document describes the OpenAI Whisper API schema used for audio transcription in VoxTether.

## Endpoint

```
POST https://api.openai.com/v1/audio/transcriptions
```

## Authentication

All requests require an `Authorization` header with a valid API key:

```
Authorization: Bearer sk-your-api-key-here
```

## Request Schema

### Content-Type

```
multipart/form-data
```

### Required Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `file` | File | The audio file to transcribe. Supported formats: `mp3`, `mp4`, `mpeg`, `mpga`, `m4a`, `wav`, `webm` |
| `model` | String | The model to use for transcription. One of: `whisper-1`, `gpt-4o-transcribe`, `gpt-4o-mini-transcribe` |

### Optional Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `language` | String | auto-detect | ISO-639-1 language code (e.g., `en`, `da`, `es`). If not provided, language is auto-detected. |
| `prompt` | String | - | An optional prompt to guide the model's style or continue a previous audio segment. Should match the audio language. |
| `response_format` | String | `json` | The format of the response. One of: `json`, `text`, `srt`, `verbose_json`, `vtt` |
| `temperature` | Number | 0 | Sampling temperature between 0 and 1. Higher values make output more random. |

## Response Schemas

### JSON Response (response_format: "json")

```json
{
  "text": "The transcribed text content..."
}
```

**TypeScript Interface:**

```typescript
interface TranscriptionResponse {
  text: string;
}
```

### Verbose JSON Response (response_format: "verbose_json")

```json
{
  "task": "transcribe",
  "language": "english",
  "duration": 8.47,
  "text": "The transcribed text content...",
  "segments": [
    {
      "id": 0,
      "seek": 0,
      "start": 0.0,
      "end": 2.5,
      "text": " Segment text...",
      "tokens": [50364, 440, 1892, 1853, 13],
      "temperature": 0.0,
      "avg_logprob": -0.25,
      "compression_ratio": 1.2,
      "no_speech_prob": 0.01
    }
  ]
}
```

**TypeScript Interface:**

```typescript
interface VerboseTranscriptionResponse {
  task: "transcribe";
  language: string;
  duration: number;
  text: string;
  segments: TranscriptionSegment[];
}

interface TranscriptionSegment {
  id: number;
  seek: number;
  start: number;
  end: number;
  text: string;
  tokens: number[];
  temperature: number;
  avg_logprob: number;
  compression_ratio: number;
  no_speech_prob: number;
}
```

### Text Response (response_format: "text")

Returns plain text without JSON wrapper:

```
The transcribed text content...
```

### SRT Response (response_format: "srt")

Returns subtitle file format:

```
1
00:00:00,000 --> 00:00:02,500
Segment text...

2
00:00:02,500 --> 00:00:05,000
More segment text...
```

### VTT Response (response_format: "vtt")

Returns WebVTT subtitle format:

```
WEBVTT

00:00:00.000 --> 00:00:02.500
Segment text...

00:00:02.500 --> 00:00:05.000
More segment text...
```

## Error Response Schema

```json
{
  "error": {
    "message": "Description of the error",
    "type": "invalid_request_error",
    "param": "file",
    "code": "invalid_file_format"
  }
}
```

**TypeScript Interface:**

```typescript
interface ErrorResponse {
  error: {
    message: string;
    type: string;
    param?: string;
    code?: string;
  };
}
```

### Common Error Types

| Type | Description |
|------|-------------|
| `invalid_request_error` | Invalid parameters or file format |
| `authentication_error` | Invalid or missing API key |
| `rate_limit_error` | Too many requests |
| `invalid_api_key` | API key is not valid |
| `insufficient_quota` | Account has insufficient credits |

## Available Models

| Model | Price | Description | Best For |
|-------|-------|-------------|----------|
| `whisper-1` | $0.006/min | Original Whisper model (Jan 2023 snapshot) | General transcription, cost-effective |
| `gpt-4o-transcribe` | $0.006/min | Higher quality transcription | High accuracy requirements |
| `gpt-4o-mini-transcribe` | $0.003/min | Faster, lightweight model | Quick transcriptions |

## Limitations

- **Maximum file size:** 25 MB
- **Supported formats:** mp3, mp4, mpeg, mpga, m4a, wav, webm
- **Requires internet connection**
- **Rate limits:** Varies by account tier

## Example Request (JavaScript/Node.js)

```javascript
const fs = require('fs');
const https = require('https');

async function transcribe(audioPath, apiKey, language = 'auto') {
    return new Promise((resolve, reject) => {
        const audioData = fs.readFileSync(audioPath);
        const boundary = `----WebKitFormBoundary${Date.now().toString(16)}`;
        
        // Build multipart form data
        let body = '';
        body += `--${boundary}\r\n`;
        body += `Content-Disposition: form-data; name="file"; filename="audio.wav"\r\n`;
        body += 'Content-Type: audio/wav\r\n\r\n';
        
        let bodyEnd = `\r\n--${boundary}\r\n`;
        bodyEnd += `Content-Disposition: form-data; name="model"\r\n\r\nwhisper-1\r\n`;
        
        if (language !== 'auto') {
            bodyEnd += `--${boundary}\r\n`;
            bodyEnd += `Content-Disposition: form-data; name="language"\r\n\r\n${language}\r\n`;
        }
        
        bodyEnd += `--${boundary}\r\n`;
        bodyEnd += `Content-Disposition: form-data; name="response_format"\r\n\r\nverbose_json\r\n`;
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
                if (res.statusCode === 200) {
                    resolve(JSON.parse(data));
                } else {
                    reject(new Error(data));
                }
            });
        });
        
        req.on('error', reject);
        req.write(bodyBuffer);
        req.end();
    });
}
```

## VoxTether Integration

VoxTether uses the `verbose_json` response format to extract:

- `text` - The full transcription text
- `language` - Detected or specified language
- `duration` - Audio duration in seconds

The response is normalized to match the local backend format:

```javascript
{
  success: true,
  data: {
    text: "Transcribed text...",
    language: "english",
    duration: 8.47,
    success: true
  }
}
```

## References

- [OpenAI Audio API Documentation](https://platform.openai.com/docs/api-reference/audio/createTranscription)
- [OpenAI API Pricing](https://openai.com/api/pricing/)
- [Whisper Model Information](https://platform.openai.com/docs/models/whisper)
