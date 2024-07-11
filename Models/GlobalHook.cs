using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class GlobalHook
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private HookProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action<IntPtr, string, string> OnWindowCreated;

    public GlobalHook()
    {
        _hookProc = HookCallback;
    }

    public void SetHook()
    {
        using (var process = Process.GetCurrentProcess())
        using (var module = process.MainModule)
        {
            _hookId = SetWindowsHookEx(WH_CBT, _hookProc, GetModuleHandle(null), 0);
        }
        if (_hookId == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Unhook()
    {
        UnhookWindowsHookEx(_hookId);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HCBT_CREATEWND)
        {
            var title = GetWindowTitle(wParam);
            var className = GetClassName(wParam);
            OnWindowCreated?.Invoke(wParam, title, className);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private string GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        var title = new StringBuilder(length + 1);
        GetWindowText(hWnd, title, title.Capacity);
        return title.ToString();
    }

    public string GetClassName(IntPtr hWnd)
    {
        StringBuilder className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString();
    }

    private const int WH_CBT = 5;
    private const int HCBT_CREATEWND = 3;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
}
