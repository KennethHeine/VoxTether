# Build script for VoxTether
# Builds both frontend (WinUI 3) and backend (Python)

param(
    [switch]$Release,
    [switch]$FrontendOnly,
    [switch]$BackendOnly,
    [switch]$CreateInstaller,
    [string]$Version = "2.0.0"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$BuildDir = "$RootDir\build"
$OutputDir = "$BuildDir\output"
$InstallerDir = "$BuildDir\installer"

Write-Host "VoxTether Build Script" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Gray
Write-Host ""

# Create output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Build Backend
if (-not $FrontendOnly) {
    Write-Host "Building Python Backend..." -ForegroundColor Yellow
    
    $BackendDir = "$RootDir\src\backend"
    $BackendOutput = "$OutputDir\backend"
    
    # Create virtual environment if needed
    if (-not (Test-Path "$BackendDir\venv")) {
        Write-Host "Creating virtual environment..."
        python -m venv "$BackendDir\venv"
    }
    
    # Activate and install dependencies
    & "$BackendDir\venv\Scripts\pip.exe" install -r "$BackendDir\requirements.txt" -q
    & "$BackendDir\venv\Scripts\pip.exe" install pyinstaller -q
    
    # Build with PyInstaller
    Write-Host "Building with PyInstaller..."
    Push-Location $BackendDir
    & "$BackendDir\venv\Scripts\pyinstaller.exe" `
        --onefile `
        --name "vox-backend" `
        --distpath $BackendOutput `
        --workpath "$BuildDir\pyinstaller-work" `
        --specpath "$BuildDir\pyinstaller-spec" `
        --noconfirm `
        main.py
    Pop-Location
    
    if (-not (Test-Path "$BackendOutput\vox-backend.exe")) {
        Write-Error "Backend build failed!"
        exit 1
    }
    
    Write-Host "Backend built successfully!" -ForegroundColor Green
}

# Build Frontend
if (-not $BackendOnly) {
    Write-Host ""
    Write-Host "Building WinUI 3 Frontend..." -ForegroundColor Yellow
    
    $FrontendDir = "$RootDir\src\frontend"
    $FrontendOutput = "$OutputDir"
    
    $Configuration = if ($Release) { "Release" } else { "Debug" }
    
    # Restore and build
    dotnet restore "$FrontendDir\VoxTether.sln"
    dotnet publish "$FrontendDir\VoxTether\VoxTether.csproj" `
        -c $Configuration `
        -r win-x64 `
        --self-contained `
        -p:PublishSingleFile=false `
        -p:Version=$Version `
        -o $FrontendOutput
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Frontend build failed!"
        exit 1
    }
    
    Write-Host "Frontend built successfully!" -ForegroundColor Green
}

# Create release package (ZIP)
if ($Release -and -not $FrontendOnly -and -not $BackendOnly) {
    Write-Host ""
    Write-Host "Creating release package..." -ForegroundColor Yellow
    
    $PackageName = "VoxTether-$Version-win-x64"
    $PackageDir = "$BuildDir\$PackageName"
    $PackageZip = "$BuildDir\$PackageName.zip"
    
    # Create package directory
    if (Test-Path $PackageDir) {
        Remove-Item $PackageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PackageDir | Out-Null
    
    # Copy frontend
    Copy-Item "$OutputDir\*" $PackageDir -Recurse -Exclude "backend"
    
    # Copy backend
    New-Item -ItemType Directory -Path "$PackageDir\backend" | Out-Null
    Copy-Item "$OutputDir\backend\vox-backend.exe" "$PackageDir\backend\"
    
    # Create README
    @"
VoxTether $Version
==================

Push-to-talk dictation for Windows. Fully offline speech-to-text.

Getting Started:
1. Run VoxTether.exe
2. On first run, you'll be prompted to download a speech recognition model
3. Press Ctrl+Shift+Space (default hotkey) to record
4. Release to transcribe and paste the text

Requirements:
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (bundled)
- For GPU acceleration: NVIDIA GPU with CUDA support

For more information, visit:
https://github.com/KennethHeine/VoxTether

License: MIT
"@ | Out-File "$PackageDir\README.txt" -Encoding UTF8
    
    # Copy license
    if (Test-Path "$RootDir\LICENSE") {
        Copy-Item "$RootDir\LICENSE" "$PackageDir\LICENSE.txt"
    }
    
    # Create ZIP
    if (Test-Path $PackageZip) {
        Remove-Item $PackageZip
    }
    Compress-Archive -Path "$PackageDir\*" -DestinationPath $PackageZip
    
    Write-Host "Release package created: $PackageZip" -ForegroundColor Green
}

# Create installer
if ($CreateInstaller -and -not $FrontendOnly -and -not $BackendOnly) {
    Write-Host ""
    Write-Host "Creating Windows installer..." -ForegroundColor Yellow
    
    # Check if Inno Setup is installed
    $InnoSetupPath = ""
    $PossiblePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    
    foreach ($Path in $PossiblePaths) {
        if (Test-Path $Path) {
            $InnoSetupPath = $Path
            break
        }
    }
    
    if (-not $InnoSetupPath) {
        Write-Warning "Inno Setup not found. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php"
        Write-Warning "Skipping installer creation."
    } else {
        # Create installer output directory
        if (-not (Test-Path $InstallerDir)) {
            New-Item -ItemType Directory -Path $InstallerDir | Out-Null
        }
        
        # Set version environment variable for Inno Setup
        $env:VOXTETHER_VERSION = $Version
        
        # Run Inno Setup
        $IssFile = "$RootDir\installer\VoxTether.iss"
        & $InnoSetupPath $IssFile
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Installer creation failed!"
            exit 1
        }
        
        Write-Host "Installer created: $InstallerDir\VoxTether-$Version-Setup.exe" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OutputDir" -ForegroundColor Gray
if ($CreateInstaller) {
    Write-Host "Installer: $InstallerDir" -ForegroundColor Gray
}
