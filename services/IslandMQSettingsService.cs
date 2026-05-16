using System.IO;
using System.Text.Json;
using ClassIsland.Shared;
using IslandMQ.Models;
using Microsoft.Extensions.Logging;

namespace IslandMQ.Services;

/// <summary>
/// IslandMQ插件设置服务，负责加载和保存插件设置。
/// </summary>
public class IslandMQSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly ILogger<IslandMQSettingsService>? _logger;
    private IslandMQSettings _settings = new();

    /// <summary>
    /// 初始化IslandMQSettingsService的新实例。
    /// </summary>
    public IslandMQSettingsService()
    {
        _logger = IAppHost.GetService<ILogger<IslandMQSettingsService>>();
        var configFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClassIsland",
            "plugins",
            "IslandMQ"
        );
        Directory.CreateDirectory(configFolder);
        _settingsPath = Path.Combine(configFolder, "settings.json");
        Load();
    }

    /// <summary>
    /// 获取当前的插件设置。
    /// </summary>
    public IslandMQSettings Settings => _settings;

    /// <summary>
    /// 从磁盘加载设置。
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<IslandMQSettings>(json) ?? new IslandMQSettings();
                _logger?.LogInformation("Settings loaded from {Path}", _settingsPath);
            }
            else
            {
                _settings = new IslandMQSettings();
                _logger?.LogInformation("Using default settings");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load settings, using defaults");
            _settings = new IslandMQSettings();
        }
    }

    /// <summary>
    /// 将当前设置保存到磁盘。
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
            _logger?.LogInformation("Settings saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings");
        }
    }
}
