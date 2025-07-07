using System.Windows;
using CallRecording.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using MySharedProject.Model;

namespace CallRecording.ViewModels
{
    [ObservableObject]
    public partial class GlobalMVVM
    {
        [ObservableProperty] public long availableFreeSpace;

        [ObservableProperty] public string availableFreeSpaceFM;

        [ObservableProperty] public string cn;
        [ObservableProperty] public string pn;
        [ObservableProperty] public string tt;

        [ObservableProperty] public long iusedSpace;

        [ObservableProperty] public string iusedSpaceFM;

        [ObservableProperty] public long totalSize;

        [ObservableProperty] public string totalSizeFM;

        [ObservableProperty] public long usedSpace;

        [ObservableProperty] public string usedSpaceFM;

        [ObservableProperty] public int wt = 500;

        [ObservableProperty] public bool _isWeChatChecked;

        [ObservableProperty] public bool _isWeChatWorkChecked;

        [ObservableProperty] public bool _isQQChecked;

        private int 判断软件是否刚启动 = 0;

        partial void OnIsWeChatCheckedChanged(bool value) => UpdateMonitorSettings();
        partial void OnIsWeChatWorkCheckedChanged(bool value) => UpdateMonitorSettings();
        partial void OnIsQQCheckedChanged(bool value) => UpdateMonitorSettings();


        // 计算每个部分的比例（总宽度为 wt）
        public double UsedSpaceProportion => (TotalSize > 0) ? ((double)UsedSpace / TotalSize) * Wt : 0;

        public double AvailableFreeSpaceProportion =>
            (TotalSize > 0) ? ((double)AvailableFreeSpace / TotalSize) * Wt : 0;

        public double IusedSpaceProportion => (TotalSize > 0) ? ((double)IusedSpace / TotalSize) * Wt : 0;

        // 用于计算第二个矩形和第三个矩形的偏移量
        public double TotalUsedProportion => UsedSpaceProportion + AvailableFreeSpaceProportion;

        private readonly Dictionary<string, (string Process, string Class, string Title)> _appConfigMap = new()
        {
            { "微信", ("WeChat|Weixin", "AudioWnd|ILinkAudioWnd|Qt51514QWindowIcon", "语音") },
            { "QQNT", ("QQ", "Chrome_RenderWidgetHostHWND", "语音") },
            { "企业微信", ("WXWork", "WXworkWindow", "语音") }
        };

        private void UpdateMonitorSettings()
        {
            //因为首次初始化这个类的时候会执行一次,所以需要把第一次排除掉
            if (判断软件是否刚启动 == 0)
            {
                // 读取配置文件初始化IsWeChatChecked, IsWeChatWorkChecked, IsQQChecked
                IsWeChatChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("WeChat|Weixin");
                IsWeChatWorkChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("WXWork");
                IsQQChecked = ConfigurationHelper.GetSetting("监控窗口进程名").Contains("QQ");
                判断软件是否刚启动++;
            }

            var processList = new List<string>();
            var classList = new List<string>();
            var titleList = new List<string>();

            // 根据勾选状态添加新配置
            if (IsWeChatChecked)
            {
                processList.AddRange("WeChat|Weixin".Split('|'));
                classList.AddRange("AudioWnd|ILinkAudioWnd|Qt51514QWindowIcon".Split('|'));
                titleList.AddRange("语音".Split('|'));
            }

            if (IsWeChatWorkChecked)
            {
                processList.Add("WXWork");
                classList.Add("WXworkWindow");
                titleList.Add("语音");
            }

            if (IsQQChecked)
            {
                processList.Add("QQ");
                classList.Add("Chrome_RenderWidgetHostHWND");
                titleList.Add("语音");
            }

            // 获取现有的手动配置（过滤掉自动生成的配置）
            var existingProcess = ConfigurationHelper.GetSetting("监控窗口进程名").Split('|')
                .Where(x => !string.IsNullOrEmpty(x)
                            && !x.Contains("要监控")
                            && !_appConfigMap.Values.Any(v => v.Process == x)); // 排除自动配置项

            var existingClass = ConfigurationHelper.GetSetting("监控窗口类名").Split('|')
                .Where(x => !string.IsNullOrEmpty(x)
                            && !x.Contains("要监控")
                            && !_appConfigMap.Values.Any(v =>
                                v.Class.Split('|').Contains(x))); // 检查分割后的类名 // 排除自动配置项

            var existingTitle = ConfigurationHelper.GetSetting("监控窗口标题").Split('|')
                .Where(x => !string.IsNullOrEmpty(x)
                            && !x.Contains("要监控")
                            && !_appConfigMap.Values.Any(v =>
                                v.Title.Split('|').Contains(x))); // 检查分割后的标题 // 排除自动配置项

            // 合并配置（当前勾选项 + 手动添加项）
            var finalProcess = processList.Union(existingProcess).Distinct().ToArray();
            var finalClass = classList.Union(existingClass).Distinct().ToArray();
            var finalTitle = titleList.Union(existingTitle).Distinct().ToArray();

            // 保存配置
            ConfigurationHelper.SetSetting("监控窗口进程名",
                string.Join("|", finalProcess) + "|要监控的窗口进程名");
            ConfigurationHelper.SetSetting("监控窗口类名",
                string.Join("|", finalClass) + "|要监控的窗口类名");
            ConfigurationHelper.SetSetting("监控窗口标题",
                string.Join("|", finalTitle) + "|要监控的窗口标题");

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新 UI
                Pn = string.Join("|", finalProcess) + "|要监控的窗口进程名";
                Cn = string.Join("|", finalClass) + "|要监控的窗口类名";
                Tt = string.Join("|", finalTitle) + "|要监控的窗口标题";
            });
        }


        public void GetDiskInFo()
        {
            // 读取磁盘占用相关信息
            Task.Run(() =>
            {
                var path = ConfigurationHelper.GetSetting("OutputDirectory");
                var DiskInfoIn = Utils.GetDiskInfoInMB(path);

                // 回到主线程更新 UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TotalSize = DiskInfoIn.总大小;
                    AvailableFreeSpace = DiskInfoIn.可用空间;
                    UsedSpace = DiskInfoIn.已用空间;
                    IusedSpace = Utils.GetFolderSize(path);


                    TotalSizeFM = Utils.FormatSize(DiskInfoIn.总大小);
                    AvailableFreeSpaceFM = Utils.FormatSize(DiskInfoIn.可用空间);
                    UsedSpaceFM = Utils.FormatSize(DiskInfoIn.已用空间);
                    IusedSpaceFM = Utils.FormatSize(Utils.GetFolderSize(path));
                });
            });
        }
    }
}