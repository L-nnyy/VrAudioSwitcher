namespace VrAudioSwitcher.Profiles;

/// <summary>Persisted application configuration.</summary>
public sealed class AppConfig
{
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>Name of the last profile applied (fallback when no HMD auto-switch matches).</summary>
    public string? LastUsedProfileName { get; set; }

    /// <summary>Global hotkey to cycle profiles, e.g. "Control+Alt+V". Null = disabled.</summary>
    public string? CycleHotkey { get; set; }

    /// <summary>Whether the app registers itself to launch at Windows logon.</summary>
    public bool LaunchAtStartup { get; set; }
}
