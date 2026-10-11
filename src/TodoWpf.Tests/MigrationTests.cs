using TodoWpf.Core.Persistence;
using TodoWpf.Tests.TestSupport;
using IOPath = System.IO.Path;

namespace TodoWpf.Tests;

/// <summary>旧档自动迁移：复制式（不删原件）、幂等、失败不崩。</summary>
public class MigrationTests
{
    private static string LegacyPath => IOPath.Combine("mem-root", "桌面待办", "data.json");

    private static string TargetPath => IOPath.Combine("mem-root", "桌面待办-WPF", "data.json");

    [Fact]
    public async Task Migrate_CopiesWhenTargetMissing_OriginalUntouched()
    {
        var io = new MemoryFileIo();
        io.Put(LegacyPath, """{"v":1,"todos":[]}""");

        var result = await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        Assert.Equal(MigrationStatus.Migrated, result.Status);
        Assert.Null(result.Error);
        Assert.Equal("""{"v":1,"todos":[]}""", io.ContentOf(TargetPath));
        Assert.Equal("""{"v":1,"todos":[]}""", io.ContentOf(LegacyPath));
        Assert.Equal(1, io.CreateDirectoryCount);
    }

    [Fact]
    public async Task Migrate_SkipsWhenTargetExists_TargetContentUntouched()
    {
        var io = new MemoryFileIo();
        io.Put(LegacyPath, """{"v":1,"todos":[{"content":"旧"}]}""");
        io.Put(TargetPath, """{"v":1,"todos":[]}""");

        var result = await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        Assert.Equal(MigrationStatus.SkippedTargetExists, result.Status);
        Assert.Equal("""{"v":1,"todos":[]}""", io.ContentOf(TargetPath));
    }

    [Fact]
    public async Task Migrate_SkipsWhenNoLegacy()
    {
        var io = new MemoryFileIo();

        var result = await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        Assert.Equal(MigrationStatus.SkippedNoLegacy, result.Status);
        Assert.False(io.Has(TargetPath));
    }

    [Fact]
    public async Task Migrate_SecondRun_IsNoOp()
    {
        var io = new MemoryFileIo();
        io.Put(LegacyPath, """{"v":1}""");
        await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        var second = await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        Assert.Equal(MigrationStatus.SkippedTargetExists, second.Status);
    }

    [Fact]
    public async Task Migrate_ReadFailure_ReturnsFailed_DoesNotThrow()
    {
        var io = new MemoryFileIo();
        io.Put(LegacyPath, """{"v":1}""");
        io.FailReadsRemaining = 1;

        var result = await LegacyMigration.MigrateIfNeededAsync(io, LegacyPath, TargetPath);

        Assert.Equal(MigrationStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.False(io.Has(TargetPath));
    }

    [Fact]
    public async Task Preview_MatchesRealDecision_AndIsReadOnly()
    {
        var io = new MemoryFileIo();

        Assert.Equal(
            MigrationStatus.SkippedNoLegacy,
            await LegacyMigration.PreviewAsync(io, LegacyPath, TargetPath));

        io.Put(LegacyPath, """{"v":1}""");

        Assert.Equal(
            MigrationStatus.Migrated,
            await LegacyMigration.PreviewAsync(io, LegacyPath, TargetPath));

        io.Put(TargetPath, """{"v":1}""");

        Assert.Equal(
            MigrationStatus.SkippedTargetExists,
            await LegacyMigration.PreviewAsync(io, LegacyPath, TargetPath));

        Assert.Equal(0, io.WriteCount);
        Assert.Equal(0, io.MoveCount);
        Assert.Equal(0, io.CreateDirectoryCount);
    }
}
