using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using IWshRuntimeLibrary;
using CallRecording.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using static CallRecording.Models.Recorder;
using NAudio.Wave;

namespace CallRecording.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Logger _logger;
        private readonly NotifyIcon _notifyIcon;
        private readonly Recorder _recorder;
        private WindowMonitor _windowMonitor;

        [ObservableProperty] private string _recordingSavePath;
        [ObservableProperty] public Recorder.AudioFormat _selectedFormat = Recorder.AudioFormat.MP3;

        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            _logger = new Logger(Logs);

            // 添加音频格式选项
            AudioFormats = new List<Recorder.AudioFormat>
        {
            Recorder.AudioFormat.WAV,
            Recorder.AudioFormat.MP3
        };

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
            Utils.软件启动次数add();
            _logger.LogMessage($"欢迎使用通话录音助手( ＾∀＾）／欢迎＼( ＾∀＾）", "通知");

            // 创建 Recorder 实例
            _recorder = new Recorder(_logger, _selectedFormat);
        }

        public List<Recorder.AudioFormat> AudioFormats { get; }

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
                    ConfigurationHelper.SetSetting("OutputDirectory", RecordingSavePath);
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

        // 开机自启命令
        [RelayCommand]
        private void Startup()
        {
            bool.TryParse(ConfigurationHelper.GetSetting("是否开机自启"), out bool isStartupEnabled);

            isStartupEnabled = !isStartupEnabled;
            ConfigurationHelper.SetSetting("是否开机自启", isStartupEnabled.ToString());

            string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolderPath, "CallRecording.lnk");
            string appPath = Process.GetCurrentProcess().MainModule.FileName;

            if (isStartupEnabled)
            {
                CreateShortcut(shortcutPath, appPath);
                MessageBox.Show("设置开机自启成功");
            }
            else
            {
                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                }
                MessageBox.Show("取消开机自启成功");
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.Description = "CallRecording 开机自启";
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();
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
            var targetClassNames = new List<string> { "AudioWnd" };
            var targetProcessNames = new List<string> { "WeChat" };

            string cn = ConfigurationHelper.GetSetting("监控窗口类名");
            string pn = ConfigurationHelper.GetSetting("监控窗口进程名");

            if (!string.IsNullOrEmpty(cn) && !string.IsNullOrEmpty(pn))
            {
                targetClassNames = cn.Split('|').ToList();
                targetProcessNames = pn.Split('|').ToList();
            }

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

            _logger.LogMessage($"检测到通话窗口: {title}", "系统");
            if (!_recorder.IsRecording())
            {
                _recorder.StartRecording(RecordingSavePath, "通话");
            }
        }

        // 窗口销毁事件处理
        private void OnWindowDestroyed(object sender, IntPtr hwnd)
        {
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

        partial void OnSelectedFormatChanged(AudioFormat value)
        {
            bool isRecording = false;
            if (_recorder.IsRecording())
            {
                _logger.LogMessage($"检测到正在录制,为更改音频格式即将停止录制,更改后自动继续录制", "设置更改");
                isRecording = true;
                _recorder.StopRecording();
            }

            _recorder.UpdateAudioFormat(value);
            _logger.LogMessage($"所选录制音频格式已更改为: {value}", "设置更改");

            if (isRecording)
            {
                _recorder.StartRecording(RecordingSavePath, "更改音频格式继续录制");
                _logger.LogMessage($"更改音频格式完成,正在继续录制...", "设置更改");
            }
        }
    }
}
