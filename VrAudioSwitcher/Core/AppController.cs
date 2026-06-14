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
    // continuously while VR is OFF, so it reflects the user's real desktop state
    // captured BEFORE SteamVR hijacks the default device.
    private AudioSnapshot? _desktopSnapshot;
    private readonly System.Windows.Forms.Timer _snapshotTimer;

    /// <summary>The profile currently applied, or null when on the desktop baseline.</summary>
    public Profile? CurrentProfile { get; private set; }

    public bool VrActive => Watcher.IsConnected;

    /// <summary>Raised whenever VR status or the current profile changes (UI refresh).</summary>
    public event Action? StateChanged;

    public AppController()
    {
        Watcher.Connected += OnVrConnected;
        Watcher.Quit += OnVrQuit;
        _snapshotTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _snapshotTimer.Tick += (_, _) => RefreshDesktopBaseline();
    }

    public void Initialize()
    {
        Store.Load();
        RefreshDesktopBaseline();
        _snapshotTimer.Start();
        Watcher.Start();
    }

    // Keep the desktop baseline current while no VR session is active.
    private void RefreshDesktopBaseline()
    {
        if (VrActive) return;
        try { _desktopSnapshot = Audio.SnapshotCurrent(); }
        catch { /* transient COM hiccup; keep previous baseline */ }
    }

    private void OnVrConnected(string? hmdModel)
    {
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
        }
        CurrentProfile = null;
        StateChanged?.Invoke();
    }

    /// <summary>Apply a profile (output + mic on all roles) and remember it as last used.</summary>
    public void ApplyProfile(Profile profile)
    {
        Audio.ApplyProfile(profile.OutputId, profile.MicId);
        CurrentProfile = profile;
        Store.Config.LastUsedProfileName = profile.Name;
        Store.Save();
        StateChanged?.Invoke();
    }

    public void ApplyProfileByName(string name)
    {
        var p = Store.FindByName(name);
        if (p != null) ApplyProfile(p);
    }

    /// <summary>Cycle to the next profile in the list (wraps around).</summary>
    public Profile? CycleProfile()
    {
        var list = Store.Config.Profiles;
        if (list.Count == 0) return null;

        int idx = CurrentProfile == null ? -1 : list.IndexOf(CurrentProfile);
        var next = list[(idx + 1) % list.Count];
        ApplyProfile(next);
        return next;
    }

    private AudioSnapshot? SafeSnapshot()
    {
        try { return Audio.SnapshotCurrent(); } catch { return null; }
    }

    public void Dispose()
    {
        _snapshotTimer.Stop();
        _snapshotTimer.Dispose();
        Watcher.Dispose();
    }
}
