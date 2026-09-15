using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using CallRecording.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySharedProject.Model;
using Newtonsoft.Json;
using NLog;

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

        [ObservableProperty] public bool _isLog = false;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 监控窗口列表：每一项对应一个可监控窗口（含可选的尺寸检测配置）。
        /// 任何变更（增删/属性修改）都会自动写回 appsettings.json，并通过
        /// <see cref="MonitorConfigChanged"/> 通知服务重建窗口监听。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MonitoredWindow> monitoredWindows = new();

        /// <summary>
        /// 监控相关配置变更后触发，由 MainViewModel 订阅并重建窗口监听
        /// </summary>
        public static event EventHandler? MonitorConfigChanged;

        public GlobalMVVM()
        {
            // 订阅集合变更：增删时挂/解绑子项属性变化，触发持久化与服务重建
            MonitoredWindows.CollectionChanged += OnMonitoredWindowsCollectionChanged;
        }

        /// <summary>
        /// 从 appsettings.json 读取监控窗口列表。
        /// 兼容旧版：若新 key 不存在，从 监控窗口进程名/类名/标题 + 微信通话窗口宽/高 + 是否启用大小检测 自动迁移。
        /// </summary>
        public void LoadMonitoredWindows()
        {
            string raw = ConfigurationHelper.GetSetting("监控窗口列表");
            List<MonitoredWindow> loaded;

            if (!string.IsNullOrWhiteSpace(raw) && raw != "NULL")
            {
                try
                {
                    loaded = JsonConvert.DeserializeObject<List<MonitoredWindow>>(raw) ?? new List<MonitoredWindow>();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "反序列化监控窗口列表失败，回退到默认配置");
                    loaded = BuildDefaultList();
                }
            }
            else
            {
                loaded = MigrateFromLegacyConfig();
            }

            if (loaded.Count == 0)
            {
                loaded = BuildDefaultList();
            }

            ReplaceMonitoredWindows(loaded);
        }

        /// <summary>
        /// 把当前 MonitoredWindows 列表持久化到 appsettings.json，并通知服务重建。
        /// </summary>
        private void PersistAndRaiseChanged()
        {
            try
            {
                string json = JsonConvert.SerializeObject(MonitoredWindows.ToList(), Formatting.None);
                ConfigurationHelper.SetSetting("监控窗口列表", json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存监控窗口列表失败");
            }

            MonitorConfigChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 把外部传入的列表替换到当前 ObservableCollection（先解绑旧项，再绑定新项）。
        /// </summary>
        private void ReplaceMonitoredWindows(IEnumerable<MonitoredWindow> items)
        {
            // 解绑旧项
            foreach (var old in MonitoredWindows)
            {
                old.PropertyChanged -= OnMonitoredWindowItemChanged;
            }

            // 加载时自动去重：保留第一次出现的条目，丢弃后续重复项
            var seen = new HashSet<string>();
            var deduped = new List<MonitoredWindow>();
            foreach (var item in items)
            {
                if (item == null) continue;
                string key = BuildDedupeKey(item);
                if (seen.Add(key))
                {
                    deduped.Add(item);
                }
                else
                {
                    Logger.Info($"加载时已过滤重复监控条目: {item.DisplayName}");
                }
            }

            MonitoredWindows.Clear();
            foreach (var item in deduped)
            {
                MonitoredWindows.Add(item);
            }

            // 订阅新项
            foreach (var item in MonitoredWindows)
            {
                item.PropertyChanged += OnMonitoredWindowItemChanged;
            }

            // 触发一次刷新（确保服务至少初始化一次）
            MonitorConfigChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnMonitoredWindowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MonitoredWindow item in e.NewItems)
                {
                    item.PropertyChanged += OnMonitoredWindowItemChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (MonitoredWindow item in e.OldItems)
                {
                    item.PropertyChanged -= OnMonitoredWindowItemChanged;
                }
            }

            PersistAndRaiseChanged();
        }

        private void OnMonitoredWindowItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 任一字段修改（宽高/勾选/进程名等）都触发持久化与服务重建
            PersistAndRaiseChanged();
        }

        // 计算每个部分的比例（总宽度为 wt）
        public double UsedSpaceProportion => (TotalSize > 0) ? ((double)UsedSpace / TotalSize) * Wt : 0;

        public double AvailableFreeSpaceProportion =>
            (TotalSize > 0) ? ((double)AvailableFreeSpace / TotalSize) * Wt : 0;

        public double IusedSpaceProportion => (TotalSize > 0) ? ((double)IusedSpace / TotalSize) * Wt : 0;

        // 用于计算第二个矩形和第三个矩形的偏移量
        public double TotalUsedProportion => UsedSpaceProportion + AvailableFreeSpaceProportion;

        /// <summary>
        /// 默认监控列表：内置微信/企业微信/QQNT 的常见组合。
        /// 微信条目默认开启大小检测并带上 360x640 的兜底值；如实际窗口尺寸不一致，用户可重新校准或留空。
        /// </summary>
        private List<MonitoredWindow> BuildDefaultList()
        {
            return new List<MonitoredWindow>
            {
                new MonitoredWindow
                {
                    ProcessName = "WeChat|Weixin",
                    ClassName = "AudioWnd|ILinkAudioWnd|Qt51514QWindowIcon",
                    Title = "语音|微信音视频通话|微信",
                    Width = 360,
                    Height = 640,
                    SizeCheckEnabled = false,
                },
                new MonitoredWindow
                {
                    ProcessName = "WXWork",
                    ClassName = "WXworkWindow",
                    Title = "语音",
                },
                new MonitoredWindow
                {
                    ProcessName = "QQ",
                    ClassName = "Chrome_RenderWidgetHostHWND",
                    Title = "语音",
                },
            };
        }

        /// <summary>
        /// 从旧版三个 string + 微信宽高 + 全局启用开关 迁移成新结构。
        /// 旧 key 全部保留不动，新结构写入「监控窗口列表」。
        /// </summary>
        private List<MonitoredWindow> MigrateFromLegacyConfig()
        {
            string legacyPn = ConfigurationHelper.GetSetting("监控窗口进程名");
            string legacyCn = ConfigurationHelper.GetSetting("监控窗口类名");
            string legacyTt = ConfigurationHelper.GetSetting("监控窗口标题");

            bool.TryParse(ConfigurationHelper.GetSetting("是否启用微信窗口大小检测"), out bool legacyEnabled);
            int.TryParse(ConfigurationHelper.GetSetting("微信通话窗口宽度"), out int legacyW);
            int.TryParse(ConfigurationHelper.GetSetting("微信通话窗口高度"), out int legacyH);

            int? legacyWidth = legacyW > 0 ? legacyW : (int?)null;
            int? legacyHeight = legacyH > 0 ? legacyH : (int?)null;

            // 把旧格式按 '|' 拆分，过滤掉"要监控的窗口xx"这种提示后缀
            string[] pnArr = (legacyPn ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !s.StartsWith("要监控")).ToArray();
            string[] cnArr = (legacyCn ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !s.StartsWith("要监控")).ToArray();
            string[] ttArr = (legacyTt ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !s.StartsWith("要监控")).ToArray();

            // 按进程数取最大长度，每条独立成一项（与旧行为兼容：旧版就是按位对应）
            int count = Math.Max(Math.Max(pnArr.Length, cnArr.Length), ttArr.Length);
            var list = new List<MonitoredWindow>();

            for (int i = 0; i < count; i++)
            {
                string pn = i < pnArr.Length ? pnArr[i] : string.Empty;
                string cn = i < cnArr.Length ? cnArr[i] : string.Empty;
                string tt = i < ttArr.Length ? ttArr[i] : string.Empty;

                // 跳过全空的占位
                if (string.IsNullOrWhiteSpace(pn) && string.IsNullOrWhiteSpace(cn) && string.IsNullOrWhiteSpace(tt))
                {
                    continue;
                }

                bool isWeixin = pn.Contains("Weixin", StringComparison.OrdinalIgnoreCase);
                list.Add(new MonitoredWindow
                {
                    ProcessName = pn,
                    ClassName = cn,
                    Title = tt,
                    Width = isWeixin ? legacyWidth : null,
                    Height = isWeixin ? legacyHeight : null,
                    SizeCheckEnabled = isWeixin && legacyEnabled && legacyWidth.HasValue && legacyHeight.HasValue,
                });
            }

            if (list.Count == 0)
            {
                list = BuildDefaultList();
            }

            return list;
        }

        [RelayCommand]
        private void AddMonitoredWindow()
        {
            var item = new MonitoredWindow
            {
                ProcessName = string.Empty,
                ClassName = string.Empty,
                Title = string.Empty,
                SizeCheckEnabled = false,
            };
            TryAddMonitoredWindow(item, out _, showDuplicateMessage: true);
        }

        [RelayCommand]
        private void RemoveMonitoredWindow(MonitoredWindow? item)
        {
            if (item == null) return;
            MonitoredWindows.Remove(item);
        }

        [RelayCommand]
        private void ClearMonitoredWindows()
        {
            if (MonitoredWindows.Count == 0) return;
            var result = MessageBox.Show(
                "确定要清空所有监控窗口吗？该操作不可撤销。",
                "清空监控列表",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                MonitoredWindows.Clear();
            }
        }

        /// <summary>
        /// 试图把 <paramref name="item"/> 加入到监控列表。若已有完全相同的条目（含宽高/启用检测），
        /// 则不会重复加入；可通过 <paramref name="showDuplicateMessage"/> 决定是否弹提示。
        /// </summary>
        public bool TryAddMonitoredWindow(MonitoredWindow item, out MonitoredWindow? duplicate, bool showDuplicateMessage = true)
        {
            duplicate = null;
            if (item == null) return false;

            string newKey = BuildDedupeKey(item);
            foreach (var existing in MonitoredWindows)
            {
                if (BuildDedupeKey(existing) == newKey)
                {
                    duplicate = existing;
                    if (showDuplicateMessage)
                    {
                        MessageBox.Show(
                            $"该窗口已存在监控列表里，无需重复添加:\n{existing.DisplayName}",
                            "添加监控窗口",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    return false;
                }
            }

            MonitoredWindows.Add(item);
            return true;
        }

        /// <summary>
        /// 构造用于去重比较的 key：进程名/类名/标题/宽/高/启用检测 全部一致才算重复。
        /// 字符串字段忽略大小写并 trim；数值字段直接用字符串拼接以兼容 null。
        /// </summary>
        private static string BuildDedupeKey(MonitoredWindow w)
        {
            return string.Join("|",
                (w.ProcessName ?? string.Empty).Trim().ToLowerInvariant(),
                (w.ClassName ?? string.Empty).Trim().ToLowerInvariant(),
                (w.Title ?? string.Empty).Trim().ToLowerInvariant(),
                w.Width.HasValue ? w.Width.Value.ToString() : "null",
                w.Height.HasValue ? w.Height.Value.ToString() : "null",
                w.SizeCheckEnabled ? "1" : "0");
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

        public static class LogLevelOptions
        {
            public static readonly LogLevel[] Levels = new[]
            {
                LogLevel.Off,
                LogLevel.Info,
                LogLevel.Debug
            };
        }


        public partial class LogViewModel : ObservableObject
        {
            public ObservableCollection<LogLevel> LogLevels { get; } =
                new ObservableCollection<LogLevel>(new[] { LogLevel.Off, LogLevel.Info, LogLevel.Debug });

            [ObservableProperty] private LogLevel selectedLogLevel = LogLevel.Info;

            partial void OnSelectedLogLevelChanged(LogLevel value)
            {
                Utils.SetGlobalLogLevel(value); // 参数类型完全一致
            }
        }
    }
}