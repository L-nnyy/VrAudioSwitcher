using System.Drawing;
using VrAudioSwitcher.Audio;
using VrAudioSwitcher.Core;
using VrAudioSwitcher.Hotkeys;
using VrAudioSwitcher.Profiles;

namespace VrAudioSwitcher.UI;

/// <summary>
/// Desktop configuration window: create/edit/delete profiles (output + mic +
/// optional HMD auto-switch), set the global cycle hotkey, and toggle launch at
/// startup. Built in code (no designer) to keep the project lean.
/// </summary>
public sealed class ConfigForm : Form
{
    private readonly AppController _controller;
    private readonly HotkeyManager _hotkeys;

    // Working copy of the profile list; committed to the store on Save.
    private readonly List<Profile> _working;

    private readonly ListBox _list = new();
    private readonly TextBox _txtName = new();
    private readonly ComboBox _cmbOutput = new();
    private readonly ComboBox _cmbMic = new();
    private readonly CheckBox _chkAutoSwitch = new();
    private readonly TextBox _txtHmd = new();
    private readonly Button _btnCaptureHmd = new();
    private readonly TextBox _txtHotkey = new();
    private readonly CheckBox _chkStartup = new();

    private readonly List<AudioDeviceInfo> _outputs;
    private readonly List<AudioDeviceInfo> _mics;

    private string? _hotkeyString;
    private bool _loading;

    public ConfigForm(AppController controller, HotkeyManager hotkeys)
    {
        _controller = controller;
        _hotkeys = hotkeys;
        _outputs = controller.Audio.ListPlaybackDevices().ToList();
        _mics = controller.Audio.ListCaptureDevices().ToList();
        _working = controller.Store.Config.Profiles
            .Select(Clone).ToList();

        BuildLayout();
        LoadProfileList();
    }

    private static Profile Clone(Profile p) => new()
    {
        Name = p.Name,
        OutputId = p.OutputId,
        OutputName = p.OutputName,
        MicId = p.MicId,
        MicName = p.MicName,
        HmdModel = p.HmdModel,
        AutoSwitchOnHmd = p.AutoSwitchOnHmd,
    };

    private void BuildLayout()
    {
        Text = "VR Audio Switcher — Configuration";
        Icon = IconFactory.CreateIcon(_controller.VrActive);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 420);
        Font = new Font("Segoe UI", 9f);

        // --- Left: profile list + add/remove ---
        _list.SetBounds(12, 12, 180, 320);
        _list.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        Controls.Add(_list);

        var btnNew = new Button { Text = "New", Left = 12, Top = 338, Width = 85 };
        btnNew.Click += (_, _) => AddProfile();
        var btnDelete = new Button { Text = "Delete", Left = 107, Top = 338, Width = 85 };
        btnDelete.Click += (_, _) => DeleteProfile();
        Controls.Add(btnNew);
        Controls.Add(btnDelete);

        // --- Right: editor ---
        int x = 210, y = 12, w = 410;
        Controls.Add(new Label { Text = "Profile name", Left = x, Top = y, Width = w });
        _txtName.SetBounds(x, y + 20, w, 24);
        _txtName.TextChanged += (_, _) => { if (!_loading) WriteBack(); RefreshListLabel(); };
        Controls.Add(_txtName);

        y += 56;
        Controls.Add(new Label { Text = "Output (headset / speakers)", Left = x, Top = y, Width = w });
        _cmbOutput.SetBounds(x, y + 20, w, 24);
        _cmbOutput.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbOutput.DisplayMember = nameof(AudioDeviceInfo.Name);
        _cmbOutput.SelectedIndexChanged += (_, _) => { if (!_loading) WriteBack(); };
        Controls.Add(_cmbOutput);

        y += 56;
        Controls.Add(new Label { Text = "Microphone", Left = x, Top = y, Width = w });
        _cmbMic.SetBounds(x, y + 20, w, 24);
        _cmbMic.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMic.DisplayMember = nameof(AudioDeviceInfo.Name);
        _cmbMic.SelectedIndexChanged += (_, _) => { if (!_loading) WriteBack(); };
        Controls.Add(_cmbMic);

