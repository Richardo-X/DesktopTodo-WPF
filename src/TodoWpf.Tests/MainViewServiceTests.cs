using TodoWpf.Core.Models;
using TodoWpf.Core.Services;
using Xunit;

namespace TodoWpf.Tests;

/// <summary>批次04-1：三窗列表/计数/时钟的展示口径。</summary>
public class MainViewServiceTests
{
    private static TodoItem Todo(string content, string createdAtIso, bool overdue = false) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Content = content,
        CreatedAt = createdAtIso,
        CompletedAt = null,
        Overdue = overdue,
    };

    private static TodoItem Done(string content, string createdAtIso, string completedAtIso, bool overdue = false) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Content = content,
        CreatedAt = createdAtIso,
        CompletedAt = completedAtIso,
        Overdue = overdue,
    };

    private const string Today = "2026-10-13";

    [Fact]
    public void 计数_条数与上限()
    {
        Assert.Equal("3/10", MainViewService.CountText(3, 10));
        Assert.Equal("0/10", MainViewService.CountText(0, 10));
    }

    [Fact]
    public void 能否新建_受上限约束()
    {
        Assert.True(MainViewService.CanAddTodo(9, 10));
        Assert.False(MainViewService.CanAddTodo(10, 10));
    }

    [Fact]
    public void 待办行_保持原序_逾期标按今日口径()
    {
        var data = new AppData
        {
            Todos =
            [
                Todo("今天的", "2026-10-13T01:00:00.000Z"),
                Todo("昨天的", "2026-10-12T01:00:00.000Z", overdue: true),
            ],
        };

        var rows = MainViewService.TodoRows(data, Today);
        Assert.Equal(2, rows.Count);
        Assert.Equal("今天的", rows[0].Content);
        Assert.False(rows[0].Overdue);
        Assert.Equal("昨天的", rows[1].Content);
        Assert.True(rows[1].Overdue);
    }

    [Fact]
    public void 待办行_创建在今天但Overdue标志为脏数据时_按口径显示不逾期()
    {
        // 展示层以口径为准，不信任存储的 Overdue 标志
        var data = new AppData { Todos = [Todo("脏标志", "2026-10-13T01:00:00.000Z", overdue: true)] };
        var rows = MainViewService.TodoRows(data, Today);
        Assert.False(rows[0].Overdue);
    }

    [Fact]
    public void 已完成行_默认正序_完成时间取本地HHmm_逾期按完成口径()
    {
        var data = new AppData
        {
            Completed =
            [
                Done("晚完成的", "2026-10-12T01:00:00.000Z", "2026-10-13T06:30:00.000Z"),   // 本地 14:30 (UTC+8)
                Done("早完成的", "2026-10-12T01:00:00.000Z", "2026-10-13T00:15:00.000Z"),   // 本地 08:15
            ],
        };

        var rows = MainViewService.DoneRows(data, "asc");
        Assert.Equal("早完成的", rows[0].Content);
        Assert.Equal("08:15", rows[0].CompletedTimeText);
        Assert.Equal("14:30", rows[1].CompletedTimeText);
        // 创建≠完成日 → 已逾期
        Assert.True(rows[0].Overdue);
    }

    [Fact]
    public void 已完成行_倒序()
    {
        var data = new AppData
        {
            Completed =
            [
                Done("早完成的", "2026-10-12T01:00:00.000Z", "2026-10-13T00:15:00.000Z"),
                Done("晚完成的", "2026-10-12T01:00:00.000Z", "2026-10-13T06:30:00.000Z"),
            ],
        };

        var rows = MainViewService.DoneRows(data, "desc");
        Assert.Equal("晚完成的", rows[0].Content);
    }

    [Fact]
    public void 模板行_保持原序()
    {
        var data = new AppData
        {
            Templates =
            [
                new TemplateItem { Id = "t1", Content = "每天站会前喝口水", CreatedAt = "2026-10-12T01:00:00.000Z" },
                new TemplateItem { Id = "t2", Content = "写周报", CreatedAt = "2026-10-12T02:00:00.000Z" },
            ],
        };

        var rows = MainViewService.TemplateRows(data);
        Assert.Equal(["每天站会前喝口水", "写周报"], rows.Select(r => r.Content).ToList());
    }

    [Fact]
    public void 时钟_24h与12h()
    {
        var now = new DateTimeOffset(2026, 10, 13, 15, 5, 0, TimeSpan.Zero);
        Assert.Equal("15:05", MainViewService.ClockText(now, hour12: false));
        Assert.Equal("3:05 PM", MainViewService.ClockText(now, hour12: true));

        var midnight = new DateTimeOffset(2026, 10, 13, 0, 5, 0, TimeSpan.Zero);
        Assert.Equal("12:05 AM", MainViewService.ClockText(midnight, hour12: true));
    }
}
