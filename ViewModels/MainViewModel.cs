using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Application = System.Windows.Application;

namespace CallRecording.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly string _configFilePath;
    private readonly Logger _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly Recorder _recorder;
    private CancellationTokenSource _cancellationTokenSource;

    [ObservableProperty] private string _recordingSavePath;

    public MainViewModel()
    {
        Logs = new ObservableCollection<string>();
        _logger = new Logger(Logs);
        _recorder = new Recorder(_logger);

        // 配置文件路径
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // 读取 appsettings.json 文件中的配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .Build();

        // 获取保存路径配置并进行检查
        var outputDirectory = configuration["OutputDirectory"];
        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentNullException(nameof(outputDirectory), "OutputDirectory 配置不能为空");

        // 获取绝对路径
        RecordingSavePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDirectory));

        // 检查目录是否存在，如果不存在则创建
        if (!Directory.Exists(RecordingSavePath))
        {
            Directory.CreateDirectory(RecordingSavePath);
            _logger.LogMessage($"录音文件保存路径不存在，已创建目录: {RecordingSavePath}");
        }

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

                // 更新配置文件
                UpdateConfiguration("OutputDirectory", RecordingSavePath);
            }
        }
    }

    private void UpdateConfiguration(string key, string value)
    {
        var jsonConfig = File.ReadAllText(_configFilePath);
        dynamic jsonObj = JsonConvert.DeserializeObject(jsonConfig);

        jsonObj[key] = value;

        string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
        File.WriteAllText(_configFilePath, output);
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