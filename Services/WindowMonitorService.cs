using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CallRecording.Models;
using MySharedProject;
using MySharedProject.Model;
using NLog;

namespace CallRecording.Services;

public class WindowMonitorService : IDisposable
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private readonly Logms _logms;
    private WindowMonitor _windowMonitor;
    private bool _disposed = false;

    /// <summary>
    /// 当前生效的监控窗口列表。变更后调用 <see cref="Reinitialize"/> 重建底层 hook。
    /// 每项的 ProcessName / ClassName / Title 支持 '|' 分隔多个候选项，匹配时使用 Contains 模糊匹配。
    /// </summary>
    private List<MonitoredWindow> _monitoredWindows = new();

    public event EventHandler<IntPtr> WindowCreated;
    public event EventHandler<IntPtr> WindowDestroyed;

    public WindowMonitorService(Logms logms, IEnumerable<MonitoredWindow> initialWindows)
    {
        _logms = logms;
        _monitoredWindows = initialWindows?.ToList() ?? new List<MonitoredWindow>();
        InitializeWindowMonitor();
    }

    /// <summary>
    /// 整体替换监控窗口配置并重建监听。
    /// </summary>
    public void UpdateMonitoredWindows(IEnumerable<MonitoredWindow> windows)
    {
        _monitoredWindows = windows?.ToList() ?? new List<MonitoredWindow>();
        Reinitialize();
    }

    private void InitializeWindowMonitor()
    {
        // 把 MonitoredWindow 列表按字段展平，传给底层 WindowMonitor 做 Contains 模糊匹配。
        // 空字符串会自动被底层忽略，因此未填写的字段不会误命中。
        var classNames = _monitoredWindows
            .SelectMany(w => SplitAndTrim(w.ClassName))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();
        var processNames = _monitoredWindows
            .SelectMany(w => SplitAndTrim(w.ProcessName))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();
        var titles = _monitoredWindows
            .SelectMany(w => SplitAndTrim(w.Title))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        _windowMonitor = new WindowMonitor(classNames, processNames, titles);
        _windowMonitor.WindowCreated += OnWindowCreated;
        _windowMonitor.WindowDestroyed += OnWindowDestroyed;
    }

    private static IEnumerable<string> SplitAndTrim(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void OnWindowCreated(object sender, IntPtr hwnd)
    {
        StringBuilder className = new StringBuilder(256);
        WindowMonitor.GetClassName(hwnd, className, className.Capacity);

        WindowMonitor.GetWindowThreadProcessId(hwnd, out uint processId);
        Process process = Process.GetProcessById((int)processId);
        string processName = process.ProcessName;
        // 必须取指定窗口标题，不能用 MainWindowTitle（多窗口进程会取错）
        string title = WindowInfo.GetWindowTitle(hwnd);

        // QQ 老逻辑：标题必须为 "语音通话" 才认作通话窗口
        if (processName == "QQ" && title != "语音通话")
        {
            return;
        }

        // 在配置列表中找到命中本窗口的项（按相同的三元组匹配逻辑）
        var matched = _monitoredWindows.FirstOrDefault(w => MatchesWindow(w, processName, className.ToString(), title));
        if (matched == null)
        {
            return;
        }

        // 命中项若启用了大小检测，则按其记录的宽高校验实际客户区尺寸
        if (matched.SizeCheckEnabled && matched.Width.HasValue && matched.Height.HasValue)
        {
            RECT clientRect;
            int actualWidth = 0;
            int actualHeight = 0;
            bool gotRect = false;
            try
            {
                if (GetClientRect(hwnd, out clientRect))
                {
                    int clientWidth = clientRect.Right - clientRect.Left;
                    int clientHeight = clientRect.Bottom - clientRect.Top;
                    // 自动调整竖屏/横屏：长边作为高度，短边作为宽度
                    actualWidth = Math.Min(clientWidth, clientHeight);
                    actualHeight = Math.Max(clientWidth, clientHeight);
                    gotRect = true;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "读取窗口客户区尺寸失败，跳过尺寸校验");
            }

            if (gotRect && (actualWidth != matched.Width.Value || actualHeight != matched.Height.Value))
            {
                string msg =
                    $"通话窗口尺寸不匹配（{matched.DisplayName}），本次未自动录音。实际: {actualWidth}x{actualHeight}，配置: {matched.Width}x{matched.Height}。请在通话中点「添加监控窗口」重新校准，或取消该行的「启用大小检测」";
                logger.Info(msg);
                _logms?.LogMessage(msg, "系统");
                return;
            }
        }

        _logms?.LogMessage($"检测到通话窗口: {title}", "系统");
        WindowCreated?.Invoke(this, hwnd);
    }

    /// <summary>
    /// 用与底层 WindowMonitor 一致的 Contains 模糊匹配规则判断目标窗口是否命中配置项。
    /// 任一字段为空表示该字段不参与过滤（向后兼容历史行为）。
    /// </summary>
    private static bool MatchesWindow(MonitoredWindow w, string processName, string className, string title)
    {
        bool processMatch = SplitAndTrim(w.ProcessName).Any(p =>
            !string.IsNullOrEmpty(p) && processName.Contains(p, StringComparison.OrdinalIgnoreCase));
        bool classMatch = SplitAndTrim(w.ClassName).Any(c =>
            !string.IsNullOrEmpty(c) && className.Contains(c, StringComparison.Ordinal));
        bool titleMatch = SplitAndTrim(w.Title).Any(t =>
            !string.IsNullOrEmpty(t) && title.Contains(t, StringComparison.Ordinal));
        return processMatch && classMatch && titleMatch;
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