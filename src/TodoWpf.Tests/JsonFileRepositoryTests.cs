using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;
using IOPath = System.IO.Path;

namespace TodoWpf.Tests;

/// <summary>文件仓储：原子写（.tmp→替换）、防抖合并+Flush、损坏留档、写失败保留重试。</summary>
public class JsonFileRepositoryTests
{
    private static string DataFile => IOPath.Combine("mem-root", "桌 面待办-WPF", "data.json");

    private static (JsonFileRepository Repo, MemoryFileIo Io) NewRepo(int debounceMs = 0)
    {
        var io = new MemoryFileIo();
        return (new JsonFileRepository(DataFile, io, debounceMs), io);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsNull()
    {
        var (repo, _) = NewRepo();

        Assert.Null(await repo.LoadAsync());
    }

    [Fact]
    public async Task Load_CorruptJson_ArchivesAsCorruptFile_AndReturnsNull()
    {
        var (repo, io) = NewRepo();
        io.Put(DataFile, "{ not json");

        var data = await repo.LoadAsync();

        Assert.Null(data);
        Assert.False(io.Has(DataFile));
        var archived = io.FindPathStartingWith(DataFile + ".corrupt-");
        Assert.NotNull(archived);
        Assert.Equal("{ not json", io.ContentOf(archived!));
    }

    [Fact]
    public async Task Load_ValidButNonObjectJson_ReturnsDefaults_NotCorrupt()
    {
        var (repo, io) = NewRepo();
        io.Put(DataFile, "[]");

        var data = await repo.LoadAsync();

        Assert.NotNull(data);
        Assert.Empty(data!.Todos);
        Assert.True(io.Has(DataFile));
        Assert.Null(io.FindPathStartingWith(DataFile + ".corrupt-"));
    }

    [Fact]
    public async Task Save_ImmediateMode_WritesTmpThenReplaces()
    {
        var (repo, io) = NewRepo(debounceMs: 0);

        await repo.SaveAsync(Defaults.DefaultAppData() with
        {
            Templates = [ItemFactory.CreateTemplateItem("站会", TestTime.Local(2026, 10, 11, 9))],
        });

        Assert.Equal(1, io.WriteCount);
        Assert.Equal(2, io.Operations.Count);
        Assert.StartsWith("write:", io.Operations[0]);
        Assert.Contains(".tmp", io.Operations[0]);
        Assert.StartsWith("move:", io.Operations[1]);
        Assert.Contains(".tmp", io.Operations[1]);
        Assert.Equal(1, io.CreateDirectoryCount);
        Assert.Null(io.FindPathStartingWith(DataFile + ".tmp")); // 临时文件已移走
        Assert.Equal("站会", JsonCodec.Read(io.ContentOf(DataFile)!).Templates[0].Content);
    }

    [Fact]
    public async Task Save_Debounce_CoalescesWrites_UntilFlush()
    {
        var (repo, io) = NewRepo(debounceMs: 60_000);

        await repo.SaveAsync(Defaults.DefaultAppData());
        await repo.SaveAsync(Defaults.DefaultAppData() with
        {
            Templates = [ItemFactory.CreateTemplateItem("稍后", TestTime.Local(2026, 10, 11, 9))],
        });

        Assert.Equal(0, io.WriteCount);

        await repo.FlushAsync();

        Assert.Equal(1, io.WriteCount);
        Assert.Equal("稍后", JsonCodec.Read(io.ContentOf(DataFile)!).Templates[0].Content);
    }

    [Fact]
    public async Task Save_Debounce_AutoWritesAfterDelay()
    {
        var (repo, io) = NewRepo(debounceMs: 25);

        await repo.SaveAsync(Defaults.DefaultAppData());

        Assert.True(await TestAsync.WaitUntilAsync(() => io.WriteCount == 1));
        Assert.True(io.Has(DataFile));
    }

    [Fact]
    public async Task Flush_WithoutPendingWrite_DoesNothing()
    {
        var (repo, io) = NewRepo(debounceMs: 60_000);

        await repo.FlushAsync();

        Assert.Equal(0, io.WriteCount);
    }

    [Fact]
    public async Task Save_WriteFailure_KeepsPending_RetriesOnFlush()
    {
        var (repo, io) = NewRepo(debounceMs: 0);
        io.FailWritesRemaining = 1;

        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(Defaults.DefaultAppData()));
        Assert.Equal(0, io.WriteCount);
        Assert.False(io.Has(DataFile));

        await repo.FlushAsync();

        Assert.Equal(1, io.WriteCount);
        Assert.True(io.Has(DataFile));
        Assert.Equal(1, JsonCodec.Read(io.ContentOf(DataFile)!).V);
    }

    [Fact]
    public async Task Load_ThenFlush_RoundTripsThroughRepository()
    {
        var (repo, io) = NewRepo(debounceMs: 0);
        var now = TestTime.Local(2026, 10, 11, 9);
        await repo.SaveAsync(Defaults.DefaultAppData() with
        {
            Todos = [ItemFactory.CreateTodoItem("持久化验证", now)],
        });

        var reloaded = await repo.LoadAsync();

        Assert.NotNull(reloaded);
        Assert.Equal("持久化验证", reloaded!.Todos[0].Content);
        Assert.Equal(IsoTime.Format(now), reloaded.Todos[0].CreatedAt);
    }
}
