# Copilot Instructions for VoxTether

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It provides fully offline speech-to-text using whisper.cpp. The project is built with C# and .NET 8.0, using WPF for the UI.

**Key characteristics:**
- Windows-only desktop application (WPF)
- Targets `net8.0-windows`
- Uses NAudio for audio recording
- Uses whisper.cpp (external executable) for transcription
- MIT License

## Build and Test Commands

**Always run commands from the repository root directory.**

### Required Commands (in order)

```bash
# 1. Restore dependencies (always run first)
dotnet restore

# 2. Build the solution
dotnet build --configuration Release

# 3. Run tests (requires Windows - see note below)
dotnet test --configuration Release
```

### Important Notes

- **Tests require Windows**: The project targets `net8.0-windows` with WPF/WinForms. Tests will fail on Linux/macOS due to missing `Microsoft.WindowsDesktop.App` runtime. The CI workflow runs on `windows-latest`.
- **No linting**: There are no linting tools configured. Follow standard C# conventions.
- **Build time**: Full build typically takes 10-15 seconds.
- **No additional setup**: Dependencies are managed via NuGet; no npm, pip, or other package managers needed.

### Publishing

```bash
dotnet publish src/VoxTether/VoxTether.csproj -c Release -r win-x64 --self-contained
```

## Project Architecture

```
VoxTether/
├── src/
│   ├── VoxTether/                    # WPF application (entry point)
│   │   ├── App.xaml(.cs)             # Application startup, DI setup
│   │   ├── VoxTetherController.cs    # Main controller logic
│   │   ├── TrayIconManager.cs        # System tray management
│   │   ├── SettingsWindow.xaml(.cs)  # Settings UI
│   │   └── ModelSetupWindow.xaml(.cs)# First-run model setup
│   ├── VoxTether.Core/               # Interfaces and core services
│   │   ├── Interfaces/               # Key abstractions (IAudioRecorder, etc.)
│   │   ├── Models/                   # Data models (VoxTetherSettings, etc.)
│   │   └── Services/                 # Core services (SettingsService, etc.)
│   ├── VoxTether.Infrastructure/     # Platform implementations
│   │   ├── NAudioRecorder.cs         # Audio recording via NAudio
│   │   ├── ClipboardTextInjector.cs  # Text injection via clipboard
│   │   └── LowLevelHookHotkeyService.cs # Global hotkey hook
│   └── VoxTether.Transcription/      # Transcription implementations
│       ├── WhisperCppEngine.cs       # whisper.cpp process wrapper
│       ├── BackendSelectionService.cs# GPU/CPU backend selection
│       └── BackendDownloadService.cs # Backend download management
├── tests/
│   └── VoxTether.Core.Tests/         # Unit tests (xUnit)
├── docs/                             # Additional documentation
├── installer/                        # Inno Setup installer script
├── VoxTether.slnx                    # Solution file
└── README.md
```

## Key Interfaces

New implementations should follow the interface-based architecture:

| Interface | Purpose | Implementation Location |
|-----------|---------|------------------------|
| `IAudioRecorder` | Audio recording to WAV | VoxTether.Infrastructure |
| `ITranscriptionEngine` | Speech-to-text | VoxTether.Transcription |
| `ITextInjector` | Text insertion | VoxTether.Infrastructure |
| `IHotkeyService` | Global hotkey detection | VoxTether.Infrastructure |
| `ITextPostProcessor` | Post-processing hook | VoxTether.Transcription |
| `IBackendDownloadService` | Backend management | VoxTether.Transcription |
| `IBackendSelectionService` | GPU/CPU backend selection | VoxTether.Transcription |
| `IUpdateService` | Update checking | VoxTether.Core.Services |

## CI/CD Pipeline

### Pull Request CI (`.github/workflows/ci.yml`)

Runs on every PR to `main`:
1. Checkout code
2. Setup .NET 8.0
3. `dotnet restore`
4. `dotnet build --no-restore --configuration Release`
5. `dotnet test --no-build --configuration Release`

### Release Workflow (`.github/workflows/release.yml`)

Manually triggered with version input. Builds, tests, creates portable ZIP and installer.

## Code Style Guidelines

- **Naming**: PascalCase for public members, `_camelCase` for private fields
- **Nullability**: Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Interfaces**: Define in VoxTether.Core/Interfaces/, implement in Infrastructure or Transcription
- **Tests**: Use xUnit, place in tests/VoxTether.Core.Tests/

## Configuration Files

| File | Purpose |
|------|---------|
| `VoxTether.slnx` | Solution file (XML format) |
| `*.csproj` | Project files with dependencies |
| `.github/workflows/ci.yml` | CI pipeline |
| `.github/dependabot.yml` | Automated dependency updates |
| `.gitignore` | Standard .NET gitignore |
| `installer/VoxTether.iss` | Inno Setup installer script |

## Dependency Management

Dependencies are declared in `.csproj` files:
- **VoxTether**: Microsoft.Extensions.Logging, Microsoft.Extensions.DependencyInjection
- **VoxTether.Core**: Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Abstractions, System.Text.Json
- **VoxTether.Infrastructure**: NAudio
- **VoxTether.Transcription**: (no external packages, references Core only)
- **Tests**: xUnit, Microsoft.NET.Test.Sdk, coverlet.collector

## Testing

- **Framework**: xUnit
- **Location**: `tests/VoxTether.Core.Tests/`
- **Run tests**: `dotnet test` (Windows only)
- Add tests for new functionality following existing patterns in the test project.

## Troubleshooting

### Build fails with Windows targeting errors
The project requires the Windows SDK. On Linux/macOS, builds will succeed but tests will fail. This is expected - use the CI pipeline for full validation.

### Missing whisper.cpp binary
The whisper.cpp binary is downloaded during release builds, not during development. Tests mock the transcription engine.

## Trust These Instructions

These instructions have been validated against the actual repository. If a command or path mentioned here fails, verify the current state of the repository as it may have changed. Only search the codebase if information here appears outdated or incomplete.
