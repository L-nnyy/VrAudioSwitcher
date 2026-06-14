using System.Drawing;
using VrAudioSwitcher.Audio;
using VrAudioSwitcher.Core;
using VrAudioSwitcher.Hotkeys;
using VrAudioSwitcher.Profiles;

namespace VrAudioSwitcher.UI;

/// <summary>
/// Desktop configuration window: create/edit/delete profiles (output + mic +
/// optional HMD auto-switch), set the global cycle hotkey, and toggle launch at
/// startup / switch sounds. Built in code (no designer) with a flat modern theme.
/// </summary>
public sealed class ConfigForm : Form
{
    // Theme
    private static readonly Color Bg = Color.FromArgb(250, 250, 252);
    private static readonly Color Card = Color.White;
    private static readonly Color Accent = Color.FromArgb(56, 96, 160);
    private static readonly Color Border = Color.FromArgb(225, 227, 232);
    private static readonly Color TextMuted = Color.FromArgb(120, 124, 132);
    private static readonly Color TextDim = Color.FromArgb(90, 94, 102);

    private readonly AppController _controller;
    private readonly HotkeyManager _hotkeys;

    private readonly List<Profile> _working;
    private readonly List<AudioDeviceInfo> _outputs;
    private readonly List<AudioDeviceInfo> _mics;

    private readonly ListBox _list = new();
    private readonly TextBox _txtName = new();
    private readonly ComboBox _cmbOutput = new();
    private readonly ComboBox _cmbMic = new();
    private readonly CheckBox _chkAutoSwitch = new();
    private readonly TextBox _txtHmd = new();
    private readonly Button _btnCaptureHmd = new();
    private readonly HotkeyTextBox _txtHotkey = new();
    private readonly CheckBox _chkStartup = new();
    private readonly CheckBox _chkSound = new();

    private bool _loading;

