using System.Collections.Generic;

namespace VoxTether.Core.Models;

/// <summary>
/// Represents the manifest of available backend packages for download.
/// </summary>
public class BackendManifest
{
    /// <summary>
    /// Version of the manifest format.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// List of available backend packages.
    /// </summary>
    public List<BackendPackageInfo> Backends { get; set; } = [];
}

/// <summary>
/// Information about a downloadable backend package.
/// </summary>
public class BackendPackageInfo
{
    /// <summary>
    /// Backend identifier (cuda).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the backend.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the backend provides.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Download URL for the backend package (zip file).
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Expected size of the download in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Checksum for integrity validation (format: "sha256:hash").
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// System requirements description.
    /// </summary>
    public string Requirements { get; set; } = string.Empty;

    /// <summary>
    /// Gets the TranscriptionBackendMode enum value from the Id.
    /// </summary>
    public TranscriptionBackendMode GetBackendMode()
    {
        return Id.ToLowerInvariant() switch
        {
            "cuda" => TranscriptionBackendMode.Cuda,
            _ => TranscriptionBackendMode.CpuOnly
        };
    }
}
