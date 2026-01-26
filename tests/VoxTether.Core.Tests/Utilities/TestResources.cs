namespace VoxTether.Core.Tests.Utilities;

/// <summary>
/// Utility class for accessing test resources and managing temporary files.
/// </summary>
public static class TestResources
{
    /// <summary>
    /// Gets the path to the test fixtures directory.
    /// </summary>
    public static string FixturesPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Gets the path to a specific audio fixture file.
    /// </summary>
    /// <param name="fileName">The name of the audio fixture file.</param>
    /// <returns>Full path to the fixture file.</returns>
    public static string GetAudioFixture(string fileName) =>
        Path.Combine(FixturesPath, "Audio", fileName);

    /// <summary>
    /// Creates a temporary directory for test files.
    /// </summary>
    /// <returns>Path to the created directory.</returns>
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"VoxTetherTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Cleans up a temporary directory created for tests.
    /// </summary>
    /// <param name="path">The path to the directory to clean up.</param>
    public static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    /// <summary>
    /// Creates a minimal valid WAV file for testing.
    /// </summary>
    /// <param name="path">The path where the WAV file will be created.</param>
    public static void CreateDummyWavFile(string path)
    {
        // Create a minimal valid WAV header for testing
        // 44-byte header + 0 bytes of audio data
        byte[] wavHeader = [
            0x52, 0x49, 0x46, 0x46, // "RIFF"
            0x24, 0x00, 0x00, 0x00, // File size - 8
            0x57, 0x41, 0x56, 0x45, // "WAVE"
            0x66, 0x6D, 0x74, 0x20, // "fmt "
            0x10, 0x00, 0x00, 0x00, // Subchunk1Size (16 for PCM)
            0x01, 0x00,             // AudioFormat (1 = PCM)
            0x01, 0x00,             // NumChannels (1 = mono)
            0x80, 0x3E, 0x00, 0x00, // SampleRate (16000 Hz)
            0x00, 0x7D, 0x00, 0x00, // ByteRate
            0x02, 0x00,             // BlockAlign
            0x10, 0x00,             // BitsPerSample (16)
            0x64, 0x61, 0x74, 0x61, // "data"
            0x00, 0x00, 0x00, 0x00  // Subchunk2Size (0 bytes of audio)
        ];

        File.WriteAllBytes(path, wavHeader);
    }
}
