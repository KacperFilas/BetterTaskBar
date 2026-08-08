using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BetterTaskBar;

public sealed class TrayForm : Form
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "BetterTaskBar";

    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autoStartItem;

    private KeyboardHook? _hook;
    private bool _revealed;
    private bool _autohideApplied;
    private bool _initialAutohide;

    public TrayForm()
    {
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
        _toggleItem.Click += (_, _) => Toggle();

        _autoStartItem = new ToolStripMenuItem("Uruchamiaj z systemem Windows");
        _autoStartItem.Click += (_, _) => ToggleAutoStart();

        var exitItem = new ToolStripMenuItem("Wyjdź");
        exitItem.Click += (_, _) => ExitApp();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_autoStartItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => Toggle();

        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (_, _) => SyncState();

        _autoStartItem.Checked = IsAutoStartEnabled();
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
        Restore();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_DISPLAYCHANGE:
                SyncState();
                break;
            case NativeMethods.WM_QUERYENDSESSION:
            case NativeMethods.WM_ENDSESSION:
                Restore();
                break;
        }
        base.WndProc(ref m);
    }

    private void Start()
    {
        _initialAutohide = TaskbarController.IsAutohide();
        _hook = new KeyboardHook();
        _hook.WinKeyPressed += OnWinKey;
        _hook.Install();
        _timer.Start();
        SyncState();
    }

    private void OnWinKey()
    {
        BeginInvoke(new Action(Toggle));
    }

    private void Toggle()
    {
        _revealed = !_revealed;
        _toggleItem.Text = _revealed ? "Ukryj taskbar" : "Pokaż taskbar";
        SyncState();
    }

    private void SyncState()
    {
        bool wantAutohide = !_revealed;
        if (wantAutohide != _autohideApplied)
        {
            _autohideApplied = wantAutohide;
            TaskbarController.SetAutohide(wantAutohide);
        }

        if (_revealed)
            TaskbarController.ShowAll();
        else
            TaskbarController.HideAll();
    }

    private void ToggleAutoStart()
    {
        bool enable = !_autoStartItem.Checked;
        SetAutoStart(enable);
        _autoStartItem.Checked = enable;
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) is not null;
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enable)
                key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue(RunValueName, false);
        }
        catch { }
    }

    private void Restore()
    {
        TaskbarController.ShowAll();
        TaskbarController.SetAutohide(_initialAutohide);
    }

    private void ExitApp()
    {
        _icon.Visible = false;
        _icon.Dispose();
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
