using System.Diagnostics;
using System.Runtime.InteropServices;

public static class AntiDebugHelper
{
    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll")]
    private static extern void OutputDebugString(string lpOutputString);

    /// <summary>
    /// 检测调试器附加
    /// </summary>
    public static bool IsDebuggerAttached()
    {
        return Debugger.IsAttached || IsDebuggerPresent();
    }

    /// <summary>
    /// 检测已知调试/反编译工具进程
    /// </summary>
    public static bool IsSuspiciousProcessRunning()
    {
        string[] suspiciousProcesses = { "ida64", "ida32", "x64dbg", "x32dbg", "ollydbg", "processhacker", "dnspy" };
        return suspiciousProcesses.Any(p => Process.GetProcessesByName(p).Length > 0);
    }


    /// <summary>
    /// 综合检测
    /// </summary>
    public static bool IsBeingDebugged()
    {
        return IsDebuggerAttached() || IsSuspiciousProcessRunning();
    }

    /// <summary>
    /// 反调试响应
    /// </summary>
    public static void AntiDebugResponse()
    {
        if (IsBeingDebugged())
        {
            Environment.FailFast("检测到调试器或异常环境，程序已终止。");
        }
    }

    /// <summary>
    /// 动态延迟检测（后台线程定期检测）
    /// </summary>
    public static void StartDynamicAntiDebug(int intervalMilliseconds = 30000)
    {
        Thread thread = new Thread(() =>
        {
            while (true)
            {
                if (IsBeingDebugged())
                {
                    Environment.FailFast("检测到调试器或异常环境，程序已终止。");
                }

                Thread.Sleep(intervalMilliseconds);
            }
        })
        {
            IsBackground = true,
            Name = "AntiDebugThread"
        };
        thread.Start();
    }

    /// <summary>
    /// 输出调试字符串检测（迷惑调试器）
    /// </summary>
    public static void OutputDebugTrap()
    {
        OutputDebugString("AntiDebugTrap");
    }
}