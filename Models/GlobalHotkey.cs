using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CallRecording;

public class GlobalHotkey
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;

    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    private static Keys _hotkey = Keys.F9; // 默认启停快捷键
    private static Keys _stopHotkey = Keys.End; // 默认结束快捷键
    private static bool _useCustomStopHotkey = false; // 是否使用自定义结束热键
    private static HashSet<Keys> _registeredHotkeys = new HashSet<Keys>();

    public static event Action OnHotkeyPressed;
    public static event Action OnStopHotkeyPressed;

    public static bool RegisterHotkey(Keys hotkey)
    {
        // 不允许使用Ctrl作为前缀的键作为录音热键
        // if ((hotkey & Keys.Control) == Keys.Control)
        // {
        //     MessageBox.Show("录音快捷键不能以Ctrl作为前缀，请选择其他按键", "快捷键设置");
        //     return false;
        // }

        if (_registeredHotkeys.Contains(hotkey) || _registeredHotkeys.Contains(Keys.End))
        {
            MessageBox.Show("快捷键冲突, 请选择其他快捷键");
            return false; // 快捷键冲突
        }

        _hotkey = hotkey;
        _registeredHotkeys.Add(hotkey);
        _registeredHotkeys.Add(Keys.End); // 同时注册End作为停止热键
        _hookID = SetHook(_proc);
        return true;
    }

    // 新增方法：设置自定义结束热键
    public static bool SetCustomStopHotkey(Keys hotkey)
    {
        if (_registeredHotkeys.Contains(hotkey))
        {
            MessageBox.Show("快捷键冲突, 请选择其他快捷键");
            return false; // 快捷键冲突
        }

        // 移除旧的结束热键（如果不是默认的End键）
        if (_useCustomStopHotkey && _registeredHotkeys.Contains(_stopHotkey) && _stopHotkey != Keys.End)
        {
            _registeredHotkeys.Remove(_stopHotkey);
        }

        _stopHotkey = hotkey;
        _useCustomStopHotkey = true;

        // 如果不是默认的End键，则添加到注册列表中
        if (hotkey != Keys.End)
        {
            _registeredHotkeys.Add(hotkey);
        }

        return true;
    }

    // 新增方法：获取当前结束热键
    public static Keys GetStopHotkey()
    {
        return _stopHotkey;
    }

    // 新增方法：检查是否使用自定义结束热键
    public static bool IsUsingCustomStopHotkey()
    {
        return _useCustomStopHotkey;
    }

    public static void UnregisterHotkey()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _registeredHotkeys.Remove(_hotkey);
            _registeredHotkeys.Remove(Keys.End);
            // 如果使用了自定义结束热键且不是默认的End键，则移除它
            if (_useCustomStopHotkey && _stopHotkey != Keys.End)
            {
                _registeredHotkeys.Remove(_stopHotkey);
            }

            _hookID = IntPtr.Zero;
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Keys key = (Keys)vkCode;

            // 检查Ctrl键是否被按下
            bool isCtrlPressed = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;

            // 检查是否匹配普通热键
            if (key == _hotkey)
            {
                OnHotkeyPressed?.Invoke();
                return (IntPtr)1; // 表示已处理该消息
            }

            // 检查是否匹配结束热键（默认Ctrl+End或自定义热键）
            if ((_useCustomStopHotkey && key == _stopHotkey) ||
                (!_useCustomStopHotkey && key == Keys.End && isCtrlPressed && GlobalsVariables.是否正在录音))
            {
                OnStopHotkeyPressed?.Invoke();
                return (IntPtr)1; // 表示已处理该消息
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern short GetAsyncKeyState(Keys vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}