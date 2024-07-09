using System.Collections.ObjectModel;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace CallRecording.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Timer _monitoringTimer;
    private readonly NotifyIcon _notifyIcon;
    private readonly Recorder _recorder;
    private bool _stopMonitoring;

    public MainViewModel()
    {
        Logs = new ObservableCollection<string>();
        Logger = new Logger(Logs);
        _recorder = new Recorder(Logger);

        // 显示启动通知
        NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处关闭通知!");

        // 设置系统托盘图标
        _notifyIcon = TrayIconService.SetupTrayIcon(Logger, ShowApp, ExitApp);

        // 开始监控微信通话状态
        StartMonitoring();
    }

    public Logger Logger { get; }

    public ObservableCollection<string> Logs { get; }

    // 将日志集合转换为显示字符串
    public string LogsDisplay
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var log in Logs) sb.AppendLine(log);

            return sb.ToString();
        }
    }

    // 清除日志命令
    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        Logger.LogMessage("日志已清除。");
        OnPropertyChanged(nameof(LogsDisplay)); // 通知视图更新
    }

    // 显示应用程序窗口
    private void ShowApp(object sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            Application.Current.MainWindow?.Show();
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.Activate();
            Logger.LogMessage("应用程序窗口已显示。");
            OnPropertyChanged(nameof(LogsDisplay)); // 通知视图更新
        }));
    }

    // 退出应用程序
    public void ExitApp(object sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            Logger.LogMessage("退出应用程序。");
            TrayIconService.CleanupTrayIcon(_notifyIcon);
            Application.Current.Shutdown();
        }));
    }

    // 开始监控微信通话
    private void StartMonitoring()
    {
        Logger.LogMessage("开始监测通话录音。");
        _stopMonitoring = false;

        _monitoringTimer = new Timer(3000); // 3秒间隔
        _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
        _monitoringTimer.AutoReset = true;
        _monitoringTimer.Start();
    }

    // 定时器回调函数
    private void OnMonitoringTimerElapsed(object sender, ElapsedEventArgs e)
    {
        // 使用 Task.Run 来避免阻塞 UI 线程
        Task.Run(() =>
        {
            if (WeChatCallDetector.IsWeChatCallActive())
            {
                if (!_recorder.IsRecording())
                {
                    Logger.LogMessage("检测到微信通话，开始录音。");
                    _recorder.StartRecording();
                }
            }
            else
            {
                if (_recorder.IsRecording())
                {
                    Logger.LogMessage("通话结束，停止录音并保存文件。");
                    _recorder.StopRecording();
                }
            }

            // 通知视图更新日志显示
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { OnPropertyChanged(nameof(LogsDisplay)); }));
        });
    }
}