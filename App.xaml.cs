using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CallRecording.Models;
using CallRecording.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using MySharedProject;
using MySharedProject.Model;
using MySharedProject.Model.MyAuth;
using NLog;

namespace CallRecording;

[ObservableObject]
public partial class App : Application
{
    [ObservableProperty] private string directorySize = "获取失败";

    [ObservableProperty] private string diskUsage = "获取失败";

    // 在线状态颜色
    [ObservableProperty] private Brush onlineStatusColor = Brushes.Red;

    [ObservableProperty] private string onlineStatusToolTip = "离线";

    string? reftoken;
    public IConfiguration Configuration { get; private set; }
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        logger.Info("程序启动");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            File.AppendAllText("Exception.log", $"{DateTime.Now}\n{e.ExceptionObject}\n");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            File.AppendAllText("Exception.log", $"{DateTime.Now}\n{e.Exception}\n");
            e.SetObserved();
        };


        //禁止重复运行
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
        {
            NotificationService.ShowNotification("程序已启动", "请勿重复启动");
            Environment.Exit(0);
        }

        //释放appsettings.json配置文件
        Utils.EnsureAppSettingsFile();
        //初始化配置文件&补充新增配置项
        ConfigurationHelper.LoadConfiguration();
        Utils.InitAppsettings();

        //可以屏蔽这段正常运行 因为是用的共享项目的代码

        GetSysInfo();

        if (ConfigurationHelper.GetSetting("Is_Rge") != "YYY")
        {
            await loadConfiguration();
        }

        Login();

        //可以屏蔽这段正常运行 因为是用的共享项目的代码
    }


    private void GetSysInfo()
    {
        DataSource.ComputerUserName = Environment.UserName;
        DataSource.Device_info = ConfigurationHelper.GetSetting("Device_info");
        DataSource.Device_code = ConfigurationHelper.GetSetting("Device_code");
    }


    private async void Login()
    {
        try
        {
            string msg =
                MySharedProject.Model.MyAuth.Login.SoftLogin(ConfigurationHelper.GetSetting("User"), null, null,
                    ref reftoken);
            Debug.WriteLine(msg);

            if (msg == "登录成功")
            {
                //心跳
                await Task.Run(async () =>
                {
                    //Thread.Sleep(160000);
                    while (true)
                    {
                        string msg = Heart.SoftHeart(reftoken);
                        if (msg == "心跳成功" && !GlobalsVariables.是否有新版本)
                        {
                            OnlineStatusColor = Brushes.Green;
                            OnlineStatusToolTip = "运行正常";
                        }
                        else if (GlobalsVariables.是否有新版本)
                        {
                            OnlineStatusColor = Brushes.Orange;
                            OnlineStatusToolTip = "有新版本";
                        }
                        else
                        {
                            OnlineStatusColor = Brushes.Red;
                            OnlineStatusToolTip = "离线状态";
                        }

                        await Task.Delay(160000);
                    }
                });
            }
            else if (msg == "账号不存在")
            {
                await loadConfiguration();
                Login();
                //心跳
                await Task.Run(async () =>
                {
                    //Thread.Sleep(160000);
                    while (true)
                    {
                        string msg = Heart.SoftHeart(reftoken);
                        await Task.Delay(160000);
                    }
                });
            }
        }
        catch (Exception e)
        {
            // 
        }
    }


    public async Task loadConfiguration()
    {
        try
        {
            // 获取任务的结果
            Task<string> operatingSystemVersionTask = Task.Run(() => Api.GetOperatingSystemVersion());
            Task<string> machineCodeTask = Task.Run(() => Api.GetMachineCode());

            // 等待任务完成
            await Task.WhenAll(operatingSystemVersionTask, machineCodeTask);

            // 获取任务的结果
            string operatingSystemVersion = operatingSystemVersionTask.GetAwaiter().GetResult();
            string machineCode = machineCodeTask.GetAwaiter().GetResult();

            ConfigurationHelper.SetSetting("Device_info", operatingSystemVersion);
            ConfigurationHelper.SetSetting("Device_code", machineCode);
            ConfigurationHelper.SetSetting("ComputerUserName", DataSource.ComputerUserName);

            // 设置设备信息
            DataSource.Device_info = operatingSystemVersion;
            DataSource.Device_code = machineCode;
        }
        catch (Exception ex)
        {
            // 处理异常
            Debug.WriteLine(ex.Message);
        }

        string user = DataSource.ComputerUserName + Api.GetCurrentTimestamp();
        ConfigurationHelper.SetSetting("User", user);

        string msg = Register.SoftRegister(user);
        Debug.WriteLine(msg);

        if (msg == "注册成功")
        {
            ConfigurationHelper.SetSetting("Is_Rge", "YYY");
        }
    }
}