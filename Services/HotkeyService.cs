using System.Windows.Forms;
using CallRecording.Models;
using MySharedProject.Model;

namespace CallRecording.Services;

public class HotkeyService : IDisposable
{
    private readonly Logms _logms;
    private Keys _currentHotkey = Keys.F9;
    private Keys _currentStopHotkey = Keys.End;
    private bool _disposed = false;

    public event Action OnHotkeyPressed;
    public event Action OnStopHotkeyPressed;

    public HotkeyService(Logms logms)
    {
        _logms = logms;
        LoadSettings();
        RegisterDefaultHotkeys();
    }

    private void LoadSettings()
    {
        string startHotkeyStr = ConfigurationHelper.GetSetting("录音快捷键");
        if (!string.IsNullOrEmpty(startHotkeyStr) && Enum.TryParse<Keys>(startHotkeyStr, out Keys startKey))
        {
            _currentHotkey = startKey;
        }

        string stopHotkeyStr = ConfigurationHelper.GetSetting("结束录音快捷键");
        if (!string.IsNullOrEmpty(stopHotkeyStr) && Enum.TryParse<Keys>(stopHotkeyStr, out Keys stopKey))
        {
            _currentStopHotkey = stopKey;
        }
    }

    private void RegisterDefaultHotkeys()
    {
        GlobalHotkey.RegisterHotkey(_currentHotkey);
        GlobalHotkey.OnHotkeyPressed += () => OnHotkeyPressed?.Invoke();
        GlobalHotkey.OnStopHotkeyPressed += () => OnStopHotkeyPressed?.Invoke();

        if (_currentStopHotkey != Keys.End)
        {
            GlobalHotkey.SetCustomStopHotkey(_currentStopHotkey);
        }
    }

    public Keys CurrentHotkey
    {
        get => _currentHotkey;
        set => SetHotkey(value);
    }

    public Keys CurrentStopHotkey
    {
        get => _currentStopHotkey;
        set => SetStopHotkey(value);
    }

    public bool SetHotkey(Keys hotkey)
    {
        GlobalHotkey.UnregisterHotkey();

        bool success = GlobalHotkey.RegisterHotkey(hotkey);
        if (success)
        {
            _currentHotkey = hotkey;
            ConfigurationHelper.SetSetting("录音快捷键", hotkey.ToString());
            _logms?.LogMessage($"录音快捷键已设置为: {hotkey}", "设置");
        }
        else
        {
            GlobalHotkey.RegisterHotkey(_currentHotkey);
            ConfigurationHelper.SetSetting("录音快捷键", _currentHotkey.ToString());
            _logms?.LogMessage($"快捷键 {hotkey} 冲突，已恢复为: {_currentHotkey}", "设置");
        }

        return success;
    }

    public bool SetStopHotkey(Keys hotkey)
    {
        bool success = GlobalHotkey.SetCustomStopHotkey(hotkey);
        if (success)
        {
            _currentStopHotkey = hotkey;
            ConfigurationHelper.SetSetting("结束录音快捷键", hotkey.ToString());
            _logms?.LogMessage($"结束录音快捷键已设置为: {hotkey}", "设置");
        }
        else
        {
            GlobalHotkey.SetCustomStopHotkey(_currentStopHotkey);
            ConfigurationHelper.SetSetting("结束录音快捷键", _currentStopHotkey.ToString());
            _logms?.LogMessage($"快捷键 {hotkey} 冲突，已恢复为: {_currentStopHotkey}", "设置");
        }

        return success;
    }

    public void UnregisterAll()
    {
        GlobalHotkey.UnregisterHotkey();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            UnregisterAll();
        }

        _disposed = true;
    }

    ~HotkeyService()
    {
        Dispose(false);
    }
}