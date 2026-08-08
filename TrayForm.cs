using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BetterTaskBar;

public sealed class TrayForm : Form
{
    private const int HotkeyId = 1;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "BetterTaskBar";

    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _fastTimer;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autoStartItem;

    private AppSettings _settings;
    private KeyboardHook? _hook;
    private bool _revealed;
    private DateTime _revealUntil;
    private bool _initialAutohide;
    private Dictionary<IntPtr, Rectangle> _positions = new();

    public TrayForm()
    {
        _settings = AppSettings.Load();

        Text = "BetterTaskBar";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;

        _icon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "BetterTaskBar",
            Visible = true,
        };

        _toggleItem = new ToolStripMenuItem("Ukryj taskbar");
        _toggleItem.Click += (_, _) => ToggleManual();

        _autoStartItem = new ToolStripMenuItem("Uruchamiaj z systemem Windows");
        _autoStartItem.Click += (_, _) => ToggleAutoStart();

        var settingsItem = new ToolStripMenuItem("Ustawienia…");
        settingsItem.Click += (_, _) => OpenSettings();

        var exitItem = new ToolStripMenuItem("Wyjdź");
        exitItem.Click += (_, _) => ExitApp();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_autoStartItem);
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => OpenSettings();

        _fastTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _fastTimer.Tick += (_, _) => FastTick();

        _autoStartItem.Checked = _settings.AutoStart;
        _ = Handle;
    }

    protected override void SetVisibleCore(bool value)
    {
        if (value)
        {
            base.SetVisibleCore(false);
            return;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RestoreTaskbars();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY when m.WParam.ToInt32() == HotkeyId:
                Reveal();
                break;
            case NativeMethods.WM_DISPLAYCHANGE:
                ApplyState();
                break;
            case NativeMethods.WM_QUERYENDSESSION:
            case NativeMethods.WM_ENDSESSION:
                RestoreTaskbars();
                break;
        }
        base.WndProc(ref m);
    }

    private void Start()
    {
        ApplyAutoStartRegistry();
        _initialAutohide = TaskbarController.IsAutohide();
        RefreshPositions();
        TaskbarController.SetAutohide(true);
        _hook = new KeyboardHook();
        _hook.WinKeyPressed += OnWinKey;
        _hook.Install();
        RegisterHotkey();
        UpdateToggleMenuText();
        _fastTimer.Start();
        ApplyState();
        if (AppSettings.IsFirstRun())
            OpenSettings();
    }

    private void OnWinKey()
    {
        if (!_settings.WinKeyReveals)
            return;
        BeginInvoke(new Action(Reveal));
    }

    private void Reveal()
    {
        if (_settings.RevealSeconds <= 0)
        {
            _revealed = !_revealed;
            UpdateToggleMenuText();
            return;
        }

        _revealed = true;
        _revealUntil = DateTime.Now.AddSeconds(_settings.RevealSeconds);
        UpdateToggleMenuText();
    }

    private void FastTick()
    {
        if (_revealed && _settings.RevealSeconds > 0 && DateTime.Now >= _revealUntil)
            _revealed = false;

        var bars = TaskbarController.FindAll();
        if (bars.Count != _positions.Count || bars.Any(h => !_positions.ContainsKey(h)))
            RefreshPositions();

        if (_revealed)
            RepositionAndShow(bars);
        else
            TaskbarController.HideAll();
    }

    private void ApplyState()
    {
        var bars = TaskbarController.FindAll();
        if (_revealed)
            RepositionAndShow(bars);
        else
            TaskbarController.HideAll();
    }

    private void RefreshPositions()
    {
        _positions = TaskbarController.TargetPositions();
    }

    private void RepositionAndShow(List<IntPtr> bars)
    {
        foreach (var hwnd in bars)
        {
            if (_positions.TryGetValue(hwnd, out var r))
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height,
                    NativeMethods.SWP_NOACTIVATE);
            }
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
        }
    }

    private void ToggleManual()
    {
        if (_revealed)
        {
            _revealed = false;
            UpdateToggleMenuText();
        }
        else
        {
            Reveal();
        }
    }

    private void ToggleAutoStart()
    {
        _settings.AutoStart = !_settings.AutoStart;
        ApplyAutoStartRegistry();
        _autoStartItem.Checked = _settings.AutoStart;
        _settings.Save();
    }

    private void ApplyAutoStartRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (_settings.AutoStart)
                key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue(RunValueName, false);
        }
        catch { }
    }

    private void RegisterHotkey()
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        if (!_settings.UseCustomHotkey || _settings.CustomHotkeyCode == 0)
            return;

        uint mods = NativeMethods.MOD_NOREPEAT;
        var m = (Keys)_settings.CustomHotkeyModifiers;
        if ((m & Keys.Control) != 0) mods |= NativeMethods.MOD_CONTROL;
        if ((m & Keys.Alt) != 0) mods |= NativeMethods.MOD_ALT;
        if ((m & Keys.Shift) != 0) mods |= NativeMethods.MOD_SHIFT;

        bool ok = NativeMethods.RegisterHotKey(Handle, HotkeyId, mods, (uint)_settings.CustomHotkeyCode);
        if (!ok)
            MessageBox.Show("Nie udało się zarejestrować własnego skrótu (może być już zajęty).",
                "BetterTaskBar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        _settings = form.Result;
        _settings.Save();
        ApplyAutoStartRegistry();
        _autoStartItem.Checked = _settings.AutoStart;
        RegisterHotkey();
        UpdateToggleMenuText();
    }

    private void UpdateToggleMenuText()
    {
        _toggleItem.Text = _revealed ? "Ukryj taskbar" : "Pokaż taskbar";
    }

    private void RestoreTaskbars()
    {
        TaskbarController.ShowAll();
        TaskbarController.SetAutohide(_initialAutohide);
    }    private void ExitApp()
    {
        _icon.Visible = false;
        _icon.Dispose();
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        _hook?.Dispose();
        Application.Exit();
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var screen = new Rectangle(3, 4, 26, 18);
            using (var bg = new SolidBrush(Color.FromArgb(45, 45, 48)))
            using (var pen = new Pen(Color.FromArgb(130, 130, 140), 1.5f))
            {
                g.FillRectangle(bg, screen);
                g.DrawRectangle(pen, screen);
            }

            using (var bar = new SolidBrush(Color.FromArgb(0, 120, 215)))
                g.FillRectangle(bar, new Rectangle(screen.X + 1, screen.Y + screen.Height - 5, screen.Width - 2, 4));

            using (var arrow = new SolidBrush(Color.White))
            {
                var pts = new[]
                {
                    new Point(16, 6),
                    new Point(12, 11),
                    new Point(20, 11),
                };
                g.FillPolygon(arrow, pts);
            }
        }

        IntPtr h = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(h);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(h);
        }
    }
}
