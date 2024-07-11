using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CallRecording.Services
{
    public class WindowMonitor : IDisposable
    {
        // 定义WinEventProc回调函数委托
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        private WinEventDelegate procDelegate;

        // 导入SetWinEventHook函数
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

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
        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        // 窗口钩子句柄
        private IntPtr hWinEventHook;

        // 目标窗口类名和进程名列表
        public List<string> TargetClassNames { get; set; }
        public List<string> TargetProcessNames { get; set; }

        // 窗口创建和销毁事件
        public event EventHandler<IntPtr> WindowCreated;
        public event EventHandler<IntPtr> WindowDestroyed;

        public WindowMonitor(List<string> targetClassNames, List<string> targetProcessNames)
        {
            TargetClassNames = targetClassNames ?? new List<string>();
            TargetProcessNames = targetProcessNames ?? new List<string>();
            procDelegate = new WinEventDelegate(WinEventProc);
            hWinEventHook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_DESTROY, IntPtr.Zero, procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);

            GetWindowThreadProcessId(hwnd, out uint processId);
            Process process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;

            if (TargetClassNames.Contains(className.ToString()) && TargetProcessNames.Contains(processName))
            {
                if (eventType == EVENT_OBJECT_CREATE)
                {
                    WindowCreated?.Invoke(this, hwnd);
                }
                else if (eventType == EVENT_OBJECT_DESTROY)
                {
                    WindowDestroyed?.Invoke(this, hwnd);
                }
            }
        }

        public void Dispose()
        {
            UnhookWinEvent(hWinEventHook);
        }
    }
}
