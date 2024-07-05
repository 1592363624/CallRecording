using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using FlaUI.UIA3;
using NAudio.Wave;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Timer = System.Timers.Timer;

namespace CallRecording;

public partial class MainWindow : Window
{
    public Logger _logger;
    private NotifyIcon _notifyIcon;
    private Timer monitoringTimer;
    private Recorder recorder;
    private bool stopMonitoring;

    public MainWindow()
    {
        Loaded += OnMainWindowLoaded;
        WindowState = WindowState.Minimized; // 启动时最小化窗口
        InitializeComponent();
        _logger = new Logger(LogTextBox);
        SetupTrayIcon();
        StartMonitoring();
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void SetupTrayIcon()
    {
        // 从嵌入资源中加载图标
        var assembly = Assembly.GetExecutingAssembly();
        using (var iconStream = assembly.GetManifestResourceStream("CallRecording.src.通用软件图片.ico"))
        {
            if (iconStream == null)
            {
                // 如果图标流为空，记录日志
                _logger.LogMessage("无法加载图标，图标流为空。");
                return;
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon(iconStream), // 从流中加载图标
                Visible = true,
                Text = "通话录音助手"
            };
        }

        // 添加托盘图标的右键菜单
        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("显示", null, ShowApp);
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, ExitApp);

        // 在托盘图标双击时显示窗口
        _notifyIcon.DoubleClick += (s, e) => ShowApp(null, null);
    }


    // 显示应用程序
    private void ShowApp(object sender, EventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _logger.LogMessage("应用程序窗口已显示。");
    }

    // 退出应用程序
    private void ExitApp(object sender, EventArgs e)
    {
        _logger.LogMessage("退出应用程序。");
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _logger.LogMessage("应用程序窗口已最小化。");
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 当窗口关闭时，显示确认提示
        if (MessageBox.Show("确定要退出吗? 可以最小化在托盘默默地监测哟!", "确认", MessageBoxButton.YesNo) == MessageBoxResult.No)
        {
            e.Cancel = true;
            Hide();
            _logger.LogMessage("用户取消了关闭操作，窗口已最小化。");
        }
        else
        {
            _logger.LogMessage("用户确认退出应用程序。");
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.OnClosing(e);
    }

    private void StartMonitoring()
    {
        _logger.LogMessage("开始监测通话录音。");
        recorder = new Recorder(_logger);
        stopMonitoring = false;

        // 初始化定时器
        monitoringTimer = new Timer(3000);
        monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
        monitoringTimer.AutoReset = true;
        monitoringTimer.Start();
    }

    private void OnMonitoringTimerElapsed(object sender, ElapsedEventArgs e)
    {
        // 在定时器回调中检查微信通话状态
        if (WeChatCallDetector.IsWeChatCallActive())
        {
            if (!recorder.IsRecording())
            {
                _logger.LogMessage("检测到微信通话，开始录音。");
                recorder.StartRecording();
            }
        }
        else
        {
            if (recorder.IsRecording())
            {
                _logger.LogMessage("通话结束，停止录音并保存文件。");
                recorder.StopRecording();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        stopMonitoring = true;

        // 停止定时器
        if (monitoringTimer != null)
        {
            monitoringTimer.Stop();
            monitoringTimer.Dispose();
            _logger.LogMessage("监控定时器已停止。");
        }

        recorder?.StopRecording(); // 确保停止录音
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.ClearLogs();
        _logger.LogMessage("日志已清除。");
    }
}

public class Utils
{
    // 获取当前时间并格式化
    public static string GetFormattedTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    // 获取桌面路径
    public static string GetDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    // 动态生成文件名
    public static string GenerateFilename()
    {
        return Path.Combine(GetDesktopPath(), $"{GetFormattedTime()}_wechat_call_recording.wav");
    }
}

public class Recorder
{
    private readonly Logger _logger;
    private readonly object lockObject = new();
    private bool isRecording;
    private string outputFileName;
    private WaveFileWriter waveFile;
    private WaveInEvent waveSource;

    public Recorder(Logger logger)
    {
        _logger = logger;
    }

    public void StartRecording()
    {
        lock (lockObject)
        {
            if (isRecording) return;

            outputFileName = Utils.GenerateFilename();

            waveSource = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1) // 44100 Hz, Mono
            };

            waveSource.DataAvailable += OnDataAvailable;
            waveSource.RecordingStopped += OnRecordingStopped;

            waveFile = new WaveFileWriter(outputFileName, waveSource.WaveFormat);
            waveSource.StartRecording();
            isRecording = true;
            _logger.LogMessage("开始录音...");
        }
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        lock (lockObject)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush(); // 确保数据及时写入文件
            }
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        lock (lockObject)
        {
            try
            {
                // 在停止录音时，确保释放资源
                waveSource.Dispose();
                waveFile?.Dispose();
                waveSource = null;
                waveFile = null;

                if (e.Exception != null)
                    _logger.LogMessage($"录音停止时发生异常: {e.Exception.Message}");
                else
                    _logger.LogMessage($"录音已保存到: {outputFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"在处理录音停止事件时发生异常: {ex.Message}");
            }
        }
    }

    public void StopRecording()
    {
        lock (lockObject)
        {
            if (!isRecording) return;

            try
            {
                waveSource.StopRecording();
                _logger.LogMessage("录音停止，文件已保存。");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"停止录音时发生异常: {ex.Message}");
            }

            isRecording = false;
        }
    }

    public bool IsRecording()
    {
        lock (lockObject)
        {
            return isRecording;
        }
    }
}

public class WeChatCallDetector
{
    public static bool IsWeChatCallActive()
    {
        try
        {
            // 使用 Dispatcher 在 UI 线程上执行操作
            return Application.Current.Dispatcher.Invoke(() =>
            {
                using (var app = new UIA3Automation())
                {
                    var windows = app.GetDesktop().FindAllChildren();
                    foreach (var window in windows)
                        if (window.ClassName == "AudioWnd")
                            return true;
                }

                return false;
            });
        }
        catch (Exception ex)
        {
            // 记录异常信息，方便调试
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 使用主线程的日志记录方法
                var logger = (Application.Current.MainWindow as MainWindow)?._logger;
                logger?.LogMessage($"微信通话检测时发生异常: {ex.Message}");
            });
            return false;
        }
    }
}

public class Logger
{
    private readonly TextBox _logTextBox;

    public Logger(TextBox logTextBox)
    {
        _logTextBox = logTextBox;
    }

    public void LogMessage(string message)
    {
        // 显式使用 Action 委托，确保使用合适的重载
        Application.Current.Dispatcher.Invoke(() =>
        {
            _logTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
            _logTextBox.ScrollToEnd();
        });
    }

    public void ClearLogs()
    {
        // 显式使用 Action 委托，确保使用合适的重载
        Application.Current.Dispatcher.Invoke(() => { _logTextBox.Clear(); });
    }
}