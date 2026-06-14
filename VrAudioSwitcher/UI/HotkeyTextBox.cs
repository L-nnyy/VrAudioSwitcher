using VrAudioSwitcher.Hotkeys;

namespace VrAudioSwitcher.UI;

/// <summary>
/// A textbox that captures a single hotkey combo. WinForms normally swallows
/// Tab / arrows / Enter as dialog-navigation keys before KeyDown fires, which
/// breaks naive hotkey capture — overriding <see cref="IsInputKey"/> forces every
/// key to reach us. Backspace/Delete clears the binding.
/// </summary>
public sealed class HotkeyTextBox : TextBox
{
    public string? HotkeyString { get; private set; }

    public HotkeyTextBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Hand;
        TextAlign = HorizontalAlignment.Center;
    }

    public void SetInitial(string? hotkey)
    {
        HotkeyString = hotkey;
        Text = hotkey ?? "(none)";
    }

    // Treat all keys as input so Tab/arrows/Enter raise KeyDown instead of navigating.
    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Text = "press keys…";
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Text = HotkeyString ?? "(none)";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        var key = e.KeyCode;

        if (key is Keys.Back or Keys.Delete)
        {
            Commit(null);
            return;
        }
        // Wait for a real key alongside the modifier.
        if (key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
            return;
        if (e.Modifiers == Keys.None)
        {
            Text = "need a modifier (Ctrl/Alt/Shift)";
            return;
        }

        Commit(HotkeyManager.Format(e.Modifiers | key));
    }

    private void Commit(string? hotkey)
    {
        HotkeyString = hotkey;
        Text = hotkey ?? "(none)";
    }
}
