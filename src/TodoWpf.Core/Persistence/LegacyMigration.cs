namespace TodoWpf.Core.Persistence;

public enum MigrationStatus
{
    /// <summary>已把旧档复制到新路径。</summary>
    Migrated,

    /// <summary>新路径已有数据：不迁移（幂等）。</summary>
    SkippedTargetExists,

    /// <summary>旧档不存在：不迁移。</summary>
    SkippedNoLegacy,

    /// <summary>迁移失败（IO 异常等）；调用方按"无旧档"继续，不阻塞启动。</summary>
    Failed,
}

public sealed record MigrationResult(MigrationStatus Status, string? Error);

/// <summary>
/// 旧档自动迁移（修订记录 R3）：复制式（不删原件）、幂等（新档存在则跳过）、失败不抛出。
/// </summary>
public static class LegacyMigration
{
    public static async Task<MigrationResult> MigrateIfNeededAsync(
        IFileIo io,
        string legacyPath,
        string targetPath)
    {
        try
        {
            if (await io.ExistsAsync(targetPath))
            {
                return new MigrationResult(MigrationStatus.SkippedTargetExists, null);
            }

            if (!await io.ExistsAsync(legacyPath))
            {
                return new MigrationResult(MigrationStatus.SkippedNoLegacy, null);
            }

            var text = await io.ReadAllTextAsync(legacyPath);
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                await io.CreateDirectoryAsync(directory);
            }

            await io.WriteAllTextAsync(targetPath, text);
            return new MigrationResult(MigrationStatus.Migrated, null);
        }
        catch (Exception ex)
        {
            return new MigrationResult(MigrationStatus.Failed, ex.Message);
        }
    }
}
