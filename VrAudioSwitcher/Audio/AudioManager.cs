using System.Runtime.InteropServices;

namespace VrAudioSwitcher.Audio;

/// <summary>
/// High-level audio control: enumerate endpoints, read/apply defaults across the
/// three Windows roles (Console / Multimedia / Communications), and snapshot /
/// restore the full default state.
/// </summary>
public sealed class AudioManager : IDisposable
{
    private static readonly ERole[] AllRoles =
        { ERole.Console, ERole.Multimedia, ERole.Communications };

    private static readonly EDataFlow[] AllFlows =
        { EDataFlow.Render, EDataFlow.Capture };

    // Long-lived enumerator + client for default-change notifications (event-driven,
    // replaces polling).
    private IMMDeviceEnumerator? _notifyEnumerator;
    private NotificationClient? _notifyClient;

    public IReadOnlyList<AudioDeviceInfo> ListPlaybackDevices() => List(EDataFlow.Render);

    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices() => List(EDataFlow.Capture);

    private static List<AudioDeviceInfo> List(EDataFlow flow)
    {
        var result = new List<AudioDeviceInfo>();
        var enumerator = CreateEnumerator();
        try
        {
            Check(enumerator.EnumAudioEndpoints(flow, AudioConstants.DEVICE_STATE_ACTIVE,
                out var collection), "EnumAudioEndpoints");
            Check(collection.GetCount(out var count), "GetCount");
            for (uint i = 0; i < count; i++)
            {
                Check(collection.Item(i, out var device), "Item");
                try
                {
                    var id = GetId(device);
                    var name = GetFriendlyName(device) ?? id;
                    result.Add(new AudioDeviceInfo(id, name, flow == EDataFlow.Capture));
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
            Marshal.ReleaseComObject(collection);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
        return result;
    }

    /// <summary>Current default endpoint id for the given flow/role, or null if none.</summary>
    public string? GetDefaultId(EDataFlow flow, ERole role)
    {
        var enumerator = CreateEnumerator();
        try
        {
            int hr = enumerator.GetDefaultAudioEndpoint(flow, role, out var device);
            if (hr != 0 || device is null) return null;
            try { return GetId(device); }
            finally { Marshal.ReleaseComObject(device); }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    /// <summary>Set <paramref name="deviceId"/> as default on all three roles.</summary>
    public void SetDefaultAllRoles(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return;
        var policy = (IPolicyConfig)new PolicyConfigClientComObject();
        try
        {
            foreach (var role in AllRoles)
                Check(policy.SetDefaultEndpoint(deviceId, role), $"SetDefaultEndpoint({role})");
        }
        finally
        {
            Marshal.ReleaseComObject(policy);
        }
    }

    /// <summary>Apply a profile: set output and microphone defaults (each on all roles).</summary>
    public void ApplyProfile(string? outputId, string? micId)
    {
        if (!string.IsNullOrEmpty(outputId)) SetDefaultAllRoles(outputId!);
        if (!string.IsNullOrEmpty(micId)) SetDefaultAllRoles(micId!);
    }

    /// <summary>Snapshot the current default endpoint for every flow/role.</summary>
    public AudioSnapshot SnapshotCurrent()
    {
        var snap = new AudioSnapshot();
        foreach (var flow in AllFlows)
            foreach (var role in AllRoles)
                snap.Defaults[AudioSnapshot.Key(flow, role)] = GetDefaultId(flow, role);
        return snap;
    }

    /// <summary>Restore a previously captured snapshot, per flow/role.</summary>
    public void Restore(AudioSnapshot snapshot)
    {
        var policy = (IPolicyConfig)new PolicyConfigClientComObject();
        try
        {
            foreach (var flow in AllFlows)
                foreach (var role in AllRoles)
                {
                    if (snapshot.Defaults.TryGetValue(AudioSnapshot.Key(flow, role), out var id)
                        && !string.IsNullOrEmpty(id))
                    {
                        // Best effort: a device may have been unplugged since the snapshot.
                        policy.SetDefaultEndpoint(id!, role);
                    }
                }
        }
        finally
        {
            Marshal.ReleaseComObject(policy);
        }
    }

    /// <summary>
    /// Subscribe to default-device changes (Console role only, to fire once per real
    /// change). The callback runs on a system thread. Replaces polling for the
    /// desktop baseline.
    /// </summary>
    public void RegisterDefaultChangeCallback(Action onDefaultChanged)
    {
        if (_notifyEnumerator != null) return;
        _notifyEnumerator = CreateEnumerator();
        _notifyClient = new NotificationClient(onDefaultChanged);
        _notifyEnumerator.RegisterEndpointNotificationCallback(_notifyClient);
    }

    private static IMMDeviceEnumerator CreateEnumerator() =>
        (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

    public void Dispose()
    {
        if (_notifyEnumerator != null && _notifyClient != null)
        {
            try { _notifyEnumerator.UnregisterEndpointNotificationCallback(_notifyClient); }
            catch { /* shutting down */ }
            Marshal.ReleaseComObject(_notifyEnumerator);
            _notifyEnumerator = null;
            _notifyClient = null;
        }
    }

    // Minimal IMMNotificationClient: only OnDefaultDeviceChanged matters; fire once
    // per change by filtering on the Console role.
    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly Action _onChanged;
        public NotificationClient(Action onChanged) => _onChanged = onChanged;

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
        {
            if (role == ERole.Console) _onChanged();
            return 0;
        }

        public int OnDeviceStateChanged(string deviceId, uint newState) => 0;
        public int OnDeviceAdded(string deviceId) => 0;
        public int OnDeviceRemoved(string deviceId) => 0;
        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }

    private static string GetId(IMMDevice device)
    {
        Check(device.GetId(out var id), "GetId");
        return id;
    }

    private static string? GetFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(AudioConstants.STGM_READ, out var store) != 0)
            return null;
        try
        {
            var key = new PropertyKey
            {
                FmtId = AudioConstants.PKEY_Device_FriendlyName_FmtId,
                Pid = AudioConstants.PKEY_Device_FriendlyName_Pid,
            };
            if (store.GetValue(ref key, out var value) != 0) return null;
            return value.GetString();
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static void Check(int hr, string what)
    {
        if (hr != 0) throw new COMException($"Core Audio call failed: {what}", hr);
    }
}
