### one line command to start backend localy

```powershell
cd c:\Users\KennethSølberg\code\VoxTether\src\backend; if (Test-Path "venv\Scripts\Activate.ps1") { .\venv\Scripts\Activate.ps1 } else { python -m venv venv; .\venv\Scripts\Activate.ps1; pip install -r requirements.txt }; python -m uvicorn main:app --host 127.0.0.1 --port 5678 --reload
```

### one line command to start frontend localy
```powershell
cd c:\Users\KennethSølberg\code\VoxTether\src\frontend-electron; npm install; npm start
```

## test
python -m tests.download_all_models

python -m tests.test_backend_api