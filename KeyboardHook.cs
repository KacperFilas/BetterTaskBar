using System.Runtime.InteropServices;

namespace BetterTaskBar;

public sealed class KeyboardHook : IDisposable
{
    public event Action? WinKeyPressed;

    private NativeMethods.HookProc? _proc;
    private IntPtr _hook = IntPtr.Zero;

    public void Install()
    {
        if (_hook != IntPtr.Zero)
            return;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (kb.vkCode == NativeMethods.VK_LWIN || kb.vkCode == NativeMethods.VK_RWIN)
                {
                    bool injected = (kb.flags & NativeMethods.LLKHF_INJECTED) != 0;
                    if (!injected)
                        WinKeyPressed?.Invoke();
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
