using System.Text;
using Valve.VR;

namespace VrAudioSwitcher.Vr;

/// <summary>
/// Watches SteamVR's lifecycle without launching it. Initialises OpenVR as a
/// Background application (which fails harmlessly while SteamVR is not running),
/// reconnects automatically, reads the connected HMD model, and fires
/// <see cref="Connected"/> / <see cref="Quit"/> so the app can apply/restore
/// audio profiles.
///
/// Runs on the WinForms UI thread via a timer — all OpenVR calls stay on a single
/// thread and events are raised on the UI thread, so handlers can touch UI freely.
/// </summary>
public sealed class SteamVrWatcher : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private bool _connected;
    private uint _eventSize;

    /// <summary>Raised when SteamVR becomes available. Argument is the HMD model (may be null).</summary>
    public event Action<string?>? Connected;

    /// <summary>Raised when SteamVR has quit (or the connection was lost).</summary>
    public event Action? Quit;

    public bool IsConnected => _connected;

    public SteamVrWatcher(int pollIntervalMs = 1500)
    {
        _eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));
        _timer = new System.Windows.Forms.Timer { Interval = pollIntervalMs };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();

    private void Tick()
    {
        if (!_connected)
            TryConnect();
        else
            PumpEvents();
    }

    private void TryConnect()
    {
        var err = EVRInitError.None;
        // Background type never starts SteamVR; it fails with
        // Init_NoServerForBackgroundApp until the runtime is up.
        var system = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Background);
        if (err != EVRInitError.None || system == null)
        {
            // Make sure no partial state lingers before the next attempt.
            OpenVR.Shutdown();
            return;
        }

        _connected = true;
        string? model = ReadHmdModel(system);
        Connected?.Invoke(model);
    }

    private void PumpEvents()
    {
        var system = OpenVR.System;
        if (system == null)
        {
            HandleDisconnect();
            return;
        }

        try
        {
            var ev = new VREvent_t();
            while (system.PollNextEvent(ref ev, _eventSize))
            {
                if (ev.eventType == (uint)EVREventType.VREvent_Quit)
                {
                    // Acknowledge so SteamVR doesn't wait on us, then tear down.
                    system.AcknowledgeQuit_Exiting();
                    HandleDisconnect();
                    return;
                }
            }
        }
        catch
        {
            // Runtime vanished unexpectedly (e.g. crash): treat as a disconnect.
            HandleDisconnect();
        }
    }

    private void HandleDisconnect()
    {
        OpenVR.Shutdown();
        _connected = false;
        Quit?.Invoke();
    }

    private static string? ReadHmdModel(CVRSystem system)
    {
        var err = ETrackedPropertyError.TrackedProp_Success;
        var sb = new StringBuilder(256);
        uint len = system.GetStringTrackedDeviceProperty(
            OpenVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_ModelNumber_String,
            sb, (uint)sb.Capacity, ref err);
        if (err != ETrackedPropertyError.TrackedProp_Success || len == 0)
            return null;
        return sb.ToString();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        if (_connected)
        {
            OpenVR.Shutdown();
            _connected = false;
        }
    }
}
