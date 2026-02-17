# Build script for VoxTether
# Builds the Electron frontend.
# Backend is in a separate repository: https://github.com/KennethHeine/VoxTether-backend

param(
    [switch]$Release,
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
Write-Host "Building Electron frontend (client)" -ForegroundColor Gray
Write-Host "Backend: https://github.com/KennethHeine/VoxTether-backend" -ForegroundColor Gray
Write-Host ""

# Create output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Build Frontend (Electron)
Write-Host "Building Electron Frontend..." -ForegroundColor Yellow

$FrontendDir = "$RootDir\src\frontend-electron"
$FrontendOutput = "$OutputDir"

# Install dependencies
Push-Location $FrontendDir
npm install

# Build with electron-builder
if ($Release) {
    npm run build
} else {
    npm run pack
}
Pop-Location

# Copy build output
if (Test-Path "$FrontendDir\dist\win-unpacked") {
    Copy-Item "$FrontendDir\dist\win-unpacked\*" $FrontendOutput -Recurse
}

if (-not (Test-Path "$FrontendOutput\VoxTether.exe")) {
    Write-Warning "Frontend executable not found at expected location"
    Get-ChildItem -Path "$FrontendDir\dist" -Recurse | Format-Table Name, Length
} else {
    Write-Host "Frontend built successfully!" -ForegroundColor Green
}

# Create release package (ZIP)
if ($Release) {
    Write-Host ""
    Write-Host "Creating release package..." -ForegroundColor Yellow
    
    $PackageName = "VoxTether-Client-$Version-win-x64"
    $PackageDir = "$BuildDir\$PackageName"
    $PackageZip = "$BuildDir\$PackageName.zip"
    
    # Create package directory
    if (Test-Path $PackageDir) {
        Remove-Item $PackageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PackageDir | Out-Null
    
    # Copy frontend
    Copy-Item "$OutputDir\*" $PackageDir -Recurse
    
    # Create README
    @"
VoxTether $Version
==========================

Push-to-talk dictation for Windows.

Getting Started:
1. Ensure the VoxTether backend server is running
   (see https://github.com/KennethHeine/VoxTether-backend)
2. Run VoxTether.exe
3. On first run, configure the backend server address if needed
4. Press Ctrl+Shift+R (default hotkey) to record
5. Release to transcribe and paste the text

Requirements:
- Windows 10/11 (64-bit)
- VoxTether backend server running (localhost or network)

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
if ($CreateInstaller) {
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