    public ConfigForm(AppController controller, HotkeyManager hotkeys)
    {
        _controller = controller;
        _hotkeys = hotkeys;
        _outputs = controller.Audio.ListPlaybackDevices().ToList();
        _mics = controller.Audio.ListCaptureDevices().ToList();
        _working = controller.Store.Config.Profiles.Select(Clone).ToList();

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

    // ----- Layout -----

    private void BuildLayout()
    {
        Text = "VR Audio Switcher";
        Icon = IconFactory.CreateIcon(_controller.VrActive);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(724, 596);
        BackColor = Bg;
        Font = new Font("Segoe UI", 9f);

        BuildLeftColumn();
        BuildEditor();
        BuildFooter();

        _txtHotkey.SetInitial(_controller.Store.Config.CycleHotkey);
        _chkStartup.Checked = StartupManager.IsEnabled();
        _chkSound.Checked = _controller.Store.Config.PlaySwitchSound;
    }

    private void BuildLeftColumn()
    {
        Controls.Add(SectionLabel("PROFILES", 16, 16, 204));

        _list.SetBounds(16, 40, 204, 398);
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.IntegralHeight = false;
        _list.Font = new Font("Segoe UI", 10f);
        _list.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        Controls.Add(_list);

        var btnAdd = MakeButton("+  New", 16, 446, 98, primary: false);
        btnAdd.Click += (_, _) => AddProfile();
        var btnDel = MakeButton("Remove", 122, 446, 98, primary: false);
        btnDel.Click += (_, _) => DeleteProfile();
        Controls.Add(btnAdd);
        Controls.Add(btnDel);
    }

    private void BuildEditor()
    {
        // Card background for the editor area.
        var card = new Panel
        {
            Bounds = new Rectangle(244, 16, 464, 424),
            BackColor = Card,
            BorderStyle = BorderStyle.FixedSingle,
        };
        Controls.Add(card);

        int x = 20, w = 424, y = 16;
        card.Controls.Add(SectionLabel("PROFILE SETTINGS", x, y, w));

        y += 30;
        card.Controls.Add(FieldLabel("Name", x, y));
        StyleInput(_txtName);
        _txtName.SetBounds(x, y + 20, w, 26);
        _txtName.TextChanged += (_, _) => { if (!_loading) WriteBack(); RefreshListLabel(); };
        card.Controls.Add(_txtName);

        y += 60;
        card.Controls.Add(FieldLabel("Output  (headset / speakers)", x, y));
        StyleCombo(_cmbOutput);
        _cmbOutput.SetBounds(x, y + 20, w, 26);
        _cmbOutput.SelectedIndexChanged += (_, _) => { if (!_loading) WriteBack(); };
        card.Controls.Add(_cmbOutput);

        y += 60;
        card.Controls.Add(FieldLabel("Microphone", x, y));
        StyleCombo(_cmbMic);
        _cmbMic.SetBounds(x, y + 20, w, 26);
        _cmbMic.SelectedIndexChanged += (_, _) => { if (!_loading) WriteBack(); };
        card.Controls.Add(_cmbMic);

        y += 66;
        card.Controls.Add(Divider(x, y, w));

        y += 12;
        _chkAutoSwitch.Text = "Auto-switch to this profile when the bound headset is detected";
        _chkAutoSwitch.SetBounds(x, y, w, 24);
        _chkAutoSwitch.ForeColor = TextDim;
        _chkAutoSwitch.CheckedChanged += (_, _) => { if (!_loading) WriteBack(); };
        card.Controls.Add(_chkAutoSwitch);

        y += 30;
        card.Controls.Add(FieldLabel("Bound HMD model", x, y));
        StyleInput(_txtHmd);
        _txtHmd.SetBounds(x, y + 20, w - 134, 26);
        _txtHmd.TextChanged += (_, _) => { if (!_loading) WriteBack(); };
        card.Controls.Add(_txtHmd);

        _btnCaptureHmd.Text = "Use current";
        StyleButton(_btnCaptureHmd, primary: false);
        _btnCaptureHmd.SetBounds(x + w - 124, y + 19, 124, 28);
        _btnCaptureHmd.Click += (_, _) => CaptureCurrentHmd();
        card.Controls.Add(_btnCaptureHmd);
    }

    private void BuildFooter()
    {
        Controls.Add(Divider(16, 486, 692));

        Controls.Add(FieldLabel("Cycle hotkey", 16, 500));
        StyleInput(_txtHotkey);
        _txtHotkey.SetBounds(16, 520, 200, 26);
        Controls.Add(_txtHotkey);
        Controls.Add(new Label
        {
            Text = "click, then press keys (Backspace clears)",
            ForeColor = TextMuted,
            Left = 226, Top = 524, Width = 290, AutoSize = false,
        });

        _chkStartup.Text = "Launch at Windows startup";
        _chkStartup.SetBounds(16, 558, 230, 24);
        _chkStartup.ForeColor = TextDim;
        Controls.Add(_chkStartup);

        _chkSound.Text = "Play a sound on switch";
        _chkSound.SetBounds(256, 558, 220, 24);
        _chkSound.ForeColor = TextDim;
        Controls.Add(_chkSound);

        var btnSave = MakeButton("Save", 530, 554, 84, primary: true);
        btnSave.Click += (_, _) => { Save(); Close(); };
        var btnCancel = MakeButton("Cancel", 622, 554, 86, primary: false);
        btnCancel.Click += (_, _) => Close();
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        AcceptButton = null;       // hotkey box needs raw key input
        CancelButton = btnCancel;
    }

    // ----- Theme helpers -----

    private static Label SectionLabel(string text, int x, int y, int w) => new()
    {
        Text = text,
        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
        ForeColor = Color.FromArgb(110, 116, 128),
        Left = x, Top = y, Width = w, Height = 18,
    };

    private static Label FieldLabel(string text, int x, int y) => new()
    {
        Text = text,
        ForeColor = TextDim,
        Left = x, Top = y, Width = 400, Height = 16, AutoSize = true,
    };

    private static Label Divider(int x, int y, int w) => new()
    {
        BackColor = Border, Left = x, Top = y, Width = w, Height = 1,
    };

    private static void StyleInput(TextBox t)
    {
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = new Font("Segoe UI", 10f);
    }

    private static void StyleCombo(ComboBox c)
    {
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.FlatStyle = FlatStyle.Flat;
        c.Font = new Font("Segoe UI", 10f);
        c.DisplayMember = nameof(AudioDeviceInfo.Name);
    }

    private static Button MakeButton(string text, int x, int y, int w, bool primary)
    {
        var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 30 };
        StyleButton(b, primary);
        return b;
    }

    private static void StyleButton(Button b, bool primary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular);
        b.Cursor = Cursors.Hand;
        if (primary)
        {
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 112, 180);
        }
        else
        {
            b.BackColor = Color.White;
            b.ForeColor = TextDim;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 242, 246);
        }
    }

    // ----- Data binding -----

    private void LoadProfileList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in _working) _list.Items.Add(p.Name);
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
            cmb.Items.Add(new AudioDeviceInfo(id, $"{name ?? id} (unavailable)", isCapture));

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
        _txtName.Focus();
        _txtName.SelectAll();
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
        _controller.Store.Config.CycleHotkey = _txtHotkey.HotkeyString;
        _controller.Store.Config.LaunchAtStartup = _chkStartup.Checked;
        _controller.Store.Config.PlaySwitchSound = _chkSound.Checked;
        _controller.Store.Save();

        _hotkeys.Register(_txtHotkey.HotkeyString);
        StartupManager.SetEnabled(_chkStartup.Checked);

        _controller.NotifyConfigChanged();
    }
}
