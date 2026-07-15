using VrAudioSwitcher.Audio;
using VrAudioSwitcher.Profiles;
using VrAudioSwitcher.Vr;

namespace VrAudioSwitcher.Core;

/// <summary>
/// Central coordinator: owns the audio manager, profile store and SteamVR watcher,
/// and drives the snapshot / apply / restore flow. The tray, hotkey and VR overlay
/// all route their actions through here.
/// </summary>
public sealed class AppController : IDisposable
{
    public AudioManager Audio { get; } = new();
    public ProfileStore Store { get; } = new();
    public SteamVrWatcher Watcher { get; } = new();

    // The desktop audio baseline to restore when SteamVR quits. It is refreshed
    // (event-driven) whenever a default device changes while VR is OFF, so it
    // reflects the user's real desktop state captured BEFORE SteamVR hijacks it.
    private volatile AudioSnapshot? _desktopSnapshot;

    /// <summary>The profile currently applied, or null when on the desktop baseline.</summary>
    public Profile? CurrentProfile { get; private set; }

    /// <summary>Model of the most recently connected HMD (for "Use current" in config).</summary>
    public string? LastHmdModel { get; private set; }

    public bool VrActive => Watcher.IsConnected;

    /// <summary>Raised whenever VR status or the current profile changes (UI refresh).</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Raised when a profile could not be applied because a target endpoint is not
    /// present (e.g. VR headset asleep). The UI surfaces this instead of crashing.
    /// </summary>
    public event Action<Profile, AudioDeviceUnavailableException>? ProfileApplyFailed;

    public AppController()
    {
        Watcher.Connected += OnVrConnected;
        Watcher.Quit += OnVrQuit;
    }

    public void Initialize()
    {
        Store.Load();
        RefreshDesktopBaseline();
        // Event-driven baseline: re-snapshot only when a default actually changes.
        Audio.RegisterDefaultChangeCallback(RefreshDesktopBaseline);
        Watcher.Start();
    }

    // Capture the desktop baseline while no VR session is active. Called once at
    // startup and on every default-device change (from a system thread — reference
    // assignment is atomic, no lock needed).
    private void RefreshDesktopBaseline()
    {
        if (VrActive) return;
        try { _desktopSnapshot = Audio.SnapshotCurrent(); }
        catch { /* transient COM hiccup; keep previous baseline */ }
    }

    private void OnVrConnected(string? hmdModel)
    {
        LastHmdModel = hmdModel;
        // Fallback: if we somehow have no baseline (app started after SteamVR),
        // capture one now — best effort.
        _desktopSnapshot ??= SafeSnapshot();

        var profile = Store.ResolveProfileForHmd(hmdModel);
        if (profile != null)
            ApplyProfile(profile);
        else
            StateChanged?.Invoke();
    }

    private void OnVrQuit()
    {
        if (_desktopSnapshot != null)
        {
            try { Audio.Restore(_desktopSnapshot); } catch { /* devices may be gone */ }
            if (Store.Config.PlaySwitchSound) Sounds.PlayRestore();
        }
        CurrentProfile = null;
        StateChanged?.Invoke();
    }

    /// <summary>Apply a profile (output + mic on all roles) and remember it as last used.</summary>
    public bool ApplyProfile(Profile profile)
    {
        try
        {
            Audio.ApplyProfile(profile.OutputId, profile.MicId);
        }
        catch (AudioDeviceUnavailableException ex)
        {
            // Endpoint not present (headset asleep, device unplugged). Leave the
            // current default untouched and let the UI explain.
            ProfileApplyFailed?.Invoke(profile, ex);
            return false;
        }
        CurrentProfile = profile;
        Store.Config.LastUsedProfileName = profile.Name;
        Store.Save();
        if (Store.Config.PlaySwitchSound) Sounds.PlaySwitch();
        StateChanged?.Invoke();
        return true;
    }

    public void ApplyProfileByName(string name)
    {
        var p = Store.FindByName(name);
        if (p != null) ApplyProfile(p);
    }

    /// <summary>Called after the config window saves changes, to refresh dependent UI.</summary>
    public void NotifyConfigChanged() => StateChanged?.Invoke();

    /// <summary>Cycle to the next profile in the list (wraps around).</summary>
    public Profile? CycleProfile()
    {
        var list = Store.Config.Profiles;
        if (list.Count == 0) return null;

        int idx = CurrentProfile == null ? -1 : list.IndexOf(CurrentProfile);
        var next = list[(idx + 1) % list.Count];
        return ApplyProfile(next) ? next : null;
    }

    private AudioSnapshot? SafeSnapshot()
    {
        try { return Audio.SnapshotCurrent(); } catch { return null; }
    }

    public void Dispose()
    {
        Watcher.Dispose();
        Audio.Dispose();
    }
}
