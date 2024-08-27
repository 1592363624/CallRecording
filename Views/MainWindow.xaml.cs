using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CallRecording.Models;
using CallRecording.ViewModels;
using MySharedProject.Utiles;
using RestSharp;

namespace CallRecording.Views;

public partial class MainWindow : Window
{

    // 获取窗口句柄
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(System.Drawing.Point p);

    // 获取窗口类名
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // 获取窗口进程ID
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);


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
            // 设置主窗口的数据上下文
            DataContext = mainViewModel;
            Hide();
            bool.TryParse(ConfigurationHelper.GetSetting("是否开机自启"), out bool isStartupEnabled);
            bool.TryParse(ConfigurationHelper.GetSetting("是否隐身模式启动"), out bool isStealth);
            kjzq.IsChecked = isStartupEnabled;
            ysms.IsChecked = isStealth;
        };

    }

    private async Task CheckUpdate()
    {
        var client = new RestClient("https://gitee.com/Shell520/shell/raw/master/admin/通话录音助手");
        var request = new RestRequest("", Method.Get);
        RestResponse response = client.Execute<RestResponse>(request);
        string? latestVersion = response.Content;
        string currentVersion = "2.9";
        if (latestVersion != currentVersion)
        {
            UpdateLog updateLogWindow = new UpdateLog();
            updateLogWindow.Show();
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://wwf.lanzoue.com/b00g2fhjzg?pwd=1bxs#1bxs",
                UseShellExecute = true
            });

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
    private bool isDragging = false;

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
        System.Drawing.Point screenPoint = System.Windows.Forms.Control.MousePosition;

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
            ConfigurationHelper.SetSetting("监控窗口进程名", ConfigurationHelper.GetSetting("监控窗口进程名") + "|" + process.ProcessName);
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
}