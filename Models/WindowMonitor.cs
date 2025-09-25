using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CallRecording.Models;
using NLog;

namespace CallRecording.Services
{
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
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);

        private void EventWorkerLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                while (_eventQueue.TryDequeue(out var evt))
                {
                    try
                    {
                        GetWindowThreadProcessId(evt.Hwnd, out uint pid);
                        Process process = Process.GetProcessById((int)pid);
                        string processName = process.ProcessName;

                        StringBuilder className = new StringBuilder(256);
                        GetClassName(evt.Hwnd, className, className.Capacity);
                        string windowTitle = WindowInfo.GetWindowTitle(evt.Hwnd);

                        logger.Info($"检测到窗口事件(所有事件): {processName}, {windowTitle}, {className}");


                        bool titleMatch = TargetTitles.Count == 0 || TargetTitles.Exists(t => windowTitle.Contains(t));
                        if (!TargetClassNames.Contains(className.ToString()) ||
                            !TargetProcessNames.Contains(processName) ||
                            !titleMatch)
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
                        Application.Current.Dispatcher.Invoke(() =>
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
                    catch (Exception ex)
                    {
                        logger.Error($"处理窗口事件异常: {ex.Message}");
                        _logms.LogMessage($"处理窗口事件异常: {ex.Message}", "系统");
                    }
                }

                Thread.Sleep(10);
            }
        }


        public void Dispose()
        {
            _cts.Cancel();
            _workerTask.Wait();
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