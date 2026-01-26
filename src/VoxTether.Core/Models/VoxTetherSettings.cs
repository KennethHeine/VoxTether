using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoxTether.Core.Models;

/// <summary>
/// Application settings that are persisted to disk.
/// </summary>
public class VoxTetherSettings
{
    /// <summary>
    /// The hotkey combination string (e.g., "Ctrl + Alt + Space").
    /// </summary>
    public string Hotkey { get; set; } = "Ctrl + Alt + Space";

    /// <summary>
    /// Full path to the selected model file, or null to use default.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Name of the selected model (without path).
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Language code for transcription ("auto" for auto-detection).
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Whether to start the application with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether to show notifications.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Whether to show a recording indicator while recording.
    /// </summary>
    public bool ShowRecordingIndicator { get; set; } = true;

    /// <summary>
    /// Whether to copy text to clipboard after transcription.
    /// </summary>
    public bool CopyToClipboard { get; set; } = true;

    /// <summary>
    /// Whether to type the text if clipboard fails.
    /// </summary>
    public bool FallbackToTyping { get; set; } = true;

    /// <summary>
    /// The delay in milliseconds between clipboard operations.
    /// </summary>
    public int ClipboardDelayMs { get; set; } = 100;

    /// <summary>
    /// The toggle hotkey combination string (e.g., "Ctrl + Alt + T").
    /// Press once to start recording, press again to stop.
    /// </summary>
    public string ToggleHotkey { get; set; } = "Ctrl + Alt + T";

    /// <summary>
    /// Whether to save audio recordings to a default folder.
    /// </summary>
    public bool SaveAudioRecordings { get; set; } = false;

    /// <summary>
    /// The path where audio recordings are saved.
    /// If null or empty, uses the default AudioRecordingsPath.
    /// </summary>
    public string? AudioSavePath { get; set; }
}

/// <summary>
/// Service for loading and saving settings.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _settingsPath;
    private VoxTetherSettings _settings;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var voxTetherPath = Path.Combine(appDataPath, "VoxTether");
        Directory.CreateDirectory(voxTetherPath);
        _settingsPath = Path.Combine(voxTetherPath, "settings.json");
        _settings = Load();
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    public VoxTetherSettings Settings => _settings;

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    public string SettingsPath => _settingsPath;

    /// <summary>
    /// Gets the application data folder path.
    /// </summary>
    public static string AppDataPath
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoxTether");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the user models folder path.
    /// </summary>
    public static string UserModelsPath
    {
        get
        {
            var path = Path.Combine(AppDataPath, "models");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the logs folder path.
    /// </summary>
    public static string LogsPath
    {
        get
        {
            var path = Path.Combine(AppDataPath, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the temp folder path.
    /// </summary>
    public static string TempPath
    {
        get
        {
            var path = Path.Combine(AppDataPath, "temp");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the default audio recordings folder path.
    /// </summary>
    public static string AudioRecordingsPath
    {
        get
        {
            var path = Path.Combine(AppDataPath, "recordings");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the installed models folder path (in Program Files).
    /// </summary>
    public static string InstalledModelsPath
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "models");
        }
    }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    public VoxTetherSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<VoxTetherSettings>(json, JsonOptions) 
                    ?? new VoxTetherSettings();
            }
            else
            {
                _settings = new VoxTetherSettings();
            }
        }
        catch
        {
            _settings = new VoxTetherSettings();
        }
        
        return _settings;
    }

    /// <summary>
    /// Saves the current settings to disk.
    /// </summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>
    /// Updates settings and saves to disk.
    /// </summary>
    public void Update(Action<VoxTetherSettings> updateAction)
    {
        updateAction(_settings);
        Save();
    }

    /// <summary>
    /// Gets the effective model path, checking user folder first, then installed folder.
    /// </summary>
    public string? GetEffectiveModelPath()
    {
        // If a specific path is set and exists, use it
        if (!string.IsNullOrEmpty(_settings.ModelPath) && File.Exists(_settings.ModelPath))
        {
            return _settings.ModelPath;
        }

        // If a model name is set, look for it
        if (!string.IsNullOrEmpty(_settings.ModelName))
        {
            // Check user folder first
            var userPath = Path.Combine(UserModelsPath, _settings.ModelName);
            if (File.Exists(userPath))
            {
                return userPath;
            }

            // Check installed folder
            var installedPath = Path.Combine(InstalledModelsPath, _settings.ModelName);
            if (File.Exists(installedPath))
            {
                return installedPath;
            }
        }

        // Look for any model in user folder
        if (Directory.Exists(UserModelsPath))
        {
            var userModels = Directory.GetFiles(UserModelsPath, "*.bin");
            if (userModels.Length > 0)
            {
                return userModels[0];
            }
        }

        // Look for any model in installed folder
        if (Directory.Exists(InstalledModelsPath))
        {
            var installedModels = Directory.GetFiles(InstalledModelsPath, "*.bin");
            if (installedModels.Length > 0)
            {
                return installedModels[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all available models from both user and installed folders.
    /// </summary>
    public List<string> GetAvailableModels()
    {
        var models = new List<string>();

        if (Directory.Exists(UserModelsPath))
        {
            models.AddRange(Directory.GetFiles(UserModelsPath, "*.bin"));
        }

        if (Directory.Exists(InstalledModelsPath))
        {
            models.AddRange(Directory.GetFiles(InstalledModelsPath, "*.bin"));
        }

        return models;
    }

    /// <summary>
    /// Checks if any speech recognition model is available.
    /// </summary>
    public static bool HasAnyModel()
    {
        // Check user models folder first (this persists across updates)
        if (Directory.Exists(UserModelsPath))
        {
            var userModels = Directory.GetFiles(UserModelsPath, "*.bin");
            if (userModels.Length > 0)
            {
                return true;
            }
        }

        // Check installed models folder (bundled with app, if any)
        if (Directory.Exists(InstalledModelsPath))
        {
            var installedModels = Directory.GetFiles(InstalledModelsPath, "*.bin");
            if (installedModels.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
