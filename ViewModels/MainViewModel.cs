using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using Timer = System.Windows.Forms.Timer;
using System.Drawing;
using System.Reflection;

namespace CallRecording.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Logger _logger;
        private NotifyIcon _notifyIcon;
        private readonly Recorder _recorder;
        private WindowMonitor _windowMonitor;
        private Timer _iconBlinkTimer;
        private Icon _defaultIcon;
        private Icon _recordingIcon;
        private bool _isDefaultIcon = true;
        [ObservableProperty]
        public string totalSize;
        [ObservableProperty]
        public string availableFreeSpace;
        [ObservableProperty]
        public string usedSpace;
        [ObservableProperty]
        public string iusedSpace;


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
            //RecordingSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");
            RecordingSavePath = AppDomain.CurrentDomain.BaseDirectory + "Recordings";
            // 确保目录存在
            if (!Directory.Exists(RecordingSavePath))
            {
                Directory.CreateDirectory(RecordingSavePath);
                ConfigurationHelper.SetSetting("OutputDirectory", RecordingSavePath);
            }
            //读取更改的保存路径
            RecordingSavePath = ConfigurationHelper.GetSetting("OutputDirectory");

            // 显示启动通知
            NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处关闭通知!");

            // 设置系统托盘图标
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);

            _notifyIcon = TrayIconService.SetupTrayIcon(_logger, !isStealth, ShowApp, ExitApp);

            // 初始化托盘图标
            _defaultIcon = _notifyIcon.Icon; // 假设初始图标已经在_setupTrayIcon中设置
            var assembly = Assembly.GetExecutingAssembly();
            _recordingIcon = new Icon(assembly.GetManifestResourceStream("CallRecording.src.通用软件图片闪动.ico")); // 替换成你的录音中图标路径

            // 初始化定时器，间隔500毫秒（闪烁频率）
            _iconBlinkTimer = new Timer
            {
                Interval = 500 // 500毫秒切换一次图标
            };
            _iconBlinkTimer.Tick += IconBlinkTimer_Tick;
            //_iconBlinkTimer.Start();

            // 初始化窗口监控
            InitializeWindowMonitor();
            Utils.软件启动次数add();
            _logger.LogMessage($"欢迎使用通话录音助手( ＾∀＾）／欢迎＼( ＾∀＾）", "通知");

            // 创建 Recorder 实例
            _recorder = new Recorder(_logger, _selectedFormat);

            //读取磁盘占用相关信息
            Task.Run(() =>
            {
                var path = ConfigurationHelper.GetSetting("OutputDirectory");
                var DiskInfoIn = Utils.GetDiskInfoInMB(path);


                TotalSize = "磁盘总空间: " + Utils.FormatSize(DiskInfoIn.总大小);
                AvailableFreeSpace = "磁盘可用空间: " + Utils.FormatSize(DiskInfoIn.可用空间);
                UsedSpace = "磁盘已用空间: " + Utils.FormatSize(DiskInfoIn.已用空间);
                IusedSpace = "录音文件占用空间: " + Utils.FormatSize(Utils.GetFolderSize(path));
            });
        }

        private void IconBlinkTimer_Tick(object? sender, EventArgs e)
        {
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

        // 添加监控窗口命令
        [RelayCommand]
        private void AddMo()
        {

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

        // 隐身模式命令
        [RelayCommand]
        private void Stealth()
        {
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);

            isStealth = !isStealth;
            ConfigurationHelper.SetSetting("是否隐身模式启动", isStealth.ToString());

            if (isStealth)
            {
                MessageBox.Show("设置隐身模式启动成功,重启软件即可完全隐藏软件,不会显示系统托盘图标");
            }
            else
            {
                MessageBox.Show("取消隐身模式启动成功,下次启动将会显示系统托盘图标");
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
                _recorder.StartRecording(RecordingSavePath, "通话");//开始录音
                _iconBlinkTimer.Start();//通话录音的时候图标闪烁
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
                _logger.LogMessage("通话结束，停止录音并保存文件。", "系统");//停止录音
                _recorder.StopRecording();
                _iconBlinkTimer.Stop(); // 停止图标闪烁
                _notifyIcon.Icon = _defaultIcon; // 恢复为默认图标

                Task.Run(() =>
                {
                    var path = AppDomain.CurrentDomain.BaseDirectory + ConfigurationHelper.GetSetting("OutputDirectory");
                    var DiskInfoIn = Utils.GetDiskInfoInMB(path);


                    TotalSize = Utils.FormatSize(DiskInfoIn.总大小);
                    AvailableFreeSpace = Utils.FormatSize(DiskInfoIn.可用空间);
                    UsedSpace = Utils.FormatSize(DiskInfoIn.已用空间);
                    IusedSpace = Utils.FormatSize(Utils.GetFolderSize(path));
                });
            }
        }

        partial void OnSelectedFormatChanged(AudioFormat value)
        {
            if (_recorder.IsRecording())
            {
                MessageBoxResult result = MessageBox.Show("检测到正在录制,为更改音频格式需要停止录制,是否继续更换音频格式", "设置更改", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    StopRecording();
                    _logger.LogMessage($"所选录制音频格式已更改为: {value}", "用户确认更改音频格式");

                }
                else if (result == MessageBoxResult.Cancel)
                {
                    _logger.LogMessage($"用户已取消更改所选录制音频格式", "用户取消更改音频格式");
                }

            }

            _recorder.UpdateAudioFormat(value);
            _logger.LogMessage($"所选录制音频格式已更改为: {value}", "设置更改");

        }
    }
}
