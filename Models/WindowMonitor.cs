using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CallRecording.Models;
using NLog;

namespace CallRecording.Services
{
    public class WindowMonitor : IDisposable
    {
        private readonly Logms _logms;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        // 定义WinEventProc回调函数委托
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
            int idChild, uint dwEventThread, uint dwmsEventTime);

        private WinEventDelegate procDelegate;

        // 导入SetWinEventHook函数
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        // 导入UnhookWinEvent函数
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // 导入GetClassName函数
        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // 导入GetWindowThreadProcessId函数
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // 事件常量
        const uint EVENT_OBJECT_CREATE = 0x8000;
        const uint EVENT_OBJECT_DESTROY = 0x8001;
        const uint EVENT_SYSTEM_DIALOGSTART = 0x0010;
        const uint EVENT_SYSTEM_DIALOGEND = 0x0011;
        const uint EVENT_OBJECT_SHOW = 0x8002;
        const uint EVENT_OBJECT_HIDE = 0x8003;
        const uint WINEVENT_OUTOFCONTEXT = 0;
        const int OBJID_WINDOW = 0;


        // 窗口钩子句柄
        private IntPtr hWinEventHook;

        // 目标窗口类名、进程名和标题列表
        public List<string> TargetClassNames { get; set; }
        public List<string> TargetProcessNames { get; set; }
        public List<string> TargetTitles { get; set; }

        // 窗口创建和销毁事件
        public event EventHandler<IntPtr> WindowCreated;
        public event EventHandler<IntPtr> WindowDestroyed;

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
        }

        private readonly Dictionary<IntPtr, DateTime> _pendingWindows = new();

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != OBJID_WINDOW) return; // 忽略子控件


            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);

            GetWindowThreadProcessId(hwnd, out uint processId);

            try
            {
                Process process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName;
                string windowTitle = WindowInfo.GetWindowTitle(hwnd);
                bool titleMatch = TargetTitles.Any(title => windowTitle.Contains(title));

                logger.Info($"检测到窗口事件: {eventType}, 窗口句柄: {hwnd}, 进程名: {processName}, 窗口标题: {windowTitle}");

                if (TargetClassNames.Contains(className.ToString()) && TargetProcessNames.Contains(processName) &&
                    titleMatch)
                {
                    logger.Info($"匹配到目标窗口: {processName}, {windowTitle}");

                    if (eventType == EVENT_OBJECT_CREATE || eventType == EVENT_OBJECT_SHOW)
                    {
                        logger.Info($"窗口创建: {processName}, {windowTitle}");
                        WindowCreated?.Invoke(this, hwnd);
                    }
                    else if (eventType == EVENT_OBJECT_DESTROY || eventType == EVENT_OBJECT_HIDE)
                    {
                        logger.Info($"窗口销毁: {processName}, {windowTitle}");
                        WindowDestroyed?.Invoke(this, hwnd);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                logger.Error($"(警告)无法获取进程名: {ex.Message}");
                _logms.LogMessage($"(警告)无法获取进程名: {ex.Message}", "系统");
            }
            catch (Exception ex)
            {
                logger.Error($"(警告)未知错误: {ex.Message}");
                _logms.LogMessage($"(警告)未知错误: {ex.Message}", "系统");
            }
        }

        public void Dispose()
        {
            UnhookWinEvent(hWinEventHook);
        }
    }
}