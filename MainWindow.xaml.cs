using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FlaUI.UIA3;
using NAudio.Wave;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace CallRecording;

public partial class MainWindow : Window
{
    private NotifyIcon _notifyIcon;
    private Thread monitoringThread;
    private Recorder recorder;
    private bool stopMonitoring;

    public MainWindow()
    {
        Loaded += OnMainWindowLoaded; // 订阅 Loaded 事件
        WindowState = WindowState.Minimized; // 启动时最小化窗口

        InitializeComponent();
        Tuopan();
        StartMonitoring();
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Tuopan()
    {
        // 初始化托盘图标
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon("src/通用软件图片.ico"),
            Visible = true,
            Text = "通话录音助手"
        };

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
    }

    // 退出应用程序
    private void ExitApp(object sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized) Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 当窗口关闭时，显示确认提示
        if (MessageBox.Show("确定要退出吗?", "确认", MessageBoxButton.YesNo) == MessageBoxResult.No)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.OnClosing(e);
    }

    private void StartMonitoring()
    {
        Console.WriteLine("开始监测通话录音...");
        recorder = new Recorder();
        stopMonitoring = false;

        monitoringThread = new Thread(MonitorCallStatus);
        monitoringThread.Start();
    }

    private void MonitorCallStatus()
    {
        try
        {
            while (!stopMonitoring)
            {
                if (WeChatCallDetector.IsWeChatCallActive())
                {
                    if (!recorder.IsRecording())
                    {
                        Console.WriteLine("检测到微信通话，开始录音...");
                        recorder.StartRecording();
                    }
                }
                else
                {
                    if (recorder.IsRecording())
                    {
                        Console.WriteLine("通话结束，停止录音并保存文件...");
                        recorder.StopRecording();
                    }
                }

                Thread.Sleep(1000); // 每1秒检查一次通话状态
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"监控过程中出现异常: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        stopMonitoring = true;

        if (monitoringThread != null && monitoringThread.IsAlive) monitoringThread.Join(); // 等待监控线程结束

        recorder?.StopRecording(); // 确保停止录音
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
    private bool isRecording;
    private string outputFileName;
    private WaveFileWriter waveFile;
    private WaveInEvent waveSource;

    public void StartRecording()
    {
        if (isRecording) return;

        outputFileName = Utils.GenerateFilename();

        waveSource = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 1) // 44100 Hz, Mono
        };

        waveSource.DataAvailable += OnDataAvailable;

        waveSource.RecordingStopped += (sender, e) =>
        {
            waveSource.Dispose();
            waveFile?.Dispose();
            Console.WriteLine($"录音已保存到: {outputFileName}");
        };

        waveFile = new WaveFileWriter(outputFileName, waveSource.WaveFormat);
        waveSource.StartRecording();
        isRecording = true;
        Console.WriteLine("开始录音...");
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (waveFile != null)
        {
            waveFile.Write(e.Buffer, 0, e.BytesRecorded);
            waveFile.Flush(); // 确保数据及时写入文件
        }
    }

    public void StopRecording()
    {
        if (!isRecording) return;

        waveSource.StopRecording();
        isRecording = false;
        Console.WriteLine("录音停止，文件已保存。");
    }

    public bool IsRecording()
    {
        return isRecording;
    }
}

public class WeChatCallDetector
{
    public static bool IsWeChatCallActive()
    {
        using (var app = new UIA3Automation())
        {
            var windows = app.GetDesktop().FindAllChildren();
            foreach (var window in windows)
                if (window.ClassName == "AudioWnd")
                    return true;
        }

        return false;
    }
}