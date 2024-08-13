using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using MySharedProject;
using MySharedProject.Model.MyAuth;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using CallRecording.Models;
using FlaUI.Core.Input;
using MySharedProject.Model.Download;

namespace CallRecording;

public partial class App : Application
{
    public IConfiguration Configuration { get; private set; }
    string? reftoken;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);
        Configuration = builder.Build();

        //可以屏蔽这段正常运行 因为是用的共享项目的代码

        GetSysInfo();

        if (ConfigurationHelper.GetSetting("Is_Rge") != "YY")
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

        string msg = MySharedProject.Model.MyAuth.Login.SoftLogin(ConfigurationHelper.GetSetting("User"), null, null, ref reftoken);
        Debug.WriteLine(msg);

        if (msg == "登录成功")
        {
            //心跳
            await Task.Run(() =>
            {
                //Thread.Sleep(160000);
                while (true)
                {
                    string msg = MySharedProject.Model.MyAuth.Heart.SoftHeart(reftoken);
                    Thread.Sleep(160000);
                }
            });
        }
        else if (msg == "账号不存在")
        {
            await loadConfiguration();
            Login();
            //心跳
            await Task.Run(() =>
            {
                //Thread.Sleep(160000);
                while (true)
                {
                    string msg = MySharedProject.Model.MyAuth.Heart.SoftHeart(reftoken);
                    Thread.Sleep(160000);
                }
            });
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

        string msg = MySharedProject.Model.MyAuth.Register.SoftRegister(user);
        Debug.WriteLine(msg);

        if (msg == "注册成功")
        {
            ConfigurationHelper.SetSetting("Is_Rge", "YY");
        }

    }
}