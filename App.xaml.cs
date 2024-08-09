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

        //可以屏蔽这段正常运行
        GetSysInfo();

        if (ConfigurationHelper.GetSetting("Is_Rge") != "Y")
        {
            await loadConfiguration();
        }

        Login();
        //懒得写心跳了
        await Task.Run(() =>
         {
             //Thread.Sleep(160000);
             while (true)
             {
                 string msg = MySharedProject.Model.MyAuth.Heart.SoftHeart(reftoken);
                 Thread.Sleep(160000);
             }
         });
        //可以屏蔽这段正常运行

    }

    private void GetSysInfo()
    {
        DataSource.ComputerUserName = Environment.UserName;
        DataSource.Device_info = ConfigurationHelper.GetSetting("Device_info");
        DataSource.Device_code = ConfigurationHelper.GetSetting("Device_code");
    }

    private void Login()
    {
        string msg = MySharedProject.Model.MyAuth.Login.SoftLogin(DataSource.Device_code, DataSource.Device_code, null, ref reftoken);
        Debug.WriteLine(msg);
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

        string msg = MySharedProject.Model.MyAuth.Register.SoftRegister(DataSource.Device_code);
        Debug.WriteLine(msg);

        if (msg == "注册成功")
        {
            ConfigurationHelper.SetSetting("Is_Rge", "Y");
        }

    }
}