# VoxTether Quick Commands

This document provides quick one-liner commands for developers.

## Start Backend Locally

```powershell
cd src/backend; if (Test-Path "venv\Scripts\Activate.ps1") { .\venv\Scripts\Activate.ps1 } else { python -m venv venv; .\venv\Scripts\Activate.ps1; pip install -r requirements.txt }; python -m uvicorn main:app --host 127.0.0.1 --port 5678 --reload
```

## Start Frontend Locally

```powershell
cd src/frontend-electron; npm install; npm start
```

## Tests

```bash
python -m tests.download_all_models
python -m tests.test_backend_api
```
