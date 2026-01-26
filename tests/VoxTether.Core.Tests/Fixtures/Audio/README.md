# Audio Test Fixtures

This directory contains audio files for integration testing.

## Available Fixtures

- `silence-1s.wav` - 1 second of silence for basic testing (auto-generated in tests)

## Adding New Fixtures

To add new audio fixtures:
1. Add the `.wav` file to this directory
2. Reference it using `TestResources.GetAudioFixture("filename.wav")`

Note: Keep audio files small to avoid bloating the repository.
