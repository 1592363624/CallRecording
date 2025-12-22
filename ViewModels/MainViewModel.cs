using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CallRecording.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IWshRuntimeLibrary;
using MySharedProject;
using MySharedProject.Model;
using NLog;
using static CallRecording.Models.Recorder;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Windows.Forms.Timer;

namespace CallRecording.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDpiForWindow(IntPtr hWnd);


        private static Logms _logms;
        private readonly Recorder _recorder;
        private Icon _defaultIcon;
        private Timer _iconBlinkTimer;
        private bool _isDefaultIcon = true;
        private NotifyIcon _notifyIcon;
        private Icon _recordingIcon;

        private Keys _currentHotkey = Keys.F9;
        private Keys _currentStopHotkey = Keys.End; // 添加结束热键

        [ObservableProperty] private string _recordingSavePath;
        [ObservableProperty] public AudioFormat _selectedFormat;
        private WindowMonitor _windowMonitor;

        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            _logms = new Logms(Logs);

            // 读取并应用日志等级配置
            try 
            {
                string logLevelStr = ConfigurationHelper.GetSetting("LogLevel");
                if (!string.IsNullOrEmpty(logLevelStr) && logLevelStr != "NULL")
                {
                    SelectedLogLevel = LogLevel.FromString(logLevelStr);
                }
                else
                {
                    // 默认关闭日志
                    SelectedLogLevel = LogLevel.Off;
                }
            }
            catch
            {
                SelectedLogLevel = LogLevel.Off;
            }
            
            // 强制应用日志等级（解决NLog默认Info的问题）
            Utils.SetGlobalLogLevel(SelectedLogLevel);

            // 添加音频格式选项
            AudioFormats = new List<AudioFormat>
            {
                AudioFormat.MP3,
                AudioFormat.WAV
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
            //如果不是绝对目录就设置为绝对目录

            string? pt = Path.GetPathRoot(RecordingSavePath);
            if (pt == "")
            {
                ConfigurationHelper.SetSetting("OutputDirectory", AppDomain.CurrentDomain.BaseDirectory + "Recordings");
            }

            try
            {
                // 显示启动通知
                NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处可提前关闭通知!");
            }
            catch (Exception ex)
            {
                _logms.LogMessage($"启动通知发送失败: {ex.Message}", "警告(不影响使用)");
            }

            // 设置系统托盘图标
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);

            _notifyIcon = TrayIconService.SetupTrayIcon(_logms, !isStealth, ShowApp, ExitApp);

            // 初始化托盘图标
            _defaultIcon = _notifyIcon.Icon; // 假设初始图标已经在_setupTrayIcon中设置
            var assembly = Assembly.GetExecutingAssembly();
            _recordingIcon =
                new Icon(assembly.GetManifestResourceStream("CallRecording.src.通用软件图片闪动.ico")); // 替换成你的录音中图标路径

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
            _logms.LogMessage($"欢迎使用通话录音助手( ＾∀＾）／欢迎＼( ＾∀＾）", "通知");

            // 创建 Recorder 实例
            _recorder = new Recorder(_logms, _selectedFormat);

            //读取最后使用的音频格式
            Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedFormat = ConfigurationHelper.GetSetting("音频格式") == "MP3"
                    ? AudioFormat.MP3
                    : AudioFormat.WAV;
            });

            //读取磁盘占用相关信息
            DataSource.gbmvvm.GetDiskInFo();

            // 读取启停录音热键设置
            string startHotkeyStr = ConfigurationHelper.GetSetting("录音快捷键");
            if (!string.IsNullOrEmpty(startHotkeyStr) && Enum.TryParse<Keys>(startHotkeyStr, out Keys startKey))
            {
                _currentHotkey = startKey;
            }

            // 初始化时注册默认快捷键
            GlobalHotkey.RegisterHotkey(_currentHotkey);
            GlobalHotkey.OnHotkeyPressed += ToggleRecording; // 启停热键事件处理
            GlobalHotkey.OnStopHotkeyPressed += StopRecordingHotkey; // 停止热键事件处理

            // 读取自定义结束热键设置
            string stopHotkeyStr = ConfigurationHelper.GetSetting("结束录音快捷键");
            if (!string.IsNullOrEmpty(stopHotkeyStr) && Enum.TryParse<Keys>(stopHotkeyStr, out Keys stopKey))
            {
                _currentStopHotkey = stopKey;
                GlobalHotkey.SetCustomStopHotkey(_currentStopHotkey);
            }
        }

        private void OnRecorderStopped(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_iconBlinkTimer.Enabled)
                {
                    _iconBlinkTimer.Stop();
                    _notifyIcon.Icon = _defaultIcon;
                    // 如果是异常停止，可能需要补一条日志，但 Recorder 内部已经有了
                }
                
                // 刷新磁盘信息
                DataSource.gbmvvm.GetDiskInFo();
            });
        }

        // 停止录音热键处理函数
        private void StopRecordingHotkey()
        {
            if (_recorder.IsRecording())
            {
                _iconBlinkTimer.Stop(); //通话录音的时候图标闪烁
                StopRecording();
            }
        }

        public void SetHotkey(Keys hotkey)
        {
            // 注销当前快捷键
            GlobalHotkey.UnregisterHotkey();

            // 尝试注册新快捷键
            bool success = GlobalHotkey.RegisterHotkey(hotkey);
            if (success)
            {
                _currentHotkey = hotkey;
                ConfigurationHelper.SetSetting("录音快捷键", hotkey.ToString());
            }
            else
            {
                // 如果冲突，恢复之前的快捷键
                GlobalHotkey.RegisterHotkey(_currentHotkey);
                ConfigurationHelper.SetSetting("录音快捷键", _currentHotkey.ToString());
            }
        }

        // 新增方法：设置结束热键
        public void SetStopHotkey(Keys hotkey)
        {
            bool success = GlobalHotkey.SetCustomStopHotkey(hotkey);
            if (success)
            {
                _currentStopHotkey = hotkey;
                ConfigurationHelper.SetSetting("结束录音快捷键", hotkey.ToString());
            }
            else
            {
                // 如果冲突，恢复之前的快捷键
                GlobalHotkey.SetCustomStopHotkey(_currentStopHotkey);
                ConfigurationHelper.SetSetting("结束录音快捷键", _currentStopHotkey.ToString());
            }
        }

        private void ToggleRecording()
        {
            if (_recorder.IsRecording())
            {
                if (_recorder.IsPaused())
                {
                    // 如果当前是暂停状态，则恢复录音
                    _iconBlinkTimer.Start(); //通话录音的时候图标闪烁
                    ResumeRecording();
                }
                else
                {
                    // 如果当前是录音状态，则暂停录音
                    _iconBlinkTimer.Stop(); //通话录音的时候图标闪烁
                    PauseRecording();
                }
            }
            else
            {
                // 如果没有在录音，则开始录音
                _iconBlinkTimer.Start(); //通话录音的时候图标闪烁
                StartRecording();
            }
        }

        public void StartRecording()
        {
            if (!_recorder.IsRecording())
            {
                _recorder.StartRecording(RecordingSavePath, "通话"); //开始录音
                _iconBlinkTimer.Start(); //通话录音的时候图标闪烁
            }
        }

        public void PauseRecording()
        {
            if (_recorder.IsRecording() && !_recorder.IsPaused())
            {
                _recorder.PauseRecording();
                _iconBlinkTimer.Stop();
            }
        }

        public void ResumeRecording()
        {
            if (_recorder.IsRecording() && _recorder.IsPaused())
            {
                _recorder.ResumeRecording();
                _iconBlinkTimer.Start(); //通话录音的时候图标闪烁
            }
        }

        ~MainViewModel()
        {
            // 注销全局快捷键
            GlobalHotkey.UnregisterHotkey();
        }

        public List<AudioFormat> AudioFormats { get; }

        public ObservableCollection<LogLevel> AvailableLogLevels { get; } =
            new ObservableCollection<LogLevel>
            {
                LogLevel.Off,
                LogLevel.Info,
                LogLevel.Debug
            };

        [ObservableProperty] private LogLevel selectedLogLevel = LogLevel.Off;

        partial void OnSelectedLogLevelChanged(LogLevel value)
        {
            Utils.SetGlobalLogLevel(value);
            ConfigurationHelper.SetSetting("LogLevel", value.Name);
            _logms.LogMessage($"日志等级已切换,当前日志等级: " + value, "系统设置");
        }

        public ObservableCollection<string> Logs { get; }

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

        // 打开音频管理器命令
        [RelayCommand]
        private void OpenAudioManager()
        {
            var managerWindow = new AudioManagerWindow();
            var managerViewModel = new AudioManagerViewModel(_logms);
            managerWindow.DataContext = managerViewModel;
            managerWindow.Show();
        }

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
                    _logms.LogMessage($"录音文件保存位置已设置为: {RecordingSavePath}", "设置");
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
                _logms.LogMessage("日志已清除。", "设置");
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
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
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
            WshShell shell = null;
            IWshShortcut shortcut = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.Description = "CallRecording 开机自启";
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
            }
            catch (Exception ex)
            {
                _logms?.LogMessage($"创建快捷方式失败: {ex.Message}", "错误");
            }
            finally
            {
                // 显式释放COM资源
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
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
                //_logms.LogMessage("应用程序窗口已显示。", "系统");
            });
        }

        // 退出应用程序
        public void ExitApp(object sender, EventArgs e)
        {
            GlobalHotkey.UnregisterHotkey();
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logms.LogMessage("退出应用程序。", "系统");
                TrayIconService.CleanupTrayIcon(_notifyIcon);
                _windowMonitor.Dispose();
                Application.Current.Shutdown();
            });
        }

        // 初始化窗口监控
        private void InitializeWindowMonitor()
        {
            var targetClassNames = new List<string> { "AudioWnd|WXworkWindow|Qt51514QWindowIcon" };
            var targetProcessNames = new List<string> { "WeChat|WXWork|Weixin" };
            var targetTitles = new List<string> { "语音" };
            // GlobalMVVM gmvvm = new GlobalMVVM();
            DataSource.gbmvvm.Cn = ConfigurationHelper.GetSetting("监控窗口类名");
            DataSource.gbmvvm.Pn = ConfigurationHelper.GetSetting("监控窗口进程名");
            DataSource.gbmvvm.Tt = ConfigurationHelper.GetSetting("监控窗口标题");

            if (!string.IsNullOrEmpty(DataSource.gbmvvm.Cn) && !string.IsNullOrEmpty(DataSource.gbmvvm.Pn) &&
                !string.IsNullOrEmpty(DataSource.gbmvvm.Tt))
            {
                targetClassNames = DataSource.gbmvvm.Cn.Split('|').ToList();
                targetProcessNames = DataSource.gbmvvm.Pn.Split('|').ToList();
                targetTitles = DataSource.gbmvvm.Tt.Split('|').ToList();
            }
            else
            {
                ConfigurationHelper.SetSetting("监控窗口标题", "语音"); //添加默认监控窗口标题,禁止为空
                DataSource.gbmvvm.Tt = ConfigurationHelper.GetSetting("监控窗口标题");
                targetTitles = [DataSource.gbmvvm.Tt];
            }

            _windowMonitor = new WindowMonitor(targetClassNames, targetProcessNames, targetTitles);
            _windowMonitor.WindowCreated += OnWindowCreated;
            _windowMonitor.WindowDestroyed += OnWindowDestroyed;
        }


        // 窗口创建事件处理
        private void OnWindowCreated(object sender, IntPtr hwnd)
        {
            // 获取窗口类名
            StringBuilder className = new StringBuilder(256);
            WindowMonitor.GetClassName(hwnd, className, className.Capacity);

            // 获取窗口所属进程
            WindowMonitor.GetWindowThreadProcessId(hwnd, out uint processId);
            Process process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            string title = process.MainWindowTitle;

            // 软件适配微调
            if (processName == "QQ") // QQNT
            {
                if (title != "语音通话")
                {
                    Debug.WriteLine($"检测到QQNT窗口: {title}, 不是语音通话窗口, 不录音");
                    return;
                }
            }

            int width = 0;
            int height = 0;
            if (processName == "Weixin") // 微信测试版
            {
                RECT clientRect;
                if (GetClientRect(hwnd, out clientRect))
                {
                    int clientWidth = clientRect.Right - clientRect.Left;
                    int clientHeight = clientRect.Bottom - clientRect.Top;

                    // 自动调整竖屏/横屏逻辑：长边作为高度，短边作为宽度
                    width = Math.Min(clientWidth, clientHeight);
                    height = Math.Max(clientWidth, clientHeight);

                    logger.Info($"窗口: {title}, 客户区尺寸（Weixin）: {width}x{height}");
                    Debug.WriteLine($"窗口: {title}, 客户区尺寸（竖屏逻辑）: {width}x{height}");
                }

                //if (!((width == 360 && height == 640) || (width == 640 && height == 480)))
                //{
                //    Debug.WriteLine($"检测到微信窗口: {title}, 尺寸不符合, 不录音");
                //    return;
                //}

                // 检查是否启用窗口大小检测
                bool.TryParse(ConfigurationHelper.GetSetting("是否启用微信窗口大小检测"), out bool isCheckSize);
                if (isCheckSize)
                {
                    int w = int.Parse(ConfigurationHelper.GetSetting("微信通话窗口宽度"));
                    int h = int.Parse(ConfigurationHelper.GetSetting("微信通话窗口高度"));
                    if (width != w && height != h)
                    {
                        Debug.WriteLine($"检测到微信窗口: {title}, 尺寸不符合, 不录音");
                        //_logms.LogMessage($"检测到微信窗口: {title}, 宽高: {width}x{height}", "系统");
                        logger.Info($"检测到微信窗口: {title}, 宽高: {width}x{height}");
                        return;
                    }
                }
            }

            // 通过这里判定为通话窗口
            Debug.WriteLine($"检测到通话窗口: {title}，宽高: {width}x{height}");
            _logms.LogMessage($"检测到通话窗口: {title}", "系统");

            // 开始录音
            if (!_recorder.IsRecording())
            {
                _recorder.StartRecording(RecordingSavePath, processName + "_" + title); // 开始录音
                _iconBlinkTimer.Start(); // 通话录音时图标闪烁
            }
        }

        // Win32 DPI 相关函数
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);


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
                _logms.LogMessage("通话结束，停止录音并保存文件。", "系统"); //停止录音
                _recorder.StopRecording();
                _iconBlinkTimer.Stop(); // 停止图标闪烁
                _notifyIcon.Icon = _defaultIcon; // 恢复为默认图标

                //读取磁盘占用相关信息

                DataSource.gbmvvm.GetDiskInFo();

                //Task.Run(() =>
                //{
                //    var path = AppDomain.CurrentDomain.BaseDirectory + ConfigurationHelper.GetSetting("OutputDirectory");
                //    var DiskInfoIn = Utils.GetDiskInfoInMB(path);

                //    TotalSize = Utils.FormatSize(DiskInfoIn.总大小);
                //    AvailableFreeSpace = Utils.FormatSize(DiskInfoIn.可用空间);
                //    UsedSpace = Utils.FormatSize(DiskInfoIn.已用空间);
                //    IusedSpace = Utils.FormatSize(Utils.GetRecSize(path));
                //});
            }
        }

        partial void OnSelectedFormatChanged(AudioFormat value)
        {
            if (_recorder.IsRecording())
            {
                MessageBoxResult result =
                    MessageBox.Show("检测到正在录制,为更改音频格式需要停止录制,是否继续更换音频格式", "设置更改", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    StopRecording();
                    _logms.LogMessage($"所选录制音频格式已更改为: {value}", "用户确认更改音频格式");
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    _logms.LogMessage($"用户已取消更改所选录制音频格式", "用户取消更改音频格式");
                }
            }

            _recorder.UpdateAudioFormat(value);
            _logms.LogMessage($"所选录制音频格式已更改为: {value}", "设置更改");
        }
    }
}