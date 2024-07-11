using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CallRecording.Models;

namespace CallRecording.Services;

public static class TrayIconService
{
    // 设置系统托盘图标和上下文菜单
    public static NotifyIcon SetupTrayIcon(Logger logger, EventHandler showAppHandler, EventHandler exitAppHandler)
    {
        var notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(logger), // 加载图标
            Visible = true,
            Text = "通话录音助手"
        };

        // 设置右键菜单
        notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        notifyIcon.ContextMenuStrip.Items.Add("显示", null, showAppHandler);
        notifyIcon.ContextMenuStrip.Items.Add("退出", null, exitAppHandler);

        // 双击托盘图标时显示应用程序
        notifyIcon.DoubleClick += showAppHandler;
        return notifyIcon;
    }

    // 清理托盘图标
    public static void CleanupTrayIcon(NotifyIcon notifyIcon)
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    // 从嵌入资源加载图标
    private static Icon LoadIcon(Logger logger)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("CallRecording.src.通用软件图片.ico"))
        {
            if (stream == null)
            {
                logger.LogMessage("无法加载图标资源。", "系统托盘图标");
                return null;
            }

            return new Icon(stream);
        }
    }
}