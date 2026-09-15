using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Navigation;
using CallRecording.Models;
using CallRecording.Services;
using CallRecording.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using MySharedProject;
using MySharedProject.Model;
using MySharedProject.Model.Download;
using MySharedProject.Model.MyAuth;
using MySharedProject.Utiles;
using MySharedProject.ViewModels.MyAuth;
using Newtonsoft.Json;
using NLog;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Control = System.Windows.Forms.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Drawing.Point;

namespace CallRecording.Views;

public partial class MainWindow
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // GlobalMVVM gmvvm = new GlobalMVVM();
    private bool isDragging;
    private MarkerWindow? markerWindow;

    public MainWindow()
    {
        InitializeComponent();

        _ = CheckUpdate();

        WindowState = WindowState.Minimized;

        Closing += MainWindow_Closing;
        // 订阅启动事件
        Loaded += (sender, e) =>
        {
            // 创建主视图模型
            var mainViewModel = new MainViewModel();
            var app = App.Current;
            // 设置主窗口的数据上下文
            //Bottom_information_bar.DataContext = app;
            Onlineidentification.DataContext = app;
            Diskoccupancyinformation.DataContext = DataSource.gbmvvm;
            Button_OpenAudioManager.DataContext = mainViewModel;
            Loglvl.DataContext = mainViewModel;
            DataGrid_MonitoredWindows.DataContext = DataSource.gbmvvm;
            DataContext = mainViewModel;

            //初始化默认数据
            Hide();
            bool.TryParse(ConfigurationHelper.GetSetting("是否开机自启"), out bool isStartupEnabled);
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);
            HotkeyTextBox.Text = ConfigurationHelper.GetSetting("录音快捷键");

            // 设置结束热键文本显示
            var stopHotkey = ConfigurationHelper.GetSetting("结束录音快捷键");
            EndHotkeyTextBox.Text = !string.IsNullOrEmpty(stopHotkey) ? stopHotkey : "Ctrl + End";

            kjzq.IsChecked = isStartupEnabled;
            ysms.IsChecked = isStealth;

            // 初始化保留独立录音文件复选框
            bool.TryParse(ConfigurationHelper.GetSetting("保留独立录音文件"), out bool isKeepOriginalFiles);
            KeepOriginalFiles.IsChecked = isKeepOriginalFiles;

            // 初始化更新模块 ComboBox 默认选项
            var module = ConfigurationHelper.GetSetting("更新模块");
            if (string.IsNullOrWhiteSpace(module) || module == "NULL")
            {
                module = "GitHub";
            }
            Cb_UpdateModule.SelectedIndex =
                string.Equals(module, "Legacy", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        };
        // 订阅通知按钮事件
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            try
            {
                // 解析传递的参数
                var args = ToastArguments.Parse(toastArgs.Argument);

                // 使用 TryGetValue 方法获取 'action' 参数
                if (args.TryGetValue("action", out string actionValue))
                {
                    if (actionValue == "ConfirmUpdate")
                    {
                        // 执行确认操作的逻辑
                        // 打开日志窗口和 URL 的操作
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateLog updateLogWindow = new UpdateLog();
                            updateLogWindow.Show();
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://github.com/1592363624/CallRecording/releases",
                                UseShellExecute = true
                            });
                        });
                    }
                    else if (actionValue == "ConfirmGitHubUpdate")
                    {
                        // 新模块（GitHub Releases）触发的更新通知：打开对应 release 的页面
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var url = args.TryGetValue("url", out var u) && !string.IsNullOrWhiteSpace(u)
                                ? u
                                : GitHubUpdateService.ReleasesPageUrl;
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        });
                    }
                }
                else
                {
                    // 如果没有传递 'action' 参数，处理默认逻辑
                    Debug.WriteLine("没有传递 'action' 参数，执行默认操作,视为没点击任何通知按钮");
                }
            }
            catch (Exception ex)
            {
                // 捕获并记录异常
                Debug.WriteLine("处理 Toast 通知时出现异常: " + ex.Message);
            }
        };

        //后台线程处理一些配置读取提醒等事情
        Task.Run(() =>
        {
            //延迟30秒
            Thread.Sleep(30000);
            //节假日彩蛋提示
            HolidayEastereggtips();
        });
    }


    private static void HolidayEastereggtips()
    {
        //节日彩蛋提示
        var greeter = new HolidayGreeter();
        var (title, message) = greeter.GetGreeting(DateTime.Now);
        if (title == "普通的一天") return;
        title = "今天是 【" + title + "】 哦!";
        NotificationService.ShowNotification(title, message);
    }

    // 获取窗口句柄
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point p);

    // 获取窗口类名
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // 获取窗口进程ID
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // 获取窗口标题
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    //检测更新 - 调度方法：根据配置选择使用新的 GitHub 模块还是旧的 Legacy 模块
    private async Task CheckUpdate()
    {
        try
        {
            // 读取当前使用的更新模块；默认 GitHub
            string module = ConfigurationHelper.GetSetting("更新模块");
            if (string.IsNullOrWhiteSpace(module) || module == "NULL")
            {
                module = "GitHub";
            }

            if (string.Equals(module, "Legacy", StringComparison.OrdinalIgnoreCase))
            {
                await CheckUpdateLegacy();
            }
            else
            {
                await CheckUpdateGitHub();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("[CheckUpdate] 调度异常: " + e);
        }
    }

    // 检测更新 - 旧模块（52shell 后端，含自动下载安装逻辑）
    private async Task CheckUpdateLegacy()
    {
        try
        {
            string? latestVersion = Soft.GetNewVersion();
            // 获取更新日志列表，并取第一个元素的版本号
            var NewVersion = JsonConvert.DeserializeObject<ApiResponse>(latestVersion);
            var latestVer = NewVersion?.result?.list?[0].ver;
            var status = NewVersion?.result?.list?[0].status;
            if (status == "0")
            {
                // 将版本号转换为Version对象进行处理
                if (!string.IsNullOrEmpty(latestVer) && Version.TryParse(latestVer, out Version? version))
                {
                    int build = version.Build > 0 ? version.Build - 1 : 0;
                    version = new Version(version.Major, version.Minor, build);
                    latestVer = version.ToString();
                }
            }

            string? UpdateLog = Web.GetUpdateLog(DataSource.Skey);
            text_updateLog.Text = "\n" + UpdateLog + "\n";
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            Resources.Add("WindowTitle", "通话录音助手 v" + fileVersionInfo.FileVersion);

            if (Version.Parse(latestVer) > Version.Parse(fileVersionInfo.FileVersion))
            {
                try
                {
                    GlobalsVariables.是否有新版本 = true;

                    new ToastContentBuilder()
                        .AddText("检测到有新版本")
                        // .AddInlineImage(new Uri(FileUtil.当前文件目录 + "Assets/icons/安全.png"))
                        .AddButton(new ToastButton()
                            .SetContent("查看更新日志")
                            .AddArgument("action", "ConfirmUpdate")) // 传递参数
                        .AddButton(new ToastButtonDismiss("取消")) // 取消按钮
                        .Show();

                    //开始下载更新文件
                    await StartUpdata();
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Toast 显示失败: " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            throw;
        }
    }

    // 检测更新 - 新模块（GitHub Releases）：仅比对版本+弹出通知跳转，不做自动下载安装
    private async Task CheckUpdateGitHub()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var currentVersion = Version.Parse(fileVersionInfo.FileVersion ?? "0.0.0");
            Resources.Add("WindowTitle", "通话录音助手 v" + fileVersionInfo.FileVersion);

            var release = await GitHubUpdateService.GetLatestReleaseAsync();
            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                // 拉取失败，按"没有新版本"处理，不打扰用户
                text_updateLog.Text =
                    "\n未能从 GitHub 获取最新版本信息（所有镜像源都失败了）。\n" +
                    "可能原因：网络受限 / DNS 污染 / 防火墙拦截。\n" +
                    "临时方案：在「关于软件」中切到 Legacy 模块。\n" +
                    "长期方案：在 appsettings.json 的 \"GitHub镜像\" 配置项里填上可用的镜像 URL（多个用 | 分隔）。\n";
                logger.Warn("[CheckUpdateGitHub] 获取最新 release 失败（所有源都失败）");
                return;
            }

            var latestVersion = GitHubUpdateService.TryParseTagAsVersion(release.TagName);

            // 用 release body 作为系统公告（面板展示）
            if (!string.IsNullOrWhiteSpace(release.Body))
            {
                text_updateLog.Text = "\n" + release.Body + "\n";
            }
            else
            {
                text_updateLog.Text = "\n当前最新版本：" + release.TagName + "\n";
            }

            if (latestVersion != null && latestVersion > currentVersion)
            {
                try
                {
                    GlobalsVariables.是否有新版本 = true;

                    var htmlUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                        ? GitHubUpdateService.ReleasesPageUrl
                        : release.HtmlUrl;

                    new ToastContentBuilder()
                        .AddText($"检测到 GitHub 上有新版本：{release.TagName}")
                        .AddText("点击下方按钮前往 release 页面查看并下载")
                        .AddButton(new ToastButton()
                            .SetContent("前往 release 页面")
                            .AddArgument("action", "ConfirmGitHubUpdate")
                            .AddArgument("url", htmlUrl))
                        .AddButton(new ToastButtonDismiss("稍后再说"))
                        .Show();

                    logger.Info($"检测到 GitHub 新版本：{release.TagName}（当前 {currentVersion}），跳转链接：{htmlUrl}");
                }
                catch (Exception e)
                {
                    Debug.WriteLine("[CheckUpdateGitHub] Toast 显示失败: " + e.Message);
                }
            }
            else
            {
                logger.Info($"[CheckUpdateGitHub] 当前已是最新（{currentVersion}），GitHub 最新：{release.TagName}");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("[CheckUpdateGitHub] 异常: " + e);
        }
    }

    public async Task StartUpdata()
    {
        string updata = ConfigurationHelper.GetSetting("上次执行检测更新时间");
        //当前系统时间和上次执行检测更新时间比较
        if (DateTime.Now.Subtract(DateTime.Parse(updata)).TotalHours >= 1)
        {
            // 更新时间已超过1小时，执行更新操作
            ConfigurationHelper.SetSetting("上次执行检测更新时间", DateTime.Now.ToString());
            Debug.WriteLine("上次执行检测更新时间：" + updata + "，当前系统时间：" + DateTime.Now.ToString() + "，当前系统时间已超过1小时，执行更新操作");
        }
        else
        {
            // 更新时间未超过1小时，不执行更新操作
            Debug.WriteLine("上次执行检测更新时间：" + updata + "，当前系统时间：" + DateTime.Now.ToString() + "，当前系统时间未超过1小时，不执行更新操作");
            return;
        }

        //删除原有的从新下载
        if (File.Exists(@"C:\Shell\Download\CallRecording.zip"))
        {
            // 存在则删除
            File.Delete(@"C:\Shell\Download\CallRecording.zip");
            Debug.WriteLine("已删除文件C:\\Shell\\Download\\CallRecording.zip");
        }

        //开始下载
        await StarDownload.StarDown(Soft.getMsg("更新JSON数据"));
        new ToastContentBuilder()
            .AddText("新版本准备完毕,准备开始自更新")
            .Show();
        await CheckFileCallRecording();
    }

    //检查是否存在文件C:\Shell\Download\CallRecording.zip
    public async Task CheckFileCallRecording()
    {
        if (File.Exists(@"C:\Shell\Download\CallRecording.zip"))
        {
            // 存在则执行脚本
            Utils.UnzipBat();
        }
        else
        {
            Debug.WriteLine("未检测到文件C:\\Shell\\Download\\CallRecording.zip，跳过执行更新脚本");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 阻止窗口关闭并隐藏窗口
        e.Cancel = true;
        this.Hide();

        // 结束应用程序
        // if (DataContext is MainViewModel viewModel) viewModel.ExitApp(this, null);
    }

    private void adm_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 确保之前的状态已清理
        if (markerWindow != null)
        {
            markerWindow.Close();
            markerWindow = null;
        }

        // 重置拖拽状态
        isDragging = true;
        var element = sender as UIElement;
        if (element != null)
        {
            element.CaptureMouse();
        }

        e.Handled = true;
    }

    private void adm_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isDragging)
        {
            // 停止拖动
            isDragging = false;
            Mouse.Capture(null);

            // 获取鼠标当前所在的窗口信息
            CaptureWindowInfo();
        }
    }

    private void adm_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (isDragging)
        {
            // 完全重置拖拽状态
            isDragging = false;
            var element = sender as UIElement;
            if (element != null)
            {
                element.ReleaseMouseCapture();
            }

            Mouse.Capture(null);
            DragFeedbackLayer.Children.Clear();

            // 关闭标记窗口
            if (markerWindow != null)
            {
                markerWindow.Close();
                markerWindow = null;
            }

            // 强制鼠标状态更新
            e.Handled = true;
        }
    }

    // RECT结构体定义，用于GetClientRect函数
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // 获取客户区大小
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    private void CaptureWindowInfo()
    {
        // 获取当前鼠标位置
        Point screenPoint = Control.MousePosition;

        // 获取窗口句柄
        IntPtr hWnd = WindowFromPoint(screenPoint);

        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        // 获取窗口类名
        StringBuilder className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);

        // 获取窗口所属的进程ID
        GetWindowThreadProcessId(hWnd, out uint processId);
        Process process = Process.GetProcessById((int)processId);

        // 获取窗口标题
        StringBuilder windowTitle = new StringBuilder(256);
        GetWindowText(hWnd, windowTitle, windowTitle.Capacity);

        // 验证获取的窗口信息是否有效
        string classNameStr = className.ToString().Trim();
        string processNameStr = process.ProcessName.Trim();
        string windowTitleStr = windowTitle.ToString().Trim();

        if (string.IsNullOrEmpty(classNameStr) || string.IsNullOrEmpty(processNameStr))
        {
            logger.Info("获取到无效的窗口信息，跳过添加");
            return;
        }

        // 过滤：本软件自身窗口（包括子窗口）。如果拖到的是软件自身，提示用户而不写入列表。
        string selfProcessName = Process.GetCurrentProcess().ProcessName;
        if (string.Equals(processNameStr, selfProcessName, StringComparison.OrdinalIgnoreCase)
            || windowTitleStr.Contains("通话录音助手", StringComparison.Ordinal))
        {
            MessageBox.Show(
                "捕获到的是本软件自身的窗口，无法被监控。\n请把光标拖到要监控的第三方通话窗口上再松开按钮。",
                "添加监控窗口",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            logger.Info($"已过滤本软件自身窗口: {processNameStr}, {windowTitleStr}");
            return;
        }

        // 若能读到客户区尺寸，自动填入宽高（长边→Height，短边→Width），便于尺寸检测配置
        int? capturedWidth = null;
        int? capturedHeight = null;
        try
        {
            if (GetClientRect(hWnd, out RECT clientRect))
            {
                int clientWidth = clientRect.Right - clientRect.Left;
                int clientHeight = clientRect.Bottom - clientRect.Top;
                if (clientWidth > 0 && clientHeight > 0)
                {
                    capturedWidth = Math.Min(clientWidth, clientHeight);
                    capturedHeight = Math.Max(clientWidth, clientHeight);
                    logger.Info($"捕获窗口 {processNameStr} 客户区尺寸: {capturedWidth}x{capturedHeight}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "读取客户区尺寸失败");
        }

        var item = new MonitoredWindow
        {
            ProcessName = processNameStr,
            ClassName = classNameStr,
            Title = windowTitleStr,
            Width = capturedWidth,
            Height = capturedHeight,
            // 默认开启尺寸检测，用户可在 UI 中取消或调整宽高
            SizeCheckEnabled = capturedWidth.HasValue && capturedHeight.HasValue,
        };

        if (!DataSource.gbmvvm.TryAddMonitoredWindow(item, out _, showDuplicateMessage: true))
        {
            // 重复条目由 TryAddMonitoredWindow 内部提示，这里直接返回
            return;
        }

        // 选中新增项便于用户编辑
        if (DataGrid_MonitoredWindows != null)
        {
            DataGrid_MonitoredWindows.SelectedItem = item;
            DataGrid_MonitoredWindows.ScrollIntoView(item);
        }

        logger.Info($"已添加监控窗口: {item.DisplayName}");
    }

    private void adm_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            // 获取全局鼠标位置
            var screenPos = Control.MousePosition;

            // 创建或更新全屏标记窗口
            if (markerWindow == null)
            {
                markerWindow = new MarkerWindow();
                markerWindow.Show();
            }

            // 更新标记位置
            markerWindow.UpdatePosition(screenPos.X, screenPos.Y);
        }
        else if (markerWindow != null)
        {
            markerWindow.Close();
            markerWindow = null;
        }
    }

    private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ListBox_rz.SelectedItems.Count > 0;
    }

    private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selectedItems = ListBox_rz.SelectedItems;
        var sb = new StringBuilder();

        foreach (var item in selectedItems)
        {
            sb.AppendLine(item.ToString());
        }

        Clipboard.SetText(sb.ToString());
    }

    private void ListBox_rz_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 获取当前选中的项
        var selectedItem = ListBox_rz.SelectedItem as string;
        if (selectedItem != null)
        {
            // 优先使用用户在设置中选择的录音保存路径，避免双击日志时打开了软件默认目录
            string openPath = (DataContext as MainViewModel)?.RecordingSavePath;

            // 路径为空或不存在时，回退到软件默认的录音目录，保证功能始终可用
            if (string.IsNullOrWhiteSpace(openPath) || !Directory.Exists(openPath))
            {
                openPath = FileUtil.当前文件目录 + "Recordings";
            }

            //打开文件夹
            Process.Start("explorer.exe", openPath);
        }
    }

    private void Cb_AudioFormats_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfigurationHelper.SetSetting("音频格式", cb_AudioFormats.SelectedItem.ToString());
    }

    // 更新模块切换：选择项 0=GitHub（新模块），1=Legacy（旧模块）
    private void Cb_UpdateModule_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Cb_UpdateModule.SelectedItem is ComboBoxItem item)
        {
            var value = item.Content?.ToString()?.Contains("Legacy") == true ? "Legacy" : "GitHub";
            ConfigurationHelper.SetSetting("更新模块", value);
            Debug.WriteLine($"[更新模块] 已切换为：{value}");
        }
    }

    private void Btn_ChooseSavePath_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 右键重置为软件根目录下的默认保存路径，避免触发左键的 ChooseSavePathCommand
        if (DataContext is MainViewModel viewModel && viewModel.ResetSavePathCommand.CanExecute(null))
        {
            viewModel.ResetSavePathCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // 调用系统默认邮件客户端
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true // 必须设置为 true（.NET Core/5+ 要求）
        });
        e.Handled = true; // 标记事件已处理
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 忽略修饰键（如 Ctrl、Alt、Shift）
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            return;
        }

        // 获取按下的键
        Keys pressedKey = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);

        // 获取修饰键状态
        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // 禁止使用Ctrl作为前缀
        if (isCtrlPressed)
        {
            MessageBox.Show("录音快捷键不能以Ctrl作为前缀，请选择其他按键", "快捷键设置");
            e.Handled = true;
            return;
        }

        // 构建快捷键字符串
        string hotkeyString = string.Empty;
        if (isCtrlPressed) hotkeyString += "Ctrl + ";
        if (isAltPressed) hotkeyString += "Alt + ";
        if (isShiftPressed) hotkeyString += "Shift + ";
        hotkeyString += pressedKey.ToString();

        // 显示快捷键
        HotkeyTextBox.Text = hotkeyString;

        // 设置快捷键
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetHotkey(pressedKey);
        }

        // 阻止事件继续传递
        e.Handled = true;
    }

    // 新增方法：处理结束热键设置
    private void EndHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 忽略修饰键（如 Ctrl、Alt、Shift）
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            return;
        }

        // 获取按下的键
        Keys pressedKey = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);

        // 获取修饰键状态
        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // 构建快捷键字符串
        string hotkeyString = string.Empty;
        if (isCtrlPressed) hotkeyString += "Ctrl + ";
        if (isAltPressed) hotkeyString += "Alt + ";
        if (isShiftPressed) hotkeyString += "Shift + ";
        hotkeyString += pressedKey.ToString();

        // 显示快捷键
        EndHotkeyTextBox.Text = hotkeyString;

        // 设置结束快捷键
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetStopHotkey(pressedKey);
        }

        // 阻止事件继续传递
        e.Handled = true;
    }

    // private void Loglevel_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    // {
    //     Utils.SetGlobalLogLevel(Loglevel.SelectedItem);
    // }
}