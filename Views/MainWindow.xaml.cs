using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using CallRecording.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using MySharedProject;
using MySharedProject.Model;
using MySharedProject.Model.Download;
using MySharedProject.Model.MyAuth;
using MySharedProject.Utiles;
using MySharedProject.ViewModels.MyAuth;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Control = System.Windows.Forms.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Drawing.Point;

namespace CallRecording.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _logs;

    bool 是否点击通知更新的确认按钮 = false;

    // GlobalMVVM gmvvm = new GlobalMVVM();
    private bool isDragging = false;
    private MarkerWindow markerWindow;

    string msg = "";

    public MainWindow()
    {
        InitializeComponent();

        CheckUpdate();

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
            Diskoccupancyinformation.DataContext = MonitorLst.DataContext = DataSource.gbmvvm;
            Button_OpenAudioManager.DataContext = mainViewModel;
            DataSource.gbmvvm.Pn = ConfigurationHelper.GetSetting("监控窗口进程名");
            DataSource.gbmvvm.Cn = ConfigurationHelper.GetSetting("监控窗口类名");
            Grid_CP.DataContext = DataSource.gbmvvm;
            DataContext = mainViewModel;

            //初始化默认数据
            Hide();
            bool.TryParse(ConfigurationHelper.GetSetting("是否开机自启"), out bool isStartupEnabled);
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);
            HotkeyTextBox.Text = ConfigurationHelper.GetSetting("录音快捷键");
            kjzq.IsChecked = isStartupEnabled;
            ysms.IsChecked = isStealth;

            //初始化监控选择框是否选中
            DataSource.gbmvvm.IsWeChatChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("WeChat");
            DataSource.gbmvvm.IsWeChatWorkChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("WXWork");
            DataSource.gbmvvm.IsQQChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("QQ");
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
                                FileName = "https://wwf.lanzoue.com/b00g2fhjzg?pwd=1bxs#1bxs",
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

    //检测更新
    private async Task CheckUpdate()
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
                latestVer = (decimal.Parse(latestVer) - 0.1m).ToString();
            }

            string? UpdateLog = Web.GetUpdateLog(DataSource.Skey);
            text_updateLog.Text = "\n" + UpdateLog + "\n";
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            Resources.Add("WindowTitle", "通话录音助手 v" + fileVersionInfo.FileVersion);

            if (latestVer != fileVersionInfo.FileVersion)
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
                    StartUpdata();
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

    private void MainWindow_Closing(object sender, CancelEventArgs e)
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

    private void CaptureWindowInfo()
    {
        // 获取当前鼠标位置
        Point screenPoint = Control.MousePosition;

        // 获取窗口句柄
        IntPtr hWnd = WindowFromPoint(screenPoint);

        if (hWnd != IntPtr.Zero)
        {
            // 获取窗口类名
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            // 获取窗口所属的进程ID
            GetWindowThreadProcessId(hWnd, out uint processId);
            Process process = Process.GetProcessById((int)processId);

            ConfigurationHelper.SetSetting("监控窗口类名", ConfigurationHelper.GetSetting("监控窗口类名") + "|" + className);
            ConfigurationHelper.SetSetting("监控窗口进程名",
                ConfigurationHelper.GetSetting("监控窗口进程名") + "|" + process.ProcessName);

            DataSource.gbmvvm.Pn = ConfigurationHelper.GetSetting("监控窗口进程名");
            DataSource.gbmvvm.Cn = ConfigurationHelper.GetSetting("监控窗口类名");
        }
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
            //打开文件夹
            Process.Start("explorer.exe", FileUtil.当前文件目录 + "Recordings");
        }
    }

    private void Cb_AudioFormats_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfigurationHelper.SetSetting("音频格式", cb_AudioFormats.SelectedItem.ToString());
    }

    private void TextBox_Cn_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        DataSource.gbmvvm.Cn = TextBox_Cn.Text;
        ConfigurationHelper.SetSetting("监控窗口类名", DataSource.gbmvvm.Cn);
    }

    private void TextBox_Pn_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        DataSource.gbmvvm.Pn = TextBox_Pn.Text;
        ConfigurationHelper.SetSetting("监控窗口进程名", DataSource.gbmvvm.Pn);
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
}