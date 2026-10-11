using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>逾期判定统一口径（需求文档 §2.9）：未完成=创建日≠今天；已完成=创建日≠完成日；退回重算。</summary>
public class OverdueServiceTests
{
    private static readonly DateTimeOffset Now = TestTime.Local(2026, 10, 11, 10);

    private static string Today => LocalDate.Key(Now);

    [Fact]
    public void Active_CreatedToday_NotOverdue()
    {
        var item = TestItems.Todo("今天的活", TestTime.Local(2026, 10, 11, 8));

        Assert.False(OverdueService.IsActiveOverdue(item, Today));
    }

    [Fact]
    public void Active_CreatedYesterday_Overdue()
    {
        var item = TestItems.Todo("昨天的活", TestTime.Local(2026, 10, 10, 8));

        Assert.True(OverdueService.IsActiveOverdue(item, Today));
    }

    [Fact]
    public void Active_CompletedItem_NeverActiveOverdue()
    {
        var item = TestItems.Todo(
            "已完成", TestTime.Local(2026, 10, 1, 8), completed: TestTime.Local(2026, 10, 2, 9));

        Assert.False(OverdueService.IsActiveOverdue(item, Today));
    }

    [Fact]
    public void Completed_SameLocalDay_NotOverdue()
    {
        var item = TestItems.Todo(
            "当天完成", TestTime.Local(2026, 10, 11, 8), completed: TestTime.Local(2026, 10, 11, 23));

        Assert.False(OverdueService.IsCompletedOverdue(item));
    }

    [Fact]
    public void Completed_CrossDay_Overdue()
    {
        var item = TestItems.Todo(
            "跨天完成", TestTime.Local(2026, 10, 10, 23, 30), completed: TestTime.Local(2026, 10, 11, 8));

        Assert.True(OverdueService.IsCompletedOverdue(item));
    }

    [Fact]
    public void Completed_NotCompleted_NeverCompletedOverdue()
    {
        var item = TestItems.Todo("未完成", TestTime.Local(2026, 10, 1, 8));

        Assert.False(OverdueService.IsCompletedOverdue(item));
    }

    [Fact]
    public void Recalc_PreservesOrder_AndOnlyTouchesUnfinished()
    {
        var yesterday = TestItems.Todo("昨天的活", TestTime.Local(2026, 10, 10, 8));
        var today = TestItems.Todo("今天的活", TestTime.Local(2026, 10, 11, 8));
        var done = TestItems.Todo(
            "完成过的", TestTime.Local(2026, 10, 1, 8), overdue: true, completed: TestTime.Local(2026, 10, 2, 9));

        var result = OverdueService.RecalcActiveOverdue([yesterday, today, done], Today);

        Assert.Equal(3, result.Count);
        Assert.True(result[0].Overdue);
        Assert.False(result[1].Overdue);
        Assert.True(result[2].Overdue); // 已完成项原样保留（不按今天口径改）
        Assert.Equal("昨天的活", result[0].Content);
        Assert.Equal("今天的活", result[1].Content);
        Assert.Equal("完成过的", result[2].Content);
    }

    [Fact]
    public void MarkCompleted_SetsTimestamp_AndCompletedOverdueFlag()
    {
        var item = TestItems.Todo("昨天创建今天完成", TestTime.Local(2026, 10, 10, 8));

        var done = OverdueService.MarkCompleted(item, Now);

        Assert.Equal(IsoTime.Format(Now), done.CompletedAt);
        Assert.True(done.Overdue); // 创建日 ≠ 完成日
    }

    [Fact]
    public void MarkCompleted_SameDay_FalseFlag()
    {
        var item = TestItems.Todo("今天创建今天完成", TestTime.Local(2026, 10, 11, 8));

        var done = OverdueService.MarkCompleted(item, Now);

        Assert.False(done.Overdue);
    }

    [Fact]
    public void ReturnToActive_ClearsCompletion_AndRecalculatesByToday()
    {
        var completed = TestItems.Todo(
            "退回", TestTime.Local(2026, 10, 10, 8), overdue: false, completed: TestTime.Local(2026, 10, 11, 9));

        var returned = OverdueService.ReturnToActive(completed, Today);

        Assert.Null(returned.CompletedAt);
        Assert.True(returned.Overdue); // 创建日 10-10 ≠ 今天
        Assert.Equal(completed.Id, returned.Id);
    }

    [Fact]
    public void ReturnToActive_CreatedToday_NotOverdue()
    {
        var completed = TestItems.Todo(
            "今天创建今天完成", TestTime.Local(2026, 10, 11, 8), completed: TestTime.Local(2026, 10, 11, 9));

        var returned = OverdueService.ReturnToActive(completed, Today);

        Assert.False(returned.Overdue);
    }
}
