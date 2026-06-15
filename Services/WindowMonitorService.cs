using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CallRecording.Models;
using MySharedProject;
using MySharedProject.Model;

namespace CallRecording.Services;

public class WindowMonitorService : IDisposable
{
    private readonly Logms _logms;
    private WindowMonitor _windowMonitor;
    private bool _disposed = false;

    public event EventHandler<IntPtr> WindowCreated;
    public event EventHandler<IntPtr> WindowDestroyed;

    public WindowMonitorService(Logms logms)
    {
        _logms = logms;
        InitializeWindowMonitor();
    }

    private void InitializeWindowMonitor()
    {
        var targetClassNames = new List<string> { "AudioWnd|WXworkWindow|Qt51514QWindowIcon" };
        var targetProcessNames = new List<string> { "WeChat|Weixin" };
        var targetTitles = new List<string> { "语音|微信音视频通话" };

        DataSource.gbmvvm.Cn = ConfigurationHelper.GetSetting("监控窗口类名");
        DataSource.gbmvvm.Pn = ConfigurationHelper.GetSetting("监控窗口进程名");
        DataSource.gbmvvm.Tt = ConfigurationHelper.GetSetting("监控窗口标题");

        if (!string.IsNullOrEmpty(DataSource.gbmvvm.Cn) && !string.IsNullOrEmpty(DataSource.gbmvvm.Pn) &&
            !string.IsNullOrEmpty(DataSource.gbmvvm.Tt))
        {
            targetClassNames = DataSource.gbmvvm.Cn.Split('|')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            targetProcessNames = DataSource.gbmvvm.Pn.Split('|')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            targetTitles = DataSource.gbmvvm.Tt.Split('|')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        else
        {
            ConfigurationHelper.SetSetting("监控窗口标题", "语音");
            DataSource.gbmvvm.Tt = ConfigurationHelper.GetSetting("监控窗口标题");
            targetTitles = new List<string> { DataSource.gbmvvm.Tt };
        }

        _windowMonitor = new WindowMonitor(targetClassNames, targetProcessNames, targetTitles);
        _windowMonitor.WindowCreated += OnWindowCreated;
        _windowMonitor.WindowDestroyed += OnWindowDestroyed;
    }

    private void OnWindowCreated(object sender, IntPtr hwnd)
    {
        StringBuilder className = new StringBuilder(256);
        WindowMonitor.GetClassName(hwnd, className, className.Capacity);

        WindowMonitor.GetWindowThreadProcessId(hwnd, out uint processId);
        Process process = Process.GetProcessById((int)processId);
        string processName = process.ProcessName;
        string title = process.MainWindowTitle;

        if (processName == "QQ" && title != "语音通话")
        {
            return;
        }

        int width = 0;
        int height = 0;
        if (processName == "Weixin")
        {
            RECT clientRect;
            if (GetClientRect(hwnd, out clientRect))
            {
                int clientWidth = clientRect.Right - clientRect.Left;
                int clientHeight = clientRect.Bottom - clientRect.Top;
                width = Math.Min(clientWidth, clientHeight);
                height = Math.Max(clientWidth, clientHeight);
            }

            bool.TryParse(ConfigurationHelper.GetSetting("是否启用微信窗口大小检测"), out bool isCheckSize);
            if (isCheckSize)
            {
                int w = int.Parse(ConfigurationHelper.GetSetting("微信通话窗口宽度"));
                int h = int.Parse(ConfigurationHelper.GetSetting("微信通话窗口高度"));
                if (width != w && height != h)
                {
                    return;
                }
            }
        }

        _logms?.LogMessage($"检测到通话窗口: {title}", "系统");
        WindowCreated?.Invoke(this, hwnd);
    }

    private void OnWindowDestroyed(object sender, IntPtr hwnd)
    {
        WindowDestroyed?.Invoke(this, hwnd);
    }

    public void Reinitialize()
    {
        _windowMonitor?.Dispose();
        InitializeWindowMonitor();
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
            _windowMonitor?.Dispose();
            _windowMonitor = null;
        }

        _disposed = true;
    }

    ~WindowMonitorService()
    {
        Dispose(false);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}