using VoxTether.Core.Models;
using System.Text.Json;

namespace VoxTether.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void VoxTetherSettings_DefaultValues_AreCorrect()
    {
        var settings = new VoxTetherSettings();
        
        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey);
        Assert.Equal("auto", settings.Language);
        Assert.False(settings.StartWithWindows);
        Assert.True(settings.ShowNotifications);
        Assert.True(settings.ShowRecordingIndicator);
        Assert.True(settings.CopyToClipboard);
        Assert.True(settings.FallbackToTyping);
        Assert.Equal(100, settings.ClipboardDelayMs);
    }

    [Fact]
    public void VoxTetherSettings_CanSerializeToJson()
    {
        var settings = new VoxTetherSettings
        {
            Hotkey = "Ctrl + Shift + R",
            Language = "en",
            StartWithWindows = true
        };

        var json = JsonSerializer.Serialize(settings);
        
        Assert.Contains("Hotkey", json);
        Assert.Contains("Ctrl + Shift + R", json);
    }

    [Fact]
    public void VoxTetherSettings_CanDeserializeFromJson()
    {
        var json = """
        {
            "hotkey": "Alt + Space",
            "language": "fr",
            "startWithWindows": true,
            "showNotifications": false
        }
        """;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var settings = JsonSerializer.Deserialize<VoxTetherSettings>(json, options);
        
        Assert.NotNull(settings);
        Assert.Equal("Alt + Space", settings.Hotkey);
        Assert.Equal("fr", settings.Language);
        Assert.True(settings.StartWithWindows);
        Assert.False(settings.ShowNotifications);
    }

    [Fact]
    public void VoxTetherSettings_NullModelPath_UsesDefault()
    {
        var settings = new VoxTetherSettings();
        
        Assert.Null(settings.ModelPath);
        Assert.Null(settings.ModelName);
    }

    [Fact]
    public void SettingsService_AppDataPath_ReturnsValidPath()
    {
        var path = SettingsService.AppDataPath;
        
        Assert.NotNull(path);
        Assert.Contains("VoxTether", path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SettingsService_UserModelsPath_ReturnsValidPath()
    {
        var path = SettingsService.UserModelsPath;
        
        Assert.NotNull(path);
        Assert.Contains("models", path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SettingsService_LogsPath_ReturnsValidPath()
    {
        var path = SettingsService.LogsPath;
        
        Assert.NotNull(path);
        Assert.Contains("logs", path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SettingsService_TempPath_ReturnsValidPath()
    {
        var path = SettingsService.TempPath;
        
        Assert.NotNull(path);
        Assert.Contains("temp", path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SettingsService_Load_ReturnsDefaultsIfFileNotExists()
    {
        var service = new SettingsService();
        var settings = service.Settings;
        
        Assert.NotNull(settings);
        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey);
    }
}
