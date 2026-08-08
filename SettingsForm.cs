using System.Drawing;
using System.Windows.Forms;

namespace BetterTaskBar;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private NumericUpDown _secondsBox = null!;
    private CheckBox _winKeyCheck = null!;
    private CheckBox _hotkeyCheck = null!;
    private TextBox _hotkeyBox = null!;
    private CheckBox _autoStartCheck = null!;

    private Keys _keyCode = Keys.None;
    private Keys _modKeys = Keys.None;

    public AppSettings Result { get; private set; } = null!;

    public SettingsForm(AppSettings current)
    {
        _settings = current;
        Build();
        LoadValues();
    }

    private void Build()
    {
        const int pad = 12;
        Text = "BetterTaskBar — ustawienia";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(460, 300);
        var w = ClientSize.Width - 2 * pad;

        var info = new Label
        {
            Text = "Taskbar jest ukrywany na wszystkich monitorach.\r\nPojawia się po naciśnięciu klawisza Windows lub własnego skrótu.",
            AutoSize = false,
            Size = new Size(w, 34),
            ForeColor = SystemColors.GrayText,
            Location = new Point(pad, pad),
        };

        var secondsLabel = new Label
        {
            Text = "Pokaż taskbar przez:",
            AutoSize = true,
            Location = new Point(pad, info.Bottom + 14),
        };
        _secondsBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 60,
            DecimalPlaces = 0,
            Increment = 1,
            Location = new Point(secondsLabel.Right + 8, info.Bottom + 11),
            Size = new Size(55, 24),
        };
        var secondsUnit = new Label
        {
            Text = "sekund (0 = przełącznik klawiszem)",
            AutoSize = true,
            Location = new Point(_secondsBox.Right + 6, info.Bottom + 14),
        };

        _winKeyCheck = new CheckBox
        {
            Text = "Reaguj na klawisz Windows",
            AutoSize = true,
            Location = new Point(pad, info.Bottom + 48),
        };

        _hotkeyCheck = new CheckBox
        {
            Text = "Własny skrót:",
            AutoSize = true,
            Location = new Point(pad, info.Bottom + 74),
        };
        _hotkeyBox = new TextBox
        {
            ReadOnly = true,
            Enabled = false,
            TabStop = false,
            Location = new Point(_hotkeyCheck.Right + 8, info.Bottom + 72),
            Size = new Size(180, 24),
        };
        var hotkeyHint = new Label
        {
            Text = "Kliknij i naciśnij kombinację (np. Ctrl+Shift+H)",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(pad, info.Bottom + 100),
        };

        _autoStartCheck = new CheckBox
        {
            Text = "Uruchamiaj razem z systemem Windows",
            AutoSize = true,
            Location = new Point(pad, info.Bottom + 126),
        };

        var saveBtn = new Button
        {
            Text = "Zapisz",
            Location = new Point(ClientSize.Width - 180, ClientSize.Height - 46),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK,
        };
        var cancelBtn = new Button
        {
            Text = "Anuluj",
            Location = new Point(ClientSize.Width - 92, ClientSize.Height - 46),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel,
        };

        Controls.AddRange(new Control[]
        {
            info, secondsLabel, _secondsBox, secondsUnit,
            _winKeyCheck, _hotkeyCheck, _hotkeyBox, hotkeyHint, _autoStartCheck,
            saveBtn, cancelBtn,
        });

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
        saveBtn.Click += (_, _) => Save();

        _hotkeyCheck.CheckedChanged += (_, _) => _hotkeyBox.Enabled = _hotkeyCheck.Checked;
        _hotkeyBox.KeyDown += HotkeyBox_KeyDown;
    }

    private void LoadValues()
    {
        _secondsBox.Value = (decimal)Math.Clamp(_settings.RevealSeconds, 0, 60);
        _winKeyCheck.Checked = _settings.WinKeyReveals;
        _hotkeyCheck.Checked = _settings.UseCustomHotkey;
        _autoStartCheck.Checked = _settings.AutoStart;
        _hotkeyBox.Enabled = _settings.UseCustomHotkey;

        if (_settings.UseCustomHotkey && _settings.CustomHotkeyCode != 0)
        {
            _keyCode = (Keys)_settings.CustomHotkeyCode;
            _modKeys = (Keys)_settings.CustomHotkeyModifiers;
            _hotkeyBox.Text = FormatHotkey(_modKeys, _keyCode);
        }
    }

    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        var key = e.KeyCode;
        bool isModifier = key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin
            or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu;
        if (isModifier)
            return;

        if (key is Keys.Back or Keys.Delete or Keys.Escape)
        {
            _keyCode = Keys.None;
            _modKeys = Keys.None;
            _hotkeyBox.Text = "";
            return;
        }

        _keyCode = key;
        _modKeys = Control.ModifierKeys;
        _hotkeyBox.Text = FormatHotkey(_modKeys, _keyCode);
    }

    private static string FormatHotkey(Keys mods, Keys key)
    {
        var parts = new List<string>();
        if ((mods & Keys.Control) != 0) parts.Add("Ctrl");
        if ((mods & Keys.Alt) != 0) parts.Add("Alt");
        if ((mods & Keys.Shift) != 0) parts.Add("Shift");
        if (key != Keys.None) parts.Add(key.ToString());
        return parts.Count > 0 ? string.Join("+", parts) : "";
    }

    private void Save()
    {
        if (_hotkeyCheck.Checked && _keyCode == Keys.None)
        {
            MessageBox.Show("Wybierz skrót — kliknij pole i naciśnij kombinację klawiszy.",
                "BetterTaskBar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Result = new AppSettings
        {
            RevealSeconds = (double)_secondsBox.Value,
            WinKeyReveals = _winKeyCheck.Checked,
            UseCustomHotkey = _hotkeyCheck.Checked,
            CustomHotkeyCode = (int)_keyCode,
            CustomHotkeyModifiers = (int)_modKeys,
            AutoStart = _autoStartCheck.Checked,
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
