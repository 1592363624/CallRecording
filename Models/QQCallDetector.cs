using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;

namespace CallRecording.Models
{
    public static class QQCallDetector
    {
        public static bool IsQQCallActive()
        {
            try
            {
                var processName = "QQ"; // QQ 的进程名
                var windowClassName = "Chrome_WidgetWin_1"; // QQ 通话窗口的类名
                var callWindowTitleKeyword = "语音通话"; // QQ 通话窗口标题中包含的关键词

                // 检查是否有 QQ 进程在运行
                var isProcessRunning = Process.GetProcessesByName(processName).Any();
                if (!isProcessRunning)
                    return false;

                return Application.Current.Dispatcher.Invoke(() =>
                {
                    using (var automation = new UIA3Automation())
                    {
                        var windows = automation.GetDesktop().FindAllChildren();
                        foreach (var window in windows)
                        {
                            if (window.ClassName == windowClassName && window.Name.Contains(callWindowTitleKeyword))
                            {
                                // 检查窗口内是否有通话相关的特定 UI 元素
                                var callElement = window.FindFirstDescendant(cf => cf.ByName("挂断"));
                                if (callElement != null)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                });
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}