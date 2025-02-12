using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CallRecording.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using MySharedProject;
using MySharedProject.Model;
using MySharedProject.Model.MyAuth;
using MySharedProject.Utiles;
using Control = System.Windows.Forms.Control;
using Point = System.Drawing.Point;

namespace CallRecording.Views;

public partial class MainWindow : Window
{
    bool 是否点击通知更新的确认按钮 = false;
    private bool isDragging = false;


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
            Diskoccupancyinformation.DataContext = DataSource.gbmvvm;
            DataContext = mainViewModel;

            //初始化默认数据
            Hide();
            bool.TryParse(ConfigurationHelper.GetSetting("是否开机自启"), out bool isStartupEnabled);
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);
            kjzq.IsChecked = isStartupEnabled;
            ysms.IsChecked = isStealth;
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
        //var client = new RestClient("https://gitee.com/Shell520/shell/raw/master/admin/通话录音助手");
        //var request = new RestRequest("", Method.Get);
        //RestResponse response = client.Execute<RestResponse>(request);
        string? latestVersion = Soft.GetNewVersion();
        string? UpdateLog = Web.GetUpdateLog("2706a699-8246-4ffc-afb9-1d904e1dbe4f");
        text_updateLog.Text = "\n" + UpdateLog + "\n";
        Assembly assembly = Assembly.GetExecutingAssembly();
        FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        if (latestVersion != fileVersionInfo.FileVersion)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText("检测到有新版本")
                    .AddInlineImage(new Uri(FileUtil.当前文件目录 + "Assets/icons/安全.png"))
                    .AddButton(new ToastButton()
                        .SetContent("查看更新日志")
                        .AddArgument("action", "ConfirmUpdate")) // 传递参数
                    .AddButton(new ToastButtonDismiss("取消")) // 取消按钮
                    .Show();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Toast 显示失败: " + e.Message);
            }
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
        isDragging = true;
        Mouse.Capture(sender as UIElement);
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
        }
    }

    private void adm_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            Debug.WriteLine("正在拖动...");
        }
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
}