using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CallRecording.Models;
using NLog;

namespace CallRecording.Services
{
    /// <summary>
    /// 窗口监控服务，用于检测目标软件（微信/QQ等）的语音通话窗口
    /// </summary>
    public class WindowMonitor : IDisposable
    {
        private readonly Logms _logms;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
            int idChild, uint dwEventThread, uint dwmsEventTime);

        private WinEventDelegate procDelegate;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        const uint EVENT_OBJECT_CREATE = 0x8000;
        const uint EVENT_OBJECT_DESTROY = 0x8001;
        const uint EVENT_OBJECT_SHOW = 0x8002;
        const uint EVENT_OBJECT_HIDE = 0x8003;
        const uint WINEVENT_OUTOFCONTEXT = 0;
        const int OBJID_WINDOW = 0;
        const int CHILDID_SELF = -4; // 0xFFFFFFF0

        private IntPtr hWinEventHook;

        public List<string> TargetClassNames { get; set; }
        public List<string> TargetProcessNames { get; set; }
        public List<string> TargetTitles { get; set; }

        public event EventHandler<IntPtr> WindowCreated;
        public event EventHandler<IntPtr> WindowDestroyed;

        private readonly ConcurrentQueue<WindowEventInfo> _eventQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _workerTask;

        public WindowMonitor(List<string> targetClassNames, List<string> targetProcessNames,
            List<string> targetTitles = null)
        {
            TargetClassNames = targetClassNames ?? new List<string>();
            TargetProcessNames = targetProcessNames ?? new List<string>();
            TargetTitles = targetTitles ?? new List<string>();

            procDelegate = new WinEventDelegate(WinEventProc);
            hWinEventHook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_HIDE, IntPtr.Zero, procDelegate, 0, 0,
                WINEVENT_OUTOFCONTEXT);

            _logms = new Logms();

            _workerTask = Task.Run(EventWorkerLoop, _cts.Token);
        }

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            // 忽略非窗口对象
            if (idObject != OBJID_WINDOW && idObject != CHILDID_SELF) return;

            // 入队，后台线程处理
            _eventQueue.Enqueue(new WindowEventInfo(hwnd, eventType));
        }

        private readonly ConcurrentDictionary<IntPtr, DateTime> _lastEventTimes = new();
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(2000);

        // PID 缓存：PID -> (ProcessName, ExpireTime)
        private readonly ConcurrentDictionary<uint, (string Name, DateTime ExpireTime)> _processCache = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

        private void EventWorkerLoop()
        {
            DateTime lastCleanupTime = DateTime.UtcNow;

            while (!_cts.Token.IsCancellationRequested)
            {
                // 定期清理过期缓存 (每分钟)
                if ((DateTime.UtcNow - lastCleanupTime).TotalMinutes > 1)
                {
                    CleanupCache();
                    lastCleanupTime = DateTime.UtcNow;
                }

                while (_eventQueue.TryDequeue(out var evt))
                {
                    try
                    {
                        GetWindowThreadProcessId(evt.Hwnd, out uint pid);
                        string processName = GetProcessName(pid);

                        if (string.IsNullOrEmpty(processName)) continue;

                        StringBuilder className = new StringBuilder(256);
                        GetClassName(evt.Hwnd, className, className.Capacity);
                        // 获取窗口标题
                        string windowTitle = WindowInfo.GetWindowTitle(evt.Hwnd);

                        logger.Info($"检测到窗口事件(所有事件): {processName}, {windowTitle}, {className}");

                        // 验证获取的窗口信息
                        string classNameStr = className.ToString().Trim();
                        string processNameStr = processName.Trim();
                        string windowTitleStr = windowTitle.Trim();

                        // 跳过无效窗口
                        if (string.IsNullOrEmpty(classNameStr) || string.IsNullOrEmpty(processNameStr))
                            continue;

                        // 排除软件自身窗口
                        if (windowTitleStr.Contains("通话录音助手"))
                            continue;

                        bool titleMatch = TargetTitles.Count == 0 || TargetTitles.Exists(t =>
                            !string.IsNullOrEmpty(t) && windowTitleStr.Contains(t));
                        bool classMatch = TargetClassNames.Count == 0 || TargetClassNames.Exists(c =>
                            !string.IsNullOrEmpty(c) && classNameStr.Contains(c));
                        bool processMatch = TargetProcessNames.Count == 0 || TargetProcessNames.Exists(p =>
                            !string.IsNullOrEmpty(p) && processNameStr.Contains(p));
                        if (!classMatch || !processMatch || !titleMatch)
                            continue;

                        // 防抖检查
                        var now = DateTime.UtcNow;
                        if (_lastEventTimes.TryGetValue(evt.Hwnd, out var lastTime))
                        {
                            if ((now - lastTime) < _debounceInterval)
                                continue; // 忽略短时间重复事件
                        }

                        _lastEventTimes[evt.Hwnd] = now;

                        // 在 UI 线程安全触发事件
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (evt.EventType == EVENT_OBJECT_CREATE || evt.EventType == EVENT_OBJECT_SHOW)
                            {
                                logger.Info($"窗口创建: {processName}, {windowTitle}");
                                WindowCreated?.Invoke(this, evt.Hwnd);
                            }
                            else if (evt.EventType == EVENT_OBJECT_DESTROY || evt.EventType == EVENT_OBJECT_HIDE)
                            {
                                logger.Info($"窗口销毁: {processName}, {windowTitle}");
                                WindowDestroyed?.Invoke(this, evt.Hwnd);
                            }
                        });
                    }
                    catch (ArgumentException)
                    {
                        // 进程可能已退出，忽略
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"处理窗口事件异常: {ex.Message}");
                        _logms.LogMessage($"处理窗口事件异常: {ex.Message}", "系统");
                    }
                }

                Thread.Sleep(10);
            }
        }

        private string GetProcessName(uint pid)
        {
            var now = DateTime.UtcNow;
            if (_processCache.TryGetValue(pid, out var cached) && cached.ExpireTime > now)
            {
                return cached.Name;
            }

            try
            {
                var process = Process.GetProcessById((int)pid);
                var name = process.ProcessName;
                _processCache[pid] = (name, now.Add(_cacheDuration));
                return name;
            }
            catch
            {
                return null;
            }
        }

        private void CleanupCache()
        {
            var now = DateTime.UtcNow;

            // 清理 _lastEventTimes
            foreach (var key in _lastEventTimes.Keys)
            {
                if (_lastEventTimes.TryGetValue(key, out var time))
                {
                    if ((now - time).TotalMinutes > 5) // 5分钟无事件则移除
                        _lastEventTimes.TryRemove(key, out _);
                }
            }

            // 清理 PID 缓存
            foreach (var key in _processCache.Keys)
            {
                if (_processCache.TryGetValue(key, out var item))
                {
                    if (item.ExpireTime < now)
                        _processCache.TryRemove(key, out _);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _workerTask.Wait(1000);
            }
            catch
            {
            }

            _cts.Dispose();
            UnhookWinEvent(hWinEventHook);
        }
    }

    public class WindowEventInfo
    {
        public IntPtr Hwnd { get; }
        public uint EventType { get; }

        public WindowEventInfo(IntPtr hwnd, uint eventType)
        {
            Hwnd = hwnd;
            EventType = eventType;
        }
    }
}