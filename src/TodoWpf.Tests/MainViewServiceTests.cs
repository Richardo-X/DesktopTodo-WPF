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


    // ---------- 批量态投影（2026-11-06 查漏补测：此前 batch 分支零覆盖） ----------
    // 批量态是历史 bug 高发区（勾选消失 / 全选不同步 / 快速连点丢点击），必须有单测钉住。

    private static AppData WithTodos(params TodoItem[] todos) =>
        new() { Todos = todos.ToList() };

    private static BatchState TodosBatch(params string[] selectedIds)
    {
        var batch = new BatchState();
        batch.Enter("todos");
        foreach (var id in selectedIds)
        {
            batch.Toggle(id);
        }

        return batch;
    }

    [Fact]
    public void 待办行_批量态_投影选中与批量标志()
    {
        var a = Todo("甲", "2026-10-13T01:00:00.000Z");
        var b = Todo("乙", "2026-10-13T02:00:00.000Z");
        var data = WithTodos(a, b);

        var rows = MainViewService.TodoRows(data, Today, TodosBatch(a.Id));

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.BatchMode));   // 批量中：每行都带批量标志
        Assert.True(rows.Single(r => r.Id == a.Id).Selected);
        Assert.False(rows.Single(r => r.Id == b.Id).Selected);
    }

    [Fact]
    public void 待办行_常规态_不带批量标志也不选中()
    {
        var a = Todo("甲", "2026-10-13T01:00:00.000Z");
        var rows = MainViewService.TodoRows(WithTodos(a), Today, batch: null);

        var row = Assert.Single(rows);
        Assert.False(row.BatchMode);
        Assert.False(row.Selected);
    }

    [Fact]
    public void 待办行_批量已退出_不带批量标志()
    {
        var a = Todo("甲", "2026-10-13T01:00:00.000Z");
        var batch = TodosBatch(a.Id);
        batch.Exit();

        var row = Assert.Single(MainViewService.TodoRows(WithTodos(a), Today, batch));
        Assert.False(row.BatchMode);
        Assert.False(row.Selected);
    }

    [Fact]
    public void 待办行_批量标志只取决于是否进行中_不区分目标()
    {
        // 契约：本服务只看 IsActive，**不看 Target**；所以调用方必须按目标传 null
        // （ClusterController.PushBatchBar 里就是 `mainBatch ? _batch : null`）。
        // 若将来有调用方忘记判目标，这里会先红。
        var a = Todo("甲", "2026-10-13T01:00:00.000Z");
        var batch = new BatchState();
        batch.Enter("templates");

        var row = Assert.Single(MainViewService.TodoRows(WithTodos(a), Today, batch));
        Assert.True(row.BatchMode);   // 当前行为：目标不匹配也会带标志 → 调用方必须传 null
    }

    [Fact]
    public void 模板行_批量态_投影选中与批量标志()
    {
        var t1 = new TemplateItem { Id = "t1", Content = "模板甲" };
        var t2 = new TemplateItem { Id = "t2", Content = "模板乙" };
        var data = new AppData { Templates = [t1, t2] };

        var batch = new BatchState();
        batch.Enter("templates");
        batch.Toggle("t2");

        var rows = MainViewService.TemplateRows(data, batch);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.BatchMode));
        Assert.True(rows.Single(r => r.Id == "t2").Selected);
        Assert.False(rows.Single(r => r.Id == "t1").Selected);
    }

    // ---------- 行投影的就地更新（INPC）：快速连点不丢点击的前提 ----------

    [Fact]
    public void 待办行_就地改选中_触发INPC_同值不重复触发()
    {
        var row = new TodoRowView("x", "内容", overdue: false);
        var events = new List<string?>();
        row.PropertyChanged += (_, e) => events.Add(e.PropertyName);

        row.Selected = true;
        row.Selected = true;    // 同值：不重复触发（避免无谓刷新）
        row.Selected = false;

        Assert.Equal(new[] { "Selected", "Selected" }, events);
        Assert.False(row.Selected);
    }

    [Fact]
    public void 模板行_就地改选中_触发INPC()
    {
        var row = new TemplateRowView("t", "内容");
        var events = new List<string?>();
        row.PropertyChanged += (_, e) => events.Add(e.PropertyName);

        row.Selected = true;

        Assert.Equal(new[] { "Selected" }, events);
        Assert.True(row.Selected);
    }

    [Fact]
    public void 批量投影_不替换实例_可就地改选中()
    {
        // 关键回归点：若投影每次返回新实例，ItemsControl 会重建容器 → 快速连点丢点击
        // （用户 2026-11-04 反馈）。这里钉住"投影出的对象本身可变、且变化能通知到绑定"。
        var a = Todo("甲", "2026-10-13T01:00:00.000Z");
        var rows = MainViewService.TodoRows(WithTodos(a), Today, TodosBatch());
        var row = Assert.Single(rows);

        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;
        row.Selected = true;

        Assert.True(raised);
        Assert.True(row.Selected);
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
