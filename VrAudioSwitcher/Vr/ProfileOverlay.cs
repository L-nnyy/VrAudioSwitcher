using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Valve.VR;
using VrAudioSwitcher.Core;
using VrAudioSwitcher.Profiles;

namespace VrAudioSwitcher.Vr;

/// <summary>
/// A SteamVR dashboard overlay (like ovrAdvancedSettings / OVR Toolkit) that lets
/// the user switch profiles from inside the headset. The UI is drawn to a CPU
/// bitmap and pushed via SetOverlayRaw; laser clicks are read via overlay mouse
/// events and hit-tested against the profile buttons.
///
/// Switch-only by design: editing profiles (picking devices) stays on the desktop
/// window, which is far easier than typing in VR.
/// </summary>
public sealed class ProfileOverlay : IDisposable
{
    private const string OverlayKey = "vraudioswitcher.dashboard";
    private const int Width = 420;
    private const int HeaderH = 56;
    private const int RowH = 64;
    private const int Pad = 12;

    private readonly AppController _controller;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly uint _eventSize;

    private ulong _handle = OpenVR.k_ulOverlayHandleInvalid;
    private ulong _thumb = OpenVR.k_ulOverlayHandleInvalid;
    private bool _created;
    private int _height;
    private readonly List<RectangleF> _buttonRects = new();

    public ProfileOverlay(AppController controller)
    {
        _controller = controller;
        _eventSize = (uint)Marshal.SizeOf(typeof(VREvent_t));
        _timer = new System.Windows.Forms.Timer { Interval = 75 };
        _timer.Tick += (_, _) => PollEvents();
        _controller.StateChanged += OnStateChanged;
    }

    private void OnStateChanged()
    {
        if (_created) Render();
    }

    /// <summary>Create the overlay. Call once SteamVR (OpenVR) is connected.</summary>
    public void Create()
    {
        var ovr = OpenVR.Overlay;
        if (ovr == null || _created) return;

        var err = ovr.CreateDashboardOverlay(OverlayKey, "VR Audio Switcher",
            ref _handle, ref _thumb);
        if (err != EVROverlayError.None) return;

        ovr.SetOverlayWidthInMeters(_handle, 0.5f);
        ovr.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);

        _created = true;
        SetThumbnail();
        Render();
        _timer.Start();
    }

    /// <summary>Destroy the overlay. Call when SteamVR quits.</summary>
    public void Destroy()
    {
        _timer.Stop();
        if (!_created) return;
        var ovr = OpenVR.Overlay;
        if (ovr != null && _handle != OpenVR.k_ulOverlayHandleInvalid)
            ovr.DestroyOverlay(_handle);
        _handle = _thumb = OpenVR.k_ulOverlayHandleInvalid;
        _created = false;
    }

    private void SetThumbnail()
    {
        var ovr = OpenVR.Overlay;
        if (ovr == null || _thumb == OpenVR.k_ulOverlayHandleInvalid) return;
        using var bmp = IconFactory.CreateBitmap(128, _controller.VrActive);
        PushBitmap(ovr, _thumb, bmp);
    }

    private void Render()
    {
        var ovr = OpenVR.Overlay;
        if (ovr == null || !_created) return;

        var profiles = _controller.Store.Config.Profiles;
        _height = HeaderH + Math.Max(1, profiles.Count) * RowH + Pad;

        using var bmp = new Bitmap(Width, _height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.FromArgb(245, 24, 26, 30));

            using var titleFont = new Font("Segoe UI", 18f, FontStyle.Bold);
            using var rowFont = new Font("Segoe UI", 15f, FontStyle.Regular);
            using var subFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            using var white = new SolidBrush(Color.White);
            using var dim = new SolidBrush(Color.FromArgb(170, 255, 255, 255));

            var statusText = _controller.VrActive ? "VR active" : "Desktop";
            g.DrawString($"Audio profile  ·  {statusText}", titleFont, white, Pad, 12);

            _buttonRects.Clear();
            if (profiles.Count == 0)
            {
                g.DrawString("No profiles yet — create them on the desktop.",
                    subFont, dim, Pad, HeaderH + 8);
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                var rect = new RectangleF(Pad, HeaderH + i * RowH, Width - 2 * Pad, RowH - 8);
                _buttonRects.Add(rect);

                bool active = ReferenceEquals(p, _controller.CurrentProfile);
                using var bg = new SolidBrush(active
                    ? Color.FromArgb(46, 160, 67)
                    : Color.FromArgb(255, 44, 48, 56));
                using (var path = RoundedRect(rect, 10))
                    g.FillPath(bg, path);

                g.DrawString(p.Name, rowFont, white, rect.X + 14, rect.Y + 8);
                var sub = $"{p.OutputName ?? "—"}  /  {p.MicName ?? "—"}";
                g.DrawString(sub, subFont, dim, rect.X + 14, rect.Y + 34);
            }
        }

        PushBitmap(ovr, _handle, bmp);

        // Mouse coordinates are reported in this scale (pixels), origin bottom-left.
        var scale = new HmdVector2_t { v0 = Width, v1 = _height };
        ovr.SetOverlayMouseScale(_handle, ref scale);
    }

    private void PollEvents()
    {
        var ovr = OpenVR.Overlay;
        if (ovr == null || !_created) return;

        var ev = new VREvent_t();
        while (ovr.PollNextOverlayEvent(_handle, ref ev, _eventSize))
        {
            if (ev.eventType == (uint)EVREventType.VREvent_MouseButtonUp)
            {
                // Flip Y: overlay origin is bottom-left, bitmap is top-left.
                float x = ev.data.mouse.x;
                float y = _height - ev.data.mouse.y;
                HitTest(x, y);
            }
        }
    }

    private void HitTest(float x, float y)
    {
        var profiles = _controller.Store.Config.Profiles;
        for (int i = 0; i < _buttonRects.Count && i < profiles.Count; i++)
        {
            if (_buttonRects[i].Contains(x, y))
            {
                _controller.ApplyProfile(profiles[i]);
                return;
            }
        }
    }

    private static void PushBitmap(CVROverlay ovr, ulong handle, Bitmap bmp)
    {
        var buf = ToRgba(bmp);
        var pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try
        {
            ovr.SetOverlayRaw(handle, pin.AddrOfPinnedObject(),
                (uint)bmp.Width, (uint)bmp.Height, 4);
        }
        finally
        {
            pin.Free();
        }
    }

    private static byte[] ToRgba(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int n = bmp.Width * bmp.Height * 4;
        var buf = new byte[n];
        Marshal.Copy(data.Scan0, buf, 0, n);
        bmp.UnlockBits(data);
        // GDI gives BGRA; OpenVR wants RGBA. Swap R and B.
        for (int i = 0; i < n; i += 4)
            (buf[i], buf[i + 2]) = (buf[i + 2], buf[i]);
        return buf;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        Destroy();
    }
}
