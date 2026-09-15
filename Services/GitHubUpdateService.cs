using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using MySharedProject.Model;
using Newtonsoft.Json;

namespace CallRecording.Services;

/// <summary>
/// 通过 GitHub Releases 检测是否有新版本。
/// 默认仓库：https://github.com/1592363624/CallRecording/releases
///
/// 数据源策略（按顺序尝试，第一个成功的胜出）：
///   1) 用户自定义镜像（配置项 "GitHub镜像"，用 | 分隔多个 URL）
///   2) 内置 GitHub 直连（Atom feed）
///   3) 内置常见国内镜像（kkgithub、gh-proxy）
///   4) GitHub REST API（兜底，受 60/h 匿名速率限制）
///
/// 对国内/网络受限用户友好：把可用的镜像放进 appsettings.json 的 "GitHub镜像" 即可。
/// </summary>
public static class GitHubUpdateService
{
    public const string RepositoryOwner = "1592363624";
    public const string RepositoryName = "CallRecording";

    /// <summary>Release 列表页（用户点击通知后会跳转到这里）。</summary>
    public const string ReleasesPageUrl =
        $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases";

    // Atom feed 用的命名空间常量（GitHub atom feed 是标准的 Atom 1.0）
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";

    private static readonly HttpClient HttpClient = new()
    {
        // GitHub REST API 要求必须带 User-Agent 头；Atom Feed 不强制，但加上也无害
        DefaultRequestHeaders =
        {
            { "User-Agent", "CallRecording-Updater" }
        },
        Timeout = TimeSpan.FromSeconds(20)
    };

    /// <summary>
    /// GitHub release 返回 JSON 中我们关心的几个字段。
    /// </summary>
    public class GitHubRelease
    {
        [JsonProperty("tag_name")] public string? TagName { get; set; }
        [JsonProperty("name")] public string? Name { get; set; }
        [JsonProperty("body")] public string? Body { get; set; }
        [JsonProperty("html_url")] public string? HtmlUrl { get; set; }
        [JsonProperty("prerelease")] public bool Prerelease { get; set; }
        [JsonProperty("draft")] public bool Draft { get; set; }
    }

    /// <summary>
    /// 拉取最新 release 信息。失败时返回 null。
    /// 策略：按顺序尝试多个镜像源，第一个能访问并解析成功的胜出。
    /// </summary>
    public static async Task<GitHubRelease?> GetLatestReleaseAsync()
    {
        var urls = GetCandidateUrls();
        var tried = new List<string>();

        foreach (var url in urls)
        {
            tried.Add(url);
            try
            {
                GitHubRelease? release;
                if (IsAtomUrl(url))
                {
                    var xml = await HttpClient.GetStringAsync(url);
                    release = ParseLatestFromAtom(xml);
                }
                else
                {
                    var json = await HttpClient.GetStringAsync(url);
                    release = JsonConvert.DeserializeObject<GitHubRelease>(json);
                }

                if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
                {
                    if (string.IsNullOrWhiteSpace(release.HtmlUrl))
                    {
                        release.HtmlUrl = ReleasesPageUrl;
                    }
                    Debug.WriteLine($"[GitHubUpdateService] 命中源：{url}");
                    return release;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitHubUpdateService] 源失败 [{url}]: {ex.Message}");
            }
        }

        Debug.WriteLine($"[GitHubUpdateService] 所有源（共 {tried.Count} 个）都失败：\n  " +
                        string.Join("\n  ", tried));
        return null;
    }

    /// <summary>
    /// 拼装候选 URL 列表：用户自定义镜像 → 内置默认（含 REST API 兜底）。
    /// </summary>
    private static List<string> GetCandidateUrls()
    {
        var urls = new List<string>();

        // 1) 用户自定义镜像（appsettings.json 里 "GitHub镜像"，多个用 | 分隔）
        var custom = ConfigurationHelper.GetSetting("GitHub镜像");
        if (!string.IsNullOrWhiteSpace(custom) && custom != "NULL")
        {
            foreach (var u in custom.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = u.Trim();
                if (!string.IsNullOrEmpty(t))
                {
                    urls.Add(t);
                }
            }
        }

        // 2) 内置默认源
        urls.Add($"https://github.com/{RepositoryOwner}/{RepositoryName}/releases.atom");       // GitHub 直接（首选，无速率限制）
        urls.Add($"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");        // REST API 兜底（有 60/h 限速）

        // 注：之前内置的 kkgithub.com / gh-proxy.com / mirror.ghproxy.com 等"国内镜像"
        // 在多次实测中（2026-09）已无法访问或 SSL 握手失败，故不内置。如有可用的私有/小众镜像，
        // 请在 appsettings.json 的 "GitHub镜像" 配置项里填写（多个用 | 分隔）。

        return urls;
    }

    private static bool IsAtomUrl(string url)
    {
        return url.EndsWith(".atom", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/releases.atom", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从 Atom feed XML 中解析第一条 title 能解析为版本号的 entry
    /// （跳过 "Latest Build" 这类持续构建产物）。
    /// </summary>
    private static GitHubRelease? ParseLatestFromAtom(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            if (doc.Root == null) return null;

            XElement? entry = null;
            string? tagName = null;
            foreach (var e in doc.Root.Elements(AtomNs + "entry"))
            {
                var t = e.Element(AtomNs + "title")?.Value?.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (TryParseTagAsVersion(t) != null)
                {
                    entry = e;
                    tagName = t;
                    break;
                }
            }
            if (entry == null || tagName == null) return null;

            var htmlUrl = entry.Elements(AtomNs + "link")
                .FirstOrDefault(x => (string?)x.Attribute("rel") == "alternate")?.Attribute("href")?.Value;

            var content = entry.Element(AtomNs + "content")?.Value;

            return new GitHubRelease
            {
                TagName = tagName,
                Name = tagName,
                HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? ReleasesPageUrl : htmlUrl,
                Body = content,
                Prerelease = false,
                Draft = false
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GitHubUpdateService] 解析 Atom XML 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将 tag（例如 "v3.3" 或 "3.3"）解析为 <see cref="Version"/>。
    /// 解析失败时返回 null。
    /// </summary>
    public static Version? TryParseTagAsVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var cleaned = tag.Trim().TrimStart('v', 'V').Trim();

        return Version.TryParse(cleaned, out var version) ? version : null;
    }
}