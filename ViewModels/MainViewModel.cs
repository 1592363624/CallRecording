using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace CallRecording.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Logger _logger;
        private readonly NotifyIcon _notifyIcon;
        private readonly Recorder _recorder;
        private WindowMonitor _windowMonitor;

        [ObservableProperty] private string _recordingSavePath;

        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            _logger = new Logger(Logs);
            _recorder = new Recorder(_logger);

            // 默认保存路径为软件的运行目录
            RecordingSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");

            // 确保目录存在
            if (!Directory.Exists(RecordingSavePath))
            {
                Directory.CreateDirectory(RecordingSavePath);
            }

            // 显示启动通知
            NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处关闭通知!");

            // 设置系统托盘图标
            _notifyIcon = TrayIconService.SetupTrayIcon(_logger, ShowApp, ExitApp);

            // 初始化窗口监控
            InitializeWindowMonitor();
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
                    _logger.LogMessage($"录音文件保存位置已设置为: {RecordingSavePath}", "设置");
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
                _logger.LogMessage("日志已清除。", "设置");
            });
        }

        //开机自启命令
        [RelayCommand]
        private void Startup(bool isStartup)
        {
            string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            string appName = "CallRecording";
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
            {
                if (key == null)
                {
                    MessageBox.Show("无法访问注册表。");
                    return;
                }

                try
                {
                    if (isStartup)
                    {
                        key.SetValue(appName, appPath);
                        MessageBox.Show("设置开机自启成功");
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"设置开机自启失败：{ex.Message}");
                }
            }
        }

        // 显示应用程序窗口
        private void ShowApp(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow?.Show();
                Application.Current.MainWindow.WindowState = WindowState.Normal;
                Application.Current.MainWindow.Activate();
                //_logger.LogMessage("应用程序窗口已显示。", "系统");
            });
        }

        // 退出应用程序
        public void ExitApp(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logger.LogMessage("退出应用程序。", "系统");
                TrayIconService.CleanupTrayIcon(_notifyIcon);
                _windowMonitor.Dispose();
                Application.Current.Shutdown();
            });
        }

        // 初始化窗口监控
        private void InitializeWindowMonitor()
        {
            //var targetClassNames = new List<string> { "AudioWnd", "语音通话" }; // 替换为实际的目标窗口类名
            //var targetProcessNames = new List<string> { "WeChat", "Chrome_WidgetWin_1" }; // 替换为实际的目标进程名
            var targetClassNames = new List<string> { "AudioWnd" }; // 替换为实际的目标窗口类名
            var targetProcessNames = new List<string> { "WeChat" }; // 替换为实际的目标进程名


            _windowMonitor = new WindowMonitor(targetClassNames, targetProcessNames);
            _windowMonitor.WindowCreated += OnWindowCreated;
            _windowMonitor.WindowDestroyed += OnWindowDestroyed;
        }

        // 窗口创建事件处理
        private void OnWindowCreated(object sender, IntPtr hwnd)
        {
            StringBuilder className = new StringBuilder(256);
            WindowMonitor.GetClassName(hwnd, className, className.Capacity);

            WindowMonitor.GetWindowThreadProcessId(hwnd, out uint processId);
            Process process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            string title = process.MainWindowTitle;

            //_logger.LogMessage($"检测到新窗口: 标题: {title}, 类名: {className}, 句柄: {hwnd}", "系统");

            // 处理新创建的窗口
            //todo 这里要改一下,也许可以删掉
            //if (title.Contains("语音通话") || title.Contains("微信通话"))
            //{
            _logger.LogMessage($"检测到通话窗口: {title}", "系统");
            if (!_recorder.IsRecording())
            {
                _recorder.StartRecording(RecordingSavePath, "通话");
            }
            //}
        }

        // 窗口销毁事件处理
        private void OnWindowDestroyed(object sender, IntPtr hwnd)
        {
            //_logger.LogMessage($"窗口销毁: 句柄: {hwnd}", "系统");

            // 停止录音
            StopRecording();
        }

        // 停止录音
        public void StopRecording()
        {
            if (_recorder.IsRecording())
            {
                _logger.LogMessage("通话结束，停止录音并保存文件。", "系统");
                _recorder.StopRecording();
            }
        }
    }
}