        y += 60;
        _chkAutoSwitch.Text = "Auto-switch to this profile for the bound headset";
        _chkAutoSwitch.SetBounds(x, y, w, 24);
        _chkAutoSwitch.CheckedChanged += (_, _) => { if (!_loading) WriteBack(); };
        Controls.Add(_chkAutoSwitch);

        y += 28;
        Controls.Add(new Label { Text = "Bound HMD model", Left = x, Top = y, Width = 160 });
        _txtHmd.SetBounds(x, y + 20, w - 130, 24);
        _txtHmd.TextChanged += (_, _) => { if (!_loading) WriteBack(); };
        Controls.Add(_txtHmd);
        _btnCaptureHmd.Text = "Use current";
        _btnCaptureHmd.SetBounds(x + w - 120, y + 19, 120, 26);
        _btnCaptureHmd.Click += (_, _) => CaptureCurrentHmd();
        Controls.Add(_btnCaptureHmd);

        // --- Bottom: global settings ---
        var sep = new Label { BorderStyle = BorderStyle.Fixed3D, Left = 12, Top = 348, Width = 608, Height = 2 };
        Controls.Add(sep);

        Controls.Add(new Label { Text = "Cycle hotkey", Left = 12, Top = 360, Width = 90 });
        _txtHotkey.SetBounds(104, 358, 160, 24);
        _txtHotkey.ReadOnly = true;
        _txtHotkey.KeyDown += OnHotkeyKeyDown;
        Controls.Add(_txtHotkey);

        _chkStartup.Text = "Launch at Windows startup";
        _chkStartup.SetBounds(284, 360, 220, 24);
        Controls.Add(_chkStartup);

        var btnSave = new Button { Text = "Save", Left = 444, Top = 386, Width = 85, DialogResult = DialogResult.OK };
        btnSave.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Cancel", Left = 535, Top = 386, Width = 85, DialogResult = DialogResult.Cancel };
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
        AcceptButton = null; // hotkey textbox captures Enter; don't auto-accept
        CancelButton = btnCancel;

        // Global settings initial values
        _hotkeyString = _controller.Store.Config.CycleHotkey;
        _txtHotkey.Text = _hotkeyString ?? "(none)";
        _chkStartup.Checked = StartupManager.IsEnabled();
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        var key = e.KeyCode;
        // Backspace/Delete clears the binding.
        if (key is Keys.Back or Keys.Delete)
        {
            _hotkeyString = null;
            _txtHotkey.Text = "(none)";
            return;
        }
        // Ignore lone modifier presses; wait for a real key.
        if (key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
            return;
        if (e.Modifiers == Keys.None)
        {
            _txtHotkey.Text = "(need a modifier)";
            return;
        }
        _hotkeyString = HotkeyManager.Format(e.Modifiers | key);
        _txtHotkey.Text = _hotkeyString;
    }

