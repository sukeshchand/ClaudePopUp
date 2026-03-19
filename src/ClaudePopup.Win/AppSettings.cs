using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudePopup;

class AppSettingsData
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Ocean Blue";

    [JsonPropertyName("historyEnabled")]
    public bool HistoryEnabled { get; set; } = false;
}

static class AppSettings
{
    private static readonly string _dataDir = Path.Combine(
        Path.GetDirectoryName(Application.ExecutablePath)!, "_data");

    private static readonly string _settingsPath = Path.Combine(_dataDir, "settings.json");

    private static AppSettingsData? _cached;

    public static AppSettingsData Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                _cached = JsonSerializer.Deserialize<AppSettingsData>(json) ?? new AppSettingsData();
                return _cached;
            }
        }
        catch { }

        _cached = new AppSettingsData();
        return _cached;
    }

    public static void Save(AppSettingsData settings)
    {
        _cached = settings;
        try
        {
            Directory.CreateDirectory(_dataDir);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    public static string DataDir => _dataDir;
}
