using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Startup;
using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;
using IOPath = System.IO.Path;

namespace TodoWpf.Tests;

/// <summary>启动流程：路径解析→迁移→读取→启动补结算→有变化才落盘。</summary>
public class BootstrapTests
{
    private static string NewPath => IOPath.Combine("mem-root", "桌面待办-WPF", "data.json");

    private static string LegacyPath => IOPath.Combine("mem-root", "桌面待办", "data.json");

    [Fact]
    public async Task Open_FirstRun_NoFilesAtAll_StartsEmptyAndStampsSettleDate()
    {
        var io = new MemoryFileIo();
        var now = TestTime.Local(2026, 10, 11, 9);

        var result = await AppBootstrap.OpenAsync(io, NewPath, LegacyPath, now);

        Assert.True(result.IsFirstRun);
        Assert.Equal(MigrationStatus.SkippedNoLegacy, result.Migration.Status);
        Assert.Empty(result.Data.Todos);
        Assert.Empty(result.Data.Completed);
        Assert.Equal(LocalDate.Key(now), result.Data.UiState.LastSettleDate);
        Assert.True(result.Saved);
        Assert.True(io.Has(NewPath));
    }

    [Fact]
    public async Task Open_LegacyExists_MigratesThenLoads_OriginalKept()
    {
        var io = new MemoryFileIo();
        var legacyText = Fixtures.Text("full-data.json");
        io.Put(LegacyPath, legacyText);
        var now = TestTime.Local(2026, 10, 11, 9);

        var result = await AppBootstrap.OpenAsync(io, NewPath, LegacyPath, now);

        Assert.Equal(MigrationStatus.Migrated, result.Migration.Status);
        Assert.False(result.IsFirstRun);
        Assert.Equal(2, result.Data.Todos.Count);
        Assert.Equal("写周报", result.Data.Todos[0].Content);
        Assert.True(io.Has(LegacyPath));
        Assert.Equal(legacyText, io.ContentOf(LegacyPath));
        Assert.True(io.Has(NewPath));
    }

    [Fact]
    public async Task Open_AlreadySettledToday_NoChanges_DoesNotWrite()
    {
        var io = new MemoryFileIo();
        var now = TestTime.Local(2026, 10, 11, 9);
        var settled = Defaults.DefaultAppData() with
        {
            Todos = [TestItems.Todo("今天的活", TestTime.Local(2026, 10, 11, 8))],
            UiState = Defaults.DefaultUiState() with { LastSettleDate = LocalDate.Key(now) },
        };
        io.Put(NewPath, JsonCodec.Write(settled));

        var result = await AppBootstrap.OpenAsync(io, NewPath, LegacyPath, now);

        Assert.False(result.IsFirstRun);
        Assert.False(result.Settle.Changed);
        Assert.False(result.Saved);
        Assert.Equal(0, io.WriteCount);
    }

    [Fact]
    public async Task Open_MissedDays_CatchUpSettlesAndPersists()
    {
        var io = new MemoryFileIo();
        var now = TestTime.Local(2026, 10, 11, 9);
        var completedAt = TestTime.Local(2026, 10, 9, 9);
        var stale = Defaults.DefaultAppData() with
        {
            Todos = [TestItems.Todo("忘了完成的活", TestTime.Local(2026, 10, 8, 8))],
            Completed = [TestItems.Todo("前天完成的活", TestTime.Local(2026, 10, 9, 8), completed: completedAt)],
            UiState = Defaults.DefaultUiState() with { LastSettleDate = "2026-10-08" },
        };
        io.Put(NewPath, JsonCodec.Write(stale));

        var result = await AppBootstrap.OpenAsync(io, NewPath, LegacyPath, now);

        Assert.True(result.Saved);
        Assert.Equal(1, result.Settle.ArchivedCount);
        Assert.Equal(1, result.Settle.MarkedOverdue);
        Assert.True(result.Data.Todos[0].Overdue);
        Assert.Equal(LocalDate.Key(now), result.Data.UiState.LastSettleDate);
        Assert.Equal(LocalDate.Key(completedAt), result.Settle.Data.Archive.Keys.Single());

        var persisted = JsonCodec.Read(io.ContentOf(NewPath)!);
        Assert.True(persisted.Todos[0].Overdue);
        Assert.Single(persisted.Archive[LocalDate.Key(completedAt)]);
        Assert.Empty(persisted.Completed);
    }

    [Fact]
    public async Task Open_TargetExists_LegacyIgnored_NoMigration()
    {
        var io = new MemoryFileIo();
        var now = TestTime.Local(2026, 10, 11, 9);
        io.Put(NewPath, JsonCodec.Write(Defaults.DefaultAppData() with
        {
            UiState = Defaults.DefaultUiState() with { LastSettleDate = LocalDate.Key(now) },
        }));
        io.Put(LegacyPath, Fixtures.Text("full-data.json"));

        var result = await AppBootstrap.OpenAsync(io, NewPath, LegacyPath, now);

        Assert.Equal(MigrationStatus.SkippedTargetExists, result.Migration.Status);
        Assert.Empty(result.Data.Todos);
    }
}
