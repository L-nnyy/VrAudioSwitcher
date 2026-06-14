using System.Runtime.InteropServices;

namespace VrAudioSwitcher.Hotkeys;

/// <summary>
/// Registers a single global hotkey via the Win32 RegisterHotKey API and raises
/// <see cref="Pressed"/> when it fires. Uses a message-only NativeWindow to
/// receive WM_HOTKEY without showing any UI.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xA51D;

    [Flags]
    private enum Mod : uint
    {
        Alt = 0x1,
        Control = 0x2,
        Shift = 0x4,
        Win = 0x8,
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly MessageWindow _window;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager()
    {
        _window = new MessageWindow(OnHotkey);
    }

    private void OnHotkey() => Pressed?.Invoke();

    /// <summary>
    /// Register (or replace) the hotkey from a string like "Control+Alt+V".
    /// Passing null/empty unregisters. Returns true on success.
    /// </summary>
    public bool Register(string? hotkey)
    {
        Unregister();
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        if (!TryParse(hotkey, out var mods, out var key)) return false;

        _registered = RegisterHotKey(_window.Handle, HotkeyId,
            (uint)(mods | Mod.NoRepeat), (uint)key);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
    }

    private static bool TryParse(string text, out Mod mods, out Keys key)
    {
        mods = 0;
        key = Keys.None;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= Mod.Control; break;
                case "alt": mods |= Mod.Alt; break;
                case "shift": mods |= Mod.Shift; break;
                case "win":
                case "windows": mods |= Mod.Win; break;
                default:
                    if (!Enum.TryParse(part, ignoreCase: true, out Keys parsed)) return false;
                    key = parsed;
                    break;
            }
        }
        return key != Keys.None && mods != 0;
    }

    /// <summary>Format modifiers + key as a config/display string, e.g. "Control+Alt+V".</summary>
    public static string Format(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        var parts = new List<string>();
        if ((keyData & Keys.Control) != 0) parts.Add("Control");
        if ((keyData & Keys.Alt) != 0) parts.Add("Alt");
        if ((keyData & Keys.Shift) != 0) parts.Add("Shift");
        if (key != Keys.None && key != Keys.ControlKey && key != Keys.Menu && key != Keys.ShiftKey)
            parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        Unregister();
        _window.Dispose();
    }

    private sealed class MessageWindow : NativeWindow, IDisposable
    {
        private readonly Action _onHotkey;

        public MessageWindow(Action onHotkey)
        {
            _onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
                _onHotkey();
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
