namespace VrAudioSwitcher.Profiles;

/// <summary>
/// A named audio setup: which output + microphone to make default.
/// Device names are stored alongside ids only for display when a device is
/// currently unplugged — matching/applying always uses the stable ids.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "New profile";

    public string? OutputId { get; set; }
    public string? OutputName { get; set; }

    public string? MicId { get; set; }
    public string? MicName { get; set; }

    /// <summary>HMD model this profile is associated with (for auto-switch).</summary>
    public string? HmdModel { get; set; }

    /// <summary>When true, applying happens automatically if the detected HMD matches.</summary>
    public bool AutoSwitchOnHmd { get; set; }
}
