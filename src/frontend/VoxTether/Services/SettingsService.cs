using System.Text.Json;
using VoxTether.Core.Models;

namespace VoxTether.Services;

/// <summary>
/// Service for managing application settings.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private VoxTetherSettings _settings;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, "VoxTether");
        
        Directory.CreateDirectory(settingsDir);
        
        _settingsPath = Path.Combine(settingsDir, "settings.json");
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
    /// Gets the path to the models directory.
    /// </summary>
    public static string ModelsPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "VoxTether", "models");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// Gets the path to the logs directory.
    /// </summary>
    public static string LogsPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "VoxTether", "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private VoxTetherSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<VoxTetherSettings>(json, JsonOptions) 
                       ?? new VoxTetherSettings();
            }
        }
        catch (Exception)
        {
            // Return default settings if loading fails
        }

        return new VoxTetherSettings();
    }

    /// <summary>
    /// Saves the current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception)
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Reloads settings from disk.
    /// </summary>
    public void Reload()
    {
        _settings = Load();
    }
}
