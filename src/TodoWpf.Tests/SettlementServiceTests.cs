using TodoWpf.Core.Models;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>跨天结算（需求文档 §2.9）：归档昨日及更早完成、未完成重标逾期、静默、幂等。</summary>
public class SettlementServiceTests
{
    private static readonly DateTimeOffset Today = TestTime.Local(2026, 10, 11, 9);

    [Fact]
    public void Settle_ArchivesOlderCompleted_KeepsTodayCompleted()
    {
        var yesterdayDone = TestItems.Todo(
            "昨天的活", TestTime.Local(2026, 10, 10, 8), completed: TestTime.Local(2026, 10, 10, 9));
        var todayDone = TestItems.Todo(
            "今天的活", TestTime.Local(2026, 10, 11, 8), completed: TestTime.Local(2026, 10, 11, 8, 30));
        var data = new AppData { Completed = [yesterdayDone, todayDone] };

        var result = SettlementService.Settle(data, Today);

        Assert.Equal(1, result.ArchivedCount);
        Assert.True(result.Changed);
        Assert.Equal(todayDone.Id, Assert.Single(result.Data.Completed).Id);
        Assert.Equal(yesterdayDone.Id, Assert.Single(result.Data.Archive["2026-10-10"]).Id);
    }

    [Fact]
    public void Settle_MarksUnfinishedOverdue_KeepsOrder()
    {
        var old = TestItems.Todo("旧活", TestTime.Local(2026, 10, 9, 8));
        var fresh = TestItems.Todo("今活", TestTime.Local(2026, 10, 11, 8));
        var data = new AppData { Todos = [old, fresh] };

        var result = SettlementService.Settle(data, Today);

        Assert.True(result.Data.Todos[0].Overdue);
        Assert.False(result.Data.Todos[1].Overdue);
        Assert.Equal(1, result.MarkedOverdue);
        Assert.Equal("旧活", result.Data.Todos[0].Content);
        Assert.Equal("今活", result.Data.Todos[1].Content);
    }

    [Fact]
    public void Settle_SecondRunSameDay_IsIdempotent()
    {
        var data = new AppData
        {
            Todos = [TestItems.Todo("旧活", TestTime.Local(2026, 10, 9, 8))],
            Completed =
            [
                TestItems.Todo("昨天的活", TestTime.Local(2026, 10, 10, 8), completed: TestTime.Local(2026, 10, 10, 9)),
            ],
        };

        var first = SettlementService.Settle(data, Today);
        var second = SettlementService.Settle(first.Data, Today);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ArchivedCount);
        Assert.Equal(0, second.MarkedOverdue);
        Assert.Equal(first.Data.Todos[0].Overdue, second.Data.Todos[0].Overdue);
        Assert.Equal(first.Data.UiState.LastSettleDate, second.Data.UiState.LastSettleDate);
    }

    [Fact]
    public void Settle_UpdatesLastSettleDate()
    {
        var result = SettlementService.Settle(new AppData(), Today);

        Assert.Equal(LocalDate.Key(Today), result.Data.UiState.LastSettleDate);
        Assert.True(result.Changed); // 从未结算 → 打戳也算变化
    }

    [Fact]
    public void Settle_KeepsDirtyCompletedWithoutTimestamp()
    {
        var dirty = TestItems.Todo("脏数据", TestTime.Local(2026, 10, 8, 8));
        var data = new AppData { Completed = [dirty] };

        var result = SettlementService.Settle(data, Today);

        Assert.Equal(0, result.ArchivedCount);
        Assert.Equal(dirty.Id, Assert.Single(result.Data.Completed).Id);
        Assert.Empty(result.Data.Archive);
    }

    [Fact]
    public void Settle_CountsOnlyFlagChanges()
    {
        // 已是逾期状态（含手工标记）→ 不再计入 MarkedOverdue
        var alreadyOverdue = TestItems.Todo("早已逾期", TestTime.Local(2026, 10, 8, 8), overdue: true);
        var data = new AppData { Todos = [alreadyOverdue] };

        var result = SettlementService.Settle(data, Today);

        Assert.Equal(0, result.MarkedOverdue);
        Assert.True(result.Data.Todos[0].Overdue);
    }

    [Fact]
    public void Settle_AppendsToExistingArchiveBucket_PreservingOrder()
    {
        var earlier = TestItems.Todo(
            "先前归档", TestTime.Local(2026, 10, 10, 7), completed: TestTime.Local(2026, 10, 10, 7, 30));
        var later = TestItems.Todo(
            "本次归档", TestTime.Local(2026, 10, 10, 8), completed: TestTime.Local(2026, 10, 10, 9));
        var data = new AppData
        {
            Completed = [later],
            Archive = new Dictionary<string, List<TodoItem>> { ["2026-10-10"] = [earlier] },
        };

        var result = SettlementService.Settle(data, Today);

        var bucket = result.Data.Archive["2026-10-10"];
        Assert.Equal(2, bucket.Count);
        Assert.Equal(earlier.Id, bucket[0].Id);
        Assert.Equal(later.Id, bucket[1].Id);
    }

    [Fact]
    public void Settle_DoesNotMutateInput()
    {
        var data = new AppData { Todos = [TestItems.Todo("旧活", TestTime.Local(2026, 10, 9, 8))] };
        var beforeOverdue = data.Todos[0].Overdue;
        var beforeLastSettle = data.UiState.LastSettleDate;

        SettlementService.Settle(data, Today);

        Assert.Equal(beforeOverdue, data.Todos[0].Overdue);
        Assert.Equal(beforeLastSettle, data.UiState.LastSettleDate);
    }
}
