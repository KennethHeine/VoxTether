namespace VoxTether.Core.Services;

/// <summary>
/// Utility methods for formatting values.
/// </summary>
public static class FormatUtility
{
    /// <summary>
    /// Formats a byte count into a human-readable string.
    /// </summary>
    /// <param name="bytes">Number of bytes.</param>
    /// <returns>Formatted string (e.g., "1.5 MB").</returns>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
