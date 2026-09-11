using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CallRecording.Models;
using Timer = System.Windows.Forms.Timer;

namespace CallRecording.Services;

public class TrayIconManager : IDisposable
{
    private readonly Logms _logms;
    private NotifyIcon _notifyIcon;
    private Icon _defaultIcon;
    private Icon _recordingIcon;
    private Timer _iconBlinkTimer;
    private bool _isDefaultIcon = true;
    private bool _disposed = false;

    public TrayIconManager(Logms logms)
    {
        _logms = logms;
        InitializeIcons();
        InitializeBlinkTimer();
    }

    public NotifyIcon NotifyIcon => _notifyIcon;

    private void InitializeIcons()
    {
        var assembly = Assembly.GetExecutingAssembly();
        _defaultIcon = LoadEmbeddedIcon(assembly, "CallRecording.src.通用软件图片.ico");
        _recordingIcon = LoadEmbeddedIcon(assembly, "CallRecording.src.通用软件图片闪动.ico");
    }

    private Icon LoadEmbeddedIcon(Assembly assembly, string resourceName)
    {
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                _logms?.LogMessage($"无法加载图标资源: {resourceName}", "系统托盘图标");
                return null;
            }

            return new Icon(stream);
        }
    }

    private void InitializeBlinkTimer()
    {
        _iconBlinkTimer = new Timer
        {
            Interval = 500
        };
        _iconBlinkTimer.Tick += IconBlinkTimer_Tick;
    }

    public void SetupTrayIcon(bool isStealth, EventHandler showAppHandler, EventHandler exitAppHandler)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = _defaultIcon,
            Visible = !isStealth,
            Text = "通话录音助手"
        };

        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("显示", null, showAppHandler);
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, exitAppHandler);
        _notifyIcon.DoubleClick += showAppHandler;
    }

    public void StartBlinking()
    {
        if (_iconBlinkTimer != null && !_iconBlinkTimer.Enabled)
        {
            _iconBlinkTimer.Start();
        }
    }

    public void StopBlinking()
    {
        if (_iconBlinkTimer != null && _iconBlinkTimer.Enabled)
        {
            _iconBlinkTimer.Stop();
            if (_notifyIcon != null && _defaultIcon != null)
            {
                _notifyIcon.Icon = _defaultIcon;
            }
        }
    }

    private void IconBlinkTimer_Tick(object? sender, EventArgs e)
    {
        if (_notifyIcon == null) return;

        if (_isDefaultIcon)
        {
            _notifyIcon.Icon = _recordingIcon;
        }
        else
        {
            _notifyIcon.Icon = _defaultIcon;
        }

        _isDefaultIcon = !_isDefaultIcon;
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(2000, title, message, ToolTipIcon.Info);
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
            _iconBlinkTimer?.Stop();
            _iconBlinkTimer?.Dispose();
            _iconBlinkTimer = null;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _defaultIcon?.Dispose();
            _defaultIcon = null;

            _recordingIcon?.Dispose();
            _recordingIcon = null;
        }

        _disposed = true;
    }

    ~TrayIconManager()
    {
        Dispose(false);
    }
}