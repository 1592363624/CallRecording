using System.ComponentModel;
using System.Windows;
using CallRecording.ViewModels;

namespace CallRecording.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
        };
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        // 阻止窗口关闭并隐藏窗口
        // e.Cancel = true;
        // this.Hide();

        // 结束应用程序
        if (DataContext is MainViewModel viewModel) viewModel.ExitApp(this, null);
    }
}