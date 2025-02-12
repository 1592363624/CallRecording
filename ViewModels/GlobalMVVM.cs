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

        [ObservableProperty] public long iusedSpace;

        [ObservableProperty] public string iusedSpaceFM;

        [ObservableProperty] public long totalSize;

        [ObservableProperty] public string totalSizeFM;

        [ObservableProperty] public long usedSpace;

        [ObservableProperty] public string usedSpaceFM;


        [ObservableProperty] public int wt = 500;

        // 计算每个部分的比例（总宽度为 wt）
        public double UsedSpaceProportion => (TotalSize > 0) ? ((double)UsedSpace / TotalSize) * Wt : 0;

        public double AvailableFreeSpaceProportion =>
            (TotalSize > 0) ? ((double)AvailableFreeSpace / TotalSize) * Wt : 0;

        public double IusedSpaceProportion => (TotalSize > 0) ? ((double)IusedSpace / TotalSize) * Wt : 0;

        // 用于计算第二个矩形和第三个矩形的偏移量
        public double TotalUsedProportion => UsedSpaceProportion + AvailableFreeSpaceProportion;


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