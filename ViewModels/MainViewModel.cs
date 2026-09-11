using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using CallRecording.Models;
using CallRecording.Services;
using CallRecording.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySharedProject;
using MySharedProject.Model;
using NLog;
using static CallRecording.Models.Recorder;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;

namespace CallRecording.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static Logms _logms;
        private readonly TrayIconManager _trayIconManager;
        private readonly RecordingService _recordingService;
        private readonly HotkeyService _hotkeyService;
        private WindowMonitorService _windowMonitorService;

        [ObservableProperty] private string _recordingSavePath;
        [ObservableProperty] public AudioFormat _selectedFormat;
        [ObservableProperty] private bool _isKeepOriginalFiles;
        private bool _disposed = false;

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
                    SelectedLogLevel = LogLevel.Off;
                }
            }
            catch
            {
                SelectedLogLevel = LogLevel.Off;
            }

            Utils.SetGlobalLogLevel(SelectedLogLevel);

            AudioFormats = new List<AudioFormat>
            {
                AudioFormat.MP3,
                AudioFormat.WAV
            };

            RecordingSavePath = AppDomain.CurrentDomain.BaseDirectory + "Recordings";
            if (!Directory.Exists(RecordingSavePath))
            {
                Directory.CreateDirectory(RecordingSavePath);
                ConfigurationHelper.SetSetting("OutputDirectory", RecordingSavePath);
            }

            RecordingSavePath = ConfigurationHelper.GetSetting("OutputDirectory");
            string? pt = Path.GetPathRoot(RecordingSavePath);
            if (pt == "")
            {
                ConfigurationHelper.SetSetting("OutputDirectory", AppDomain.CurrentDomain.BaseDirectory + "Recordings");
            }

            try
            {
                NotificationService.ShowNotification("通话录音助手正在后台运行", "点击此处可提前关闭通知!");
            }
            catch (Exception ex)
            {
                _logms.LogMessage($"启动通知发送失败: {ex.Message}", "警告(不影响使用)");
            }

            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);

            _trayIconManager = new TrayIconManager(_logms);
            _trayIconManager.SetupTrayIcon(isStealth, ShowApp, ExitApp);

            InitializeWindowMonitorService();
            Utils.软件启动次数add();
            _logms.LogMessage($"欢迎使用通话录音助手( ＾∀＾）／欢迎＼( ＾∀＾）", "通知");

            _recordingService = new RecordingService(_logms);
            _recordingService.RecordingStarted += OnRecordingStarted;
            _recordingService.RecordingStopped += OnRecordingStopped;

            Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedFormat = ConfigurationHelper.GetSetting("音频格式") == "MP3"
                    ? AudioFormat.MP3
                    : AudioFormat.WAV;
            });

            bool.TryParse(ConfigurationHelper.GetSetting("保留独立录音文件"), out bool isKeepOriginalFiles);
            IsKeepOriginalFiles = isKeepOriginalFiles;

            DataSource.gbmvvm.GetDiskInFo();

            _hotkeyService = new HotkeyService(_logms);
            _hotkeyService.OnHotkeyPressed += OnHotkeyPressed;
            _hotkeyService.OnStopHotkeyPressed += OnStopRecordingHotkey;
        }

        private void OnRecordingStarted(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => { _trayIconManager.StartBlinking(); });
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _trayIconManager.StopBlinking();
                DataSource.gbmvvm.GetDiskInFo();
            });
        }

        private void OnHotkeyPressed()
        {
            Application.Current.Dispatcher.InvokeAsync(() => { _recordingService.ToggleRecording(); });
        }

        private void OnStopRecordingHotkey()
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_recordingService.IsRecording())
                {
                    _recordingService.StopRecording();
                }
            });
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

        [RelayCommand]
        private void OpenAudioManager()
        {
            var managerWindow = new AudioManagerWindow();
            var managerViewModel = new AudioManagerViewModel(_logms);
            managerWindow.DataContext = managerViewModel;
            managerWindow.Show();
        }

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
                    _recordingService.RecordingSavePath = dialog.SelectedPath;
                    _logms.LogMessage($"录音文件保存位置已设置为: {RecordingSavePath}", "设置");
                }
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
                _logms.LogMessage("日志已清除。", "设置");
            });
        }

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

        [RelayCommand]
        private void KeepOriginalFiles()
        {
            IsKeepOriginalFiles = !IsKeepOriginalFiles;
            _recordingService.IsKeepOriginalFiles = IsKeepOriginalFiles;

            if (IsKeepOriginalFiles)
            {
                _logms.LogMessage("已启用保留独立录音文件功能", "设置");
            }
            else
            {
                _logms.LogMessage("已禁用保留独立录音文件功能", "设置");
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            dynamic shell = null;
            dynamic shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                shell = Activator.CreateInstance(shellType);
                shortcut = shell.CreateShortcut(shortcutPath);
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
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        private void ShowApp(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow?.Show();
                Application.Current.MainWindow.WindowState = WindowState.Normal;
                Application.Current.MainWindow.Activate();
            });
        }

        public void ExitApp(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logms.LogMessage("退出应用程序。", "系统");
                Dispose();
                Application.Current.Shutdown();
            });
        }

        private void InitializeWindowMonitorService()
        {
            _windowMonitorService = new WindowMonitorService(_logms);
            _windowMonitorService.WindowCreated += OnWindowCreated;
            _windowMonitorService.WindowDestroyed += OnWindowDestroyed;
        }

        private void OnWindowCreated(object sender, IntPtr hwnd)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                string processName = GetProcessNameFromHwnd(hwnd);
                string title = GetWindowTitleFromHwnd(hwnd);
                _recordingService.StartRecording(processName + "_" + title);
            });
        }

        private void OnWindowDestroyed(object sender, IntPtr hwnd)
        {
            Application.Current.Dispatcher.InvokeAsync(() => { _recordingService.StopRecording(); });
        }

        private string GetProcessNameFromHwnd(IntPtr hwnd)
        {
            WindowMonitor.GetWindowThreadProcessId(hwnd, out uint processId);
            try
            {
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowTitleFromHwnd(IntPtr hwnd)
        {
            return WindowInfo.GetWindowTitle(hwnd);
        }

        partial void OnSelectedFormatChanged(AudioFormat value)
        {
            if (_recordingService.IsRecording())
            {
                MessageBoxResult result =
                    MessageBox.Show("检测到正在录制,为更改音频格式需要停止录制,是否继续更换音频格式", "设置更改", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    _recordingService.StopRecording();
                    _logms.LogMessage($"所选录制音频格式已更改为: {value}", "用户确认更改音频格式");
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    _logms.LogMessage($"用户已取消更改所选录制音频格式", "用户取消更改音频格式");
                }
            }

            _recordingService.SelectedFormat = value;
            _logms.LogMessage($"所选录制音频格式已更改为: {value}", "设置更改");
        }

        public void SetHotkey(Keys hotkey)
        {
            _hotkeyService.SetHotkey(hotkey);
        }

        public void SetStopHotkey(Keys hotkey)
        {
            _hotkeyService.SetStopHotkey(hotkey);
        }

        public void ReinitializeWindowMonitor()
        {
            _windowMonitorService?.Dispose();
            InitializeWindowMonitorService();
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
                _hotkeyService?.Dispose();
                _recordingService?.Dispose();
                _windowMonitorService?.Dispose();
                _trayIconManager?.Dispose();
            }

            _disposed = true;
        }

        ~MainViewModel()
        {
            Dispose(false);
        }
    }
}