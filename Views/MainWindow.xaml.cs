using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Windows;
using CallRecording.Models;
using CallRecording.ViewModels;
using RestSharp;

namespace CallRecording.Views;

public partial class MainWindow : Window
{
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
            kjzq.IsChecked = isStartupEnabled;
        };

    }

    private async Task CheckUpdate()
    {
        var client = new RestClient("https://gitee.com/Shell520/shell/raw/master/admin/通话录音助手");
        var request = new RestRequest("", Method.Get);
        RestResponse response = client.Execute<RestResponse>(request);
        msg = response.Content;
        if (msg != "2.6")
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
}