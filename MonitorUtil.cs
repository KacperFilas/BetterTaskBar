using System.Drawing;
using System.Runtime.InteropServices;

namespace BetterTaskBar;

public sealed class MonitorInfo2
{
    public Rectangle Bounds;
    public Rectangle WorkArea;
    public bool IsPrimary;
}

public static class MonitorUtil
{
    public static List<MonitorInfo2> Enumerate()
    {
        var list = new List<MonitorInfo2>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT lprc, IntPtr lParam) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                list.Add(new MonitorInfo2
                {
                    Bounds = ToRect(mi.rcMonitor),
                    WorkArea = ToRect(mi.rcWork),
                    IsPrimary = (mi.dwFlags & 1) != 0,
                });
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static Rectangle ToRect(RECT r) =>
        new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
