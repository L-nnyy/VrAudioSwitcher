using System.Text.Json;

namespace VrAudioSwitcher.Profiles;

/// <summary>
/// Loads/saves <see cref="AppConfig"/> as JSON under
/// %APPDATA%\VrAudioSwitcher\config.json, and resolves which profile to apply
/// when SteamVR starts.
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string ConfigPath { get; }
    public AppConfig Config { get; private set; } = new();

    public ProfileStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VrAudioSwitcher");
        ConfigPath = Path.Combine(dir, "config.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
            else
            {
                Config = new AppConfig();
            }
        }
        catch
        {
            // Corrupt/unreadable config: start fresh rather than crash the tray app.
            Config = new AppConfig();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public Profile? FindByName(string? name) =>
        name == null ? null
        : Config.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pick the profile to apply when SteamVR connects:
    /// 1) a profile flagged AutoSwitchOnHmd whose HmdModel matches the detected HMD;
    /// 2) otherwise the last used profile;
    /// 3) otherwise null (leave audio as-is).
    /// </summary>
    public Profile? ResolveProfileForHmd(string? hmdModel)
    {
        if (!string.IsNullOrWhiteSpace(hmdModel))
        {
            var match = Config.Profiles.FirstOrDefault(p =>
                p.AutoSwitchOnHmd &&
                !string.IsNullOrWhiteSpace(p.HmdModel) &&
                string.Equals(p.HmdModel, hmdModel, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return FindByName(Config.LastUsedProfileName);
    }
}
