namespace VrAudioSwitcher.Audio;

/// <summary>
/// Captures the current default endpoints across every (flow, role) combination
/// so the exact desktop audio state can be restored later. This is the key to
/// "remet tout en ordre": we snapshot the real state right before applying a VR
/// profile, rather than relying on a fixed "Desktop" profile.
/// </summary>
public sealed class AudioSnapshot
{
    // Key: "flow:role" -> endpoint id (null when no default existed for that slot).
    public Dictionary<string, string?> Defaults { get; init; } = new();

    internal static string Key(EDataFlow flow, ERole role) => $"{(int)flow}:{(int)role}";
}