    private void LoadProfileList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in _working)
            _list.Items.Add(p.Name);
        _list.EndUpdate();
        if (_working.Count > 0) _list.SelectedIndex = 0;
        else SetEditorEnabled(false);
    }

    private Profile? Selected =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _working.Count
            ? _working[_list.SelectedIndex] : null;

    private void LoadSelectedProfile()
    {
        var p = Selected;
        if (p == null) { SetEditorEnabled(false); return; }
        SetEditorEnabled(true);

        _loading = true;
        _txtName.Text = p.Name;
        PopulateCombo(_cmbOutput, _outputs, p.OutputId, p.OutputName, isCapture: false);
        PopulateCombo(_cmbMic, _mics, p.MicId, p.MicName, isCapture: true);
        _chkAutoSwitch.Checked = p.AutoSwitchOnHmd;
        _txtHmd.Text = p.HmdModel ?? "";
        _loading = false;
    }

    private void SetEditorEnabled(bool on)
    {
        _txtName.Enabled = _cmbOutput.Enabled = _cmbMic.Enabled =
            _chkAutoSwitch.Enabled = _txtHmd.Enabled = _btnCaptureHmd.Enabled = on;
    }

    private static void PopulateCombo(ComboBox cmb, List<AudioDeviceInfo> devices,
        string? id, string? name, bool isCapture)
    {
        cmb.Items.Clear();
        foreach (var d in devices) cmb.Items.Add(d);

        if (id != null && devices.All(d => d.Id != id))
        {
            // Saved device not currently present — keep it selectable as a placeholder.
            cmb.Items.Add(new AudioDeviceInfo(id, $"{name ?? id} (unavailable)", isCapture));
        }

        int idx = -1;
        for (int i = 0; i < cmb.Items.Count; i++)
            if (((AudioDeviceInfo)cmb.Items[i]!).Id == id) { idx = i; break; }
        cmb.SelectedIndex = idx;
    }

    private void WriteBack()
    {
        var p = Selected;
        if (p == null) return;
        p.Name = string.IsNullOrWhiteSpace(_txtName.Text) ? "Unnamed" : _txtName.Text.Trim();
        if (_cmbOutput.SelectedItem is AudioDeviceInfo o) { p.OutputId = o.Id; p.OutputName = o.Name; }
        if (_cmbMic.SelectedItem is AudioDeviceInfo m) { p.MicId = m.Id; p.MicName = m.Name; }
        p.AutoSwitchOnHmd = _chkAutoSwitch.Checked;
        p.HmdModel = string.IsNullOrWhiteSpace(_txtHmd.Text) ? null : _txtHmd.Text.Trim();
    }

    private void RefreshListLabel()
    {
        if (_list.SelectedIndex >= 0 && Selected != null)
            _list.Items[_list.SelectedIndex] = Selected.Name;
    }

    private void AddProfile()
    {
        var p = new Profile
        {
            Name = $"Profile {_working.Count + 1}",
            OutputId = _controller.Audio.GetDefaultId(EDataFlow.Render, ERole.Console),
            MicId = _controller.Audio.GetDefaultId(EDataFlow.Capture, ERole.Console),
        };
        p.OutputName = _outputs.FirstOrDefault(d => d.Id == p.OutputId)?.Name;
        p.MicName = _mics.FirstOrDefault(d => d.Id == p.MicId)?.Name;
        _working.Add(p);
        _list.Items.Add(p.Name);
        _list.SelectedIndex = _working.Count - 1;
    }

    private void DeleteProfile()
    {
        int idx = _list.SelectedIndex;
        if (idx < 0) return;
        _working.RemoveAt(idx);
        _list.Items.RemoveAt(idx);
        if (_working.Count > 0)
            _list.SelectedIndex = Math.Min(idx, _working.Count - 1);
        else
            SetEditorEnabled(false);
    }

    private void CaptureCurrentHmd()
    {
        if (!_controller.VrActive)
        {
            MessageBox.Show(this, "SteamVR is not running, no HMD detected.",
                "No HMD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        // The model was reported on connect; ask the user to type it if blank.
        // (We expose it via the controller's current profile resolution path instead.)
        var model = _controller.LastHmdModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            MessageBox.Show(this, "Could not read the HMD model.", "No HMD",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _txtHmd.Text = model;
        _chkAutoSwitch.Checked = true;
    }

    private void Save()
    {
        WriteBack();

        _controller.Store.Config.Profiles = _working;
        _controller.Store.Config.CycleHotkey = _hotkeyString;
        _controller.Store.Config.LaunchAtStartup = _chkStartup.Checked;
        _controller.Store.Save();

        _hotkeys.Register(_hotkeyString);
        StartupManager.SetEnabled(_chkStartup.Checked);

        _controller.NotifyConfigChanged();
    }
}
