using System.Media;

namespace VrAudioSwitcher.Core;

/// <summary>Simple audio cues using built-in Windows system sounds.</summary>
public static class Sounds
{
    /// <summary>Played when the active profile / audio devices change.</summary>
    public static void PlaySwitch()
    {
        try { SystemSounds.Asterisk.Play(); } catch { /* never let a cue crash the app */ }
    }

    /// <summary>Played when desktop audio is restored after SteamVR quits.</summary>
    public static void PlayRestore()
    {
        try { SystemSounds.Exclamation.Play(); } catch { }
    }
}
