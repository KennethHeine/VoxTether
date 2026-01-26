# AGENTS.md

This file provides context and instructions to help AI coding agents work effectively on the VoxTether project.

## Project Overview

VoxTether is a push-to-talk dictation application for Windows 10/11. It is fully offline, using whisper.cpp for local speech-to-text transcription. The project is built with C# and .NET 8.0.

## Setup Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build --configuration Release

# Run tests
dotnet test

# Publish for Windows x64
dotnet publish src/VoxTether/VoxTether.csproj -c Release -r win-x64 --self-contained
```

## Architecture

```
src/
├── VoxTether/                 # WPF application (main entry point)
├── VoxTether.Core/            # Interfaces and core services
├── VoxTether.Infrastructure/  # NAudio recorder, hotkey hook, text injector
└── VoxTether.Transcription/   # whisper.cpp engine wrapper

tests/
└── VoxTether.Core.Tests/      # Unit tests
```

## Key Interfaces

- `IAudioRecorder` - Audio recording to WAV
- `ITranscriptionEngine` - Speech-to-text transcription
- `ITextInjector` - Text insertion into focused applications
- `IHotkeyService` - Global hotkey detection
- `ITextPostProcessor` - Post-processing hook (currently no-op, V2 extension point for LLM support)

## Code Style

- C# with .NET 8.0
- Use interfaces for abstractions (located in VoxTether.Core)
- Implementations go in VoxTether.Infrastructure or VoxTether.Transcription
- Follow standard C# naming conventions (PascalCase for public members, `_camelCase` for private fields)

## Testing

- Unit tests are located in `tests/VoxTether.Core.Tests/`
- Run tests with: `dotnet test`
- Tests use xUnit framework
- Add tests for any new functionality

## CI/CD

- CI workflow is defined in `.github/workflows/ci.yml`
- Runs on pull requests to main branch
- Builds with Release configuration
- Runs all tests

## Platform

- Windows only (WPF application)
- Targets .NET 8.0 Windows
- Uses NAudio for audio recording
- Uses whisper.cpp for transcription
