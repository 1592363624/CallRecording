using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace CallRecording.Models;

/// <summary>
/// 监控窗口配置项。对应监控列表中的一行：进程名 / 窗口类名 / 窗口标题 三元组匹配，
/// 可选地开启窗口大小检测以避免误识别主窗口。
/// 进程名/类名/标题 支持使用 '|' 分隔多个候选项，按 Contains 模糊匹配。
/// </summary>
public partial class MonitoredWindow : ObservableObject
{
    /// <summary>窗口进程名（不含 .exe），支持 '|' 分隔多个</summary>
    [ObservableProperty]
    [JsonProperty("process")]
    private string processName;

    /// <summary>窗口类名，支持 '|' 分隔多个</summary>
    [ObservableProperty]
    [JsonProperty("class")]
    private string className;

    /// <summary>窗口标题，支持 '|' 分隔多个</summary>
    [ObservableProperty]
    [JsonProperty("title")]
    private string title;

    /// <summary>窗口客户区宽度（短边）。为 null 表示不参与大小检测</summary>
    [ObservableProperty]
    [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)]
    private int? width;

    /// <summary>窗口客户区高度（长边）。为 null 表示不参与大小检测</summary>
    [ObservableProperty]
    [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)]
    private int? height;

    /// <summary>是否启用窗口大小检测。仅当 Width/Height 都填写且本项为 true 时生效</summary>
    [ObservableProperty]
    [JsonProperty("sizeCheck")]
    private bool sizeCheckEnabled;

    /// <summary>该条目的简短描述，用于在 UI / 日志中展示</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ProcessName)) parts.Add(ProcessName);
            if (!string.IsNullOrWhiteSpace(ClassName)) parts.Add(ClassName);
            if (!string.IsNullOrWhiteSpace(Title)) parts.Add(Title);
            return parts.Count > 0 ? string.Join(" | ", parts) : "(空)";
        }
    }

    /// <summary>返回该项需要的尺寸检测描述，空表示不检测</summary>
    [JsonIgnore]
    public string SizeCheckSummary
    {
        get
        {
            if (!SizeCheckEnabled) return "未启用";
            if (!Width.HasValue || !Height.HasValue) return "未启用(宽高未填)";
            return $"{Width}x{Height}";
        }
    }
}