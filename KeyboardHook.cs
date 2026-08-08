using System.Runtime.InteropServices;

namespace BetterTaskBar;

public sealed class KeyboardHook : IDisposable
{
    public event Action? WinKeyPressed;

    private NativeMethods.HookProc? _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _winHeld;
    private bool _otherKeyPressed;

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
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool injected = (kb.flags & NativeMethods.LLKHF_INJECTED) != 0;

            if (!injected)
            {
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    if (kb.vkCode == NativeMethods.VK_LWIN || kb.vkCode == NativeMethods.VK_RWIN)
                    {
                        _winHeld = true;
                        _otherKeyPressed = false;
                    }
                    else if (_winHeld)
                    {
                        _otherKeyPressed = true;
                    }
                }
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                {
                    if (kb.vkCode == NativeMethods.VK_LWIN || kb.vkCode == NativeMethods.VK_RWIN)
                    {
                        if (_winHeld && !_otherKeyPressed)
                            WinKeyPressed?.Invoke();
                        _winHeld = false;
                    }
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
