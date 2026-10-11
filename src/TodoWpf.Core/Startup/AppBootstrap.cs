using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Services;

namespace TodoWpf.Core.Startup;

/// <summary>启动结果：读入的数据 + 迁移/首启/补结算/落盘情况。</summary>
public sealed record StartupResult(
    AppData Data,
    MigrationResult Migration,
    bool IsFirstRun,
    SettleResult Settle,
    bool Saved);

/// <summary>
/// 启动流程（纯 C#，无 UI 依赖）：路径解析 → 旧档迁移（复制式、幂等）→ 读取归一化
/// → 跨天补结算（关机跨天场景）→ 有变化才写盘。
/// </summary>
public static class AppBootstrap
{
    public static async Task<StartupResult> OpenAsync(
        IFileIo io,
        string newDataPath,
        string legacyDataPath,
        DateTimeOffset now,
        int debounceMs = 0)
    {
        var migration = await LegacyMigration.MigrateIfNeededAsync(io, legacyDataPath, newDataPath);

        var repository = new JsonFileRepository(newDataPath, io, debounceMs);
        var loaded = await repository.LoadAsync();
        var isFirstRun = loaded is null;
        var data = loaded ?? Defaults.DefaultAppData();

        var settle = SettlementService.Settle(data, now);

        var saved = false;
        if (settle.Changed)
        {
            await repository.SaveAsync(settle.Data);
            await repository.FlushAsync();
            saved = true;
        }

        return new StartupResult(settle.Data, migration, isFirstRun, settle, saved);
    }
}
