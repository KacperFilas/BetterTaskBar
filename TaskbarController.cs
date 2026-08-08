using System.Drawing;
using System.Runtime.InteropServices;

namespace BetterTaskBar;

public static class TaskbarController
{
    public static List<IntPtr> FindAll()
    {
        var result = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, l) =>
        {
            string cls = NativeMethods.GetClassName(hwnd);
            if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                result.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static Dictionary<IntPtr, Rectangle> TargetPositions()
    {
        var result = new Dictionary<IntPtr, Rectangle>();
        var monitors = MonitorUtil.Enumerate();
        foreach (var hwnd in FindAll())
        {
            var monitor = MatchMonitor(hwnd, monitors);
            if (monitor is null)
                continue;
            int height = monitor.Bounds.Bottom - monitor.WorkArea.Bottom;
            result[hwnd] = new Rectangle(
                monitor.Bounds.Left,
                monitor.Bounds.Bottom - height,
                monitor.Bounds.Width,
                height);
        }
        return result;
    }

    private static MonitorInfo2? MatchMonitor(IntPtr hwnd, List<MonitorInfo2> monitors)
    {
        NativeMethods.GetWindowRect(hwnd, out RECT rc);
        var rect = new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
        return monitors
            .OrderBy(m => Math.Abs(m.Bounds.Left - rect.Left))
            .ThenBy(m => m.IsPrimary ? 0 : 1)
            .FirstOrDefault();
    }

    public static void HideAll()
    {
        foreach (var hwnd in FindAll())
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    public static void ShowAll()
    {
        foreach (var hwnd in FindAll())
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        }
    }

    public static void SetAutohide(bool on)
    {
        foreach (var hwnd in FindAll())
        {
            var ab = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                lParam = on ? (IntPtr)ShellMethods.ABS_AUTOHIDE : IntPtr.Zero,
            };
            ShellMethods.SHAppBarMessage(ShellMethods.ABM_SETSTATE, ref ab);
        }
    }

    public static bool IsAutohide()
    {
        var bars = FindAll();
        if (bars.Count == 0)
            return false;
        var ab = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = bars[0] };
        return ShellMethods.SHAppBarMessage(ShellMethods.ABM_GETAUTOHIDEBAR, ref ab) != 0;
    }
}
