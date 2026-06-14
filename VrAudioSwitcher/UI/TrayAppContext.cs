using System.Drawing;
using VrAudioSwitcher.Core;
using VrAudioSwitcher.Hotkeys;
using VrAudioSwitcher.Vr;

namespace VrAudioSwitcher.UI;

/// <summary>
/// The application: a system-tray presence (no main window) that runs in the
/// background. Owns the controller and hotkeys, exposes a context menu to switch
/// profiles / open config / exit, and shows balloon notifications on VR and
/// profile changes.
/// </summary>
public sealed class TrayAppContext : ApplicationContext
{
    private readonly AppController _controller = new();
    private readonly HotkeyManager _hotkeys = new();
    private readonly NotifyIcon _tray = new();
    private readonly ProfileOverlay _overlay;
    private Icon? _iconRef;
    private ConfigForm? _configForm;

    public TrayAppContext()
    {
        _overlay = new ProfileOverlay(_controller);
        _controller.StateChanged += OnStateChanged;
        _controller.Watcher.Connected += OnVrConnected;
        _controller.Watcher.Quit += OnVrQuit;
        _hotkeys.Pressed += OnHotkeyPressed;

        _iconRef = IconFactory.CreateIcon(false);
        _tray.Icon = _iconRef;
        _tray.Text = "VR Audio Switcher";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => OpenConfig();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => BuildMenu(menu);
        _tray.ContextMenuStrip = menu;

        _controller.Initialize();
        _hotkeys.Register(_controller.Store.Config.CycleHotkey);

        if (_controller.Store.Config.Profiles.Count == 0)
            ShowBalloon("Open the menu → Configuration to create your first profile.");
    }

    private void OnStateChanged() => UpdateIcon();

    private void OnVrConnected(string? model)
    {
        UpdateIcon();
        _overlay.Create();
        var profile = _controller.CurrentProfile;
        ShowBalloon(profile != null
            ? $"SteamVR detected → applied \"{profile.Name}\""
            : "SteamVR detected (no matching profile)");
    }

    private void OnVrQuit()
    {
        _overlay.Destroy();
        UpdateIcon();
        ShowBalloon("SteamVR closed → desktop audio restored");
    }

    private void OnHotkeyPressed()
    {
        var p = _controller.CycleProfile();
        if (p != null) ShowBalloon($"Profile: {p.Name}");
    }

    private void BuildMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        var status = _controller.VrActive ? "VR active" : "Desktop";
        var current = _controller.CurrentProfile?.Name ?? "(baseline)";
        menu.Items.Add(new ToolStripMenuItem($"{status} — {current}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        foreach (var p in _controller.Store.Config.Profiles)
        {
            var item = new ToolStripMenuItem(p.Name)
            {
                Checked = ReferenceEquals(p, _controller.CurrentProfile),
            };
            var captured = p;
            item.Click += (_, _) => { _controller.ApplyProfile(captured); ShowBalloon($"Profile: {captured.Name}"); };
            menu.Items.Add(item);
        }
        if (_controller.Store.Config.Profiles.Count > 0)
            menu.Items.Add(new ToolStripSeparator());

        var cfg = new ToolStripMenuItem("Configuration…");
        cfg.Click += (_, _) => OpenConfig();
        menu.Items.Add(cfg);

        var startup = new ToolStripMenuItem("Launch at startup") { Checked = StartupManager.IsEnabled() };
        startup.Click += (_, _) =>
        {
            bool now = !StartupManager.IsEnabled();
            StartupManager.SetEnabled(now);
        };
        menu.Items.Add(startup);

        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThreadCore();
        menu.Items.Add(exit);
    }

    private void OpenConfig()
    {
        if (_configForm is { IsDisposed: false })
        {
            _configForm.Activate();
            return;
        }
        _configForm = new ConfigForm(_controller, _hotkeys);
        _configForm.FormClosed += (_, _) => UpdateIcon();
        _configForm.Show();
    }

    private void UpdateIcon()
    {
        var old = _iconRef;
        _iconRef = IconFactory.CreateIcon(_controller.VrActive);
        _tray.Icon = _iconRef;
        IconFactory.DestroyHandle(old);

        var current = _controller.CurrentProfile?.Name;
        _tray.Text = current == null
            ? "VR Audio Switcher"
            : $"VR Audio Switcher — {current}";
    }

    private void ShowBalloon(string text)
    {
        _tray.BalloonTipTitle = "VR Audio Switcher";
        _tray.BalloonTipText = text;
        _tray.ShowBalloonTip(3000);
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _configForm?.Close();
        _overlay.Dispose();
        _hotkeys.Dispose();
        _controller.Dispose();
        IconFactory.DestroyHandle(_iconRef);
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
