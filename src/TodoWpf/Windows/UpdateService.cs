using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// C4 更新服务（Velopack 封装）：更新源 = 本机 releases 目录（定稿：零联网；将来换网络源只改此处）。
/// 检查失败抛异常——调用方决定呈现（后台启动检查静默 / 设置页手动检查明确报错）。
/// </summary>
public sealed class UpdateService
{
    /// <summary>更新源：本机 releases 目录（publish-release.ps1 产出）。</summary>
    public const string SourcePath = @"F:\AI\VBcode\project\todo-wpf\releases";

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

    /// <summary>检查更新；无新版本返回 null（Velopack 按清单最新版与当前版本比较）。</summary>
    public async Task<UpdateAvailable?> CheckAsync()
    {
        var mgr = new Velopack.UpdateManager(SourcePath);
        var info = await mgr.CheckForUpdatesAsync();
        if (info is null)
        {
            return null;
        }

        return new UpdateAvailable(info.TargetFullRelease.Version.ToString(), await ReadNotesAsync());
    }

    /// <summary>下载并应用更新，随后由 Velopack 重启进程接管（旧实例退出、新实例起来）。</summary>
    public async Task UpdateAndRestartAsync()
    {
        var mgr = new Velopack.UpdateManager(SourcePath);
        var info = await mgr.CheckForUpdatesAsync();
        if (info is null)
        {
            return;
        }

        await mgr.DownloadUpdatesAsync(info);
        mgr.ApplyUpdatesAndRestart(info);
    }

    private static async Task<string> ReadNotesAsync()
    {
        try
        {
            return await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(SourcePath, "RELEASE-NOTES.txt"));
        }
        catch
        {
            return "";
        }
    }
}
