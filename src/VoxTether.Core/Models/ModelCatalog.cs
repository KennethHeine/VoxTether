namespace VoxTether.Core.Models;

/// <summary>
/// Represents a specific version of a speech-to-text model.
/// </summary>
public class ModelVersion
{
    /// <summary>
    /// Version identifier (e.g., "v3", "v3-turbo").
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// The filename for this model version.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Direct download URL for the model file.
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// Approximate file size in MB.
    /// </summary>
    public int SizeMb { get; init; }

    /// <summary>
    /// Description of this version's characteristics.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Represents a speech-to-text model with multiple versions.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Display name of the model.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Brief description of the model's capabilities.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Quality rating (e.g., "Good", "Better", "Best").
    /// </summary>
    public string Quality { get; init; } = string.Empty;

    /// <summary>
    /// Speed rating (e.g., "Fast", "Moderate", "Slow").
    /// </summary>
    public string Speed { get; init; } = string.Empty;

    /// <summary>
    /// URL for more information about this model.
    /// </summary>
    public string InfoUrl { get; init; } = string.Empty;

    /// <summary>
    /// Available versions of this model.
    /// </summary>
    public List<ModelVersion> Versions { get; init; } = new();
}

/// <summary>
/// Catalog of available speech-to-text models for download.
/// </summary>
public static class ModelCatalog
{
    /// <summary>
    /// Base URL for Hugging Face model downloads.
    /// </summary>
    private const string HuggingFaceBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    /// <summary>
    /// URL for more information about whisper.cpp models.
    /// </summary>
    public const string ModelsInfoUrl = "https://huggingface.co/ggerganov/whisper.cpp";

    /// <summary>
    /// Gets the top 5 recommended speech-to-text models with their versions.
    /// </summary>
    public static List<ModelInfo> GetAvailableModels()
    {
        return new List<ModelInfo>
        {
            new ModelInfo
            {
                Name = "Whisper Tiny",
                Description = "Fastest model with basic accuracy. Good for quick dictation.",
                Quality = "Basic",
                Speed = "Very Fast",
                InfoUrl = "https://huggingface.co/ggerganov/whisper.cpp#model-files",
                Versions = new List<ModelVersion>
                {
                    new ModelVersion
                    {
                        Version = "Standard",
                        FileName = "ggml-tiny.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-tiny.bin",
                        SizeMb = 75,
                        Description = "Standard tiny model"
                    },
                    new ModelVersion
                    {
                        Version = "English Only",
                        FileName = "ggml-tiny.en.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-tiny.en.bin",
                        SizeMb = 75,
                        Description = "English-only, slightly better for English"
                    },
                    new ModelVersion
                    {
                        Version = "Quantized (Q5)",
                        FileName = "ggml-tiny-q5_1.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-tiny-q5_1.bin",
                        SizeMb = 32,
                        Description = "Quantized version, smaller file size"
                    }
                }
            },
            new ModelInfo
            {
                Name = "Whisper Base",
                Description = "Good balance of speed and accuracy. Recommended for most users.",
                Quality = "Good",
                Speed = "Fast",
                InfoUrl = "https://huggingface.co/ggerganov/whisper.cpp#model-files",
                Versions = new List<ModelVersion>
                {
                    new ModelVersion
                    {
                        Version = "Standard",
                        FileName = "ggml-base.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-base.bin",
                        SizeMb = 142,
                        Description = "Standard base model (default)"
                    },
                    new ModelVersion
                    {
                        Version = "English Only",
                        FileName = "ggml-base.en.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-base.en.bin",
                        SizeMb = 142,
                        Description = "English-only, slightly better for English"
                    },
                    new ModelVersion
                    {
                        Version = "Quantized (Q5)",
                        FileName = "ggml-base-q5_1.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-base-q5_1.bin",
                        SizeMb = 57,
                        Description = "Quantized version, smaller file size"
                    }
                }
            },
            new ModelInfo
            {
                Name = "Whisper Small",
                Description = "Better accuracy with moderate speed. Great for longer dictation.",
                Quality = "Better",
                Speed = "Moderate",
                InfoUrl = "https://huggingface.co/ggerganov/whisper.cpp#model-files",
                Versions = new List<ModelVersion>
                {
                    new ModelVersion
                    {
                        Version = "Standard",
                        FileName = "ggml-small.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-small.bin",
                        SizeMb = 466,
                        Description = "Standard small model"
                    },
                    new ModelVersion
                    {
                        Version = "English Only",
                        FileName = "ggml-small.en.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-small.en.bin",
                        SizeMb = 466,
                        Description = "English-only, slightly better for English"
                    },
                    new ModelVersion
                    {
                        Version = "Quantized (Q5)",
                        FileName = "ggml-small-q5_1.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-small-q5_1.bin",
                        SizeMb = 181,
                        Description = "Quantized version, smaller file size"
                    }
                }
            },
            new ModelInfo
            {
                Name = "Whisper Medium",
                Description = "High accuracy for professional use. Slower but very reliable.",
                Quality = "Great",
                Speed = "Slow",
                InfoUrl = "https://huggingface.co/ggerganov/whisper.cpp#model-files",
                Versions = new List<ModelVersion>
                {
                    new ModelVersion
                    {
                        Version = "Standard",
                        FileName = "ggml-medium.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-medium.bin",
                        SizeMb = 1500,
                        Description = "Standard medium model"
                    },
                    new ModelVersion
                    {
                        Version = "English Only",
                        FileName = "ggml-medium.en.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-medium.en.bin",
                        SizeMb = 1500,
                        Description = "English-only, slightly better for English"
                    },
                    new ModelVersion
                    {
                        Version = "Quantized (Q5)",
                        FileName = "ggml-medium-q5_0.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-medium-q5_0.bin",
                        SizeMb = 514,
                        Description = "Quantized version, smaller file size"
                    }
                }
            },
            new ModelInfo
            {
                Name = "Whisper Large",
                Description = "Best accuracy available. Recommended for accuracy-critical work.",
                Quality = "Best",
                Speed = "Very Slow",
                InfoUrl = "https://huggingface.co/ggerganov/whisper.cpp#model-files",
                Versions = new List<ModelVersion>
                {
                    new ModelVersion
                    {
                        Version = "v3",
                        FileName = "ggml-large-v3.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3.bin",
                        SizeMb = 3100,
                        Description = "Latest large model, best accuracy"
                    },
                    new ModelVersion
                    {
                        Version = "v3-turbo",
                        FileName = "ggml-large-v3-turbo.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3-turbo.bin",
                        SizeMb = 1600,
                        Description = "Faster variant of v3, good balance"
                    },
                    new ModelVersion
                    {
                        Version = "v3-turbo (Q5)",
                        FileName = "ggml-large-v3-turbo-q5_0.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3-turbo-q5_0.bin",
                        SizeMb = 547,
                        Description = "Quantized turbo, smaller file size"
                    },
                    new ModelVersion
                    {
                        Version = "v2",
                        FileName = "ggml-large-v2.bin",
                        DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v2.bin",
                        SizeMb = 3100,
                        Description = "Previous large version"
                    }
                }
            }
        };
    }
}
