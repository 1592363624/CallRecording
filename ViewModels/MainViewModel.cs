using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Application = System.Windows.Application;

namespace CallRecording.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource _cancellationTokenSource;
    private readonly Logger _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly Recorder _recorder;

    [ObservableProperty] private string _recordingSavePath;

    public MainViewModel()
    {
        Logs = new ObservableCollection<string>();
        _logger = new Logger(Logs);
        _recorder = new Recorder(_logger);

        // 默认保存路径为用户的文档文件夹
        RecordingSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Recordings");

        // 显示启动通知
        NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处关闭通知!");

        // 设置系统托盘图标
        _notifyIcon = TrayIconService.SetupTrayIcon(_logger, ShowApp, ExitApp);

        // 开始监控微信通话状态
        StartMonitoring();
    }

    public ObservableCollection<string> Logs { get; }

    // 选择保存路径命令
    [RelayCommand]
    private void ChooseSavePath()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "选择录音文件保存位置";
            dialog.SelectedPath = RecordingSavePath;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                RecordingSavePath = dialog.SelectedPath;
                _logger.LogMessage($"录音文件保存位置已设置为: {RecordingSavePath}");
            }
        }
    }

    // 清除日志命令
    [RelayCommand]
    private void ClearLogs()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Logs.Clear();
            _logger.LogMessage("日志已清除。");
        });
    }

    // 显示应用程序窗口
    private void ShowApp(object sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.MainWindow?.Show();
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.Activate();
            _logger.LogMessage("应用程序窗口已显示。");
        });
    }

    // 退出应用程序
    public void ExitApp(object sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _logger.LogMessage("退出应用程序。");
            TrayIconService.CleanupTrayIcon(_notifyIcon);
            Application.Current.Shutdown();
        });
    }

    // 开始监控微信通话
    private void StartMonitoring()
    {
        _logger.LogMessage("开始监测通话录音。");
        _cancellationTokenSource = new CancellationTokenSource();
        Task.Run(async () => await MonitorWeChatCallStatus(_cancellationTokenSource.Token));
    }

    // 异步监控微信通话状态
    private async Task MonitorWeChatCallStatus(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var isWeChatCallActive = await Task.Run(() => WeChatCallDetector.IsWeChatCallActive());

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (isWeChatCallActive)
                {
                    if (!_recorder.IsRecording())
                    {
                        _logger.LogMessage("检测到微信通话，开始录音。");
                        _recorder.StartRecording(RecordingSavePath);
                    }
                }
                else
                {
                    if (_recorder.IsRecording())
                    {
                        _logger.LogMessage("通话结束，停止录音并保存文件。");
                        _recorder.StopRecording();
                    }
                }
            });

            // 等待3秒再进行下一次检测
            await Task.Delay(3000, cancellationToken);
        }
    }
}