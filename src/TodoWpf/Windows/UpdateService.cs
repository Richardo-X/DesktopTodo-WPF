using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// C4 更新服务（Velopack 封装）：更新源 = **GitHub Releases**（公开发布仓库）。
///
/// 为什么必须是公网源（2026-11-09 纠正）：本应用是**发布给其他用户**的软件，
/// 早先选的"本机 releases 目录"只在开发者本机有效——其他用户访问不到那台机器的磁盘，
/// 等于永远收不到更新。私有仓库同样不行（Release 资源需鉴权，其他用户拉不到），故仓库必须公开。
///
/// 检查失败抛异常——调用方决定呈现（后台启动检查静默 / 设置页手动检查明确报错）。
/// </summary>
public sealed class UpdateService
{
    /// <summary>更新源仓库（公开发布仓库；换地址只改这一处）。</summary>
    public const string RepoUrl = "https://github.com/Richardo-X/DesktopTodo-WPF";

    /// <summary>当前运行的应用版本（取 exe 的 ProductVersion，截掉 "+源哈希" 后缀；取不到按 0.0.0）。</summary>
    public static string CurrentVersion()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            var fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).ProductVersion;
            if (!string.IsNullOrWhiteSpace(fv))
            {
                var plus = fv.IndexOf('+');
                return plus >= 0 ? fv[..plus] : fv;
            }
        }

        return "0.0.0";
    }

    /// <summary>检测到的新版本信息。</summary>
    public sealed record UpdateAvailable(string Version, string Notes);

    /// <summary>更新管理器（公开仓库无需访问令牌）。</summary>
    private static Velopack.UpdateManager CreateManager() =>
        new(new Velopack.Sources.GithubSource(RepoUrl, null, false));

    /// <summary>检查更新；无新版本返回 null（Velopack 按清单最新版与当前版本比较）。</summary>
    public async Task<UpdateAvailable?> CheckAsync()
    {
        var mgr = CreateManager();
        var info = await mgr.CheckForUpdatesAsync();
        if (info is null)
        {
            return null;
        }

        // 更新说明来自 GitHub Release 正文（NotesMarkdown）；没有正文就留空，弹窗只显示版本号。
        var asset = info.TargetFullRelease;
        return new UpdateAvailable(asset.Version.ToString(), asset.NotesMarkdown ?? "");
    }

    /// <summary>下载并应用更新，随后由 Velopack 重启进程接管（旧实例退出、新实例起来）。</summary>
    public async Task UpdateAndRestartAsync()
    {
        var mgr = CreateManager();
        var info = await mgr.CheckForUpdatesAsync();
        if (info is null)
        {
            return;
        }

        await mgr.DownloadUpdatesAsync(info);
        mgr.ApplyUpdatesAndRestart(info);
    }
}
