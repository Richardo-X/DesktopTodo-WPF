using TodoWpf.Core.Models;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;
using Xunit;

namespace TodoWpf.Tests;

/// <summary>批次04：待办/模板增删改（纯函数）。</summary>
public class TodoOpsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);
    private const string Today = "2026-10-13";

    private static AppData WithTodos(params TodoItem[] items) => new() { Todos = [.. items] };

    private static TodoItem Todo(string content, string createdAtIso = "2026-10-13T01:00:00.000Z") => new()
    { Id = Guid.NewGuid().ToString(), Content = content, CreatedAt = createdAtIso };

    private static TodoItem Done(string content, string completedIso) => new()
    { Id = Guid.NewGuid().ToString(), Content = content, CreatedAt = "2026-10-12T01:00:00.000Z", CompletedAt = completedIso };

    // ---- 新建 ----
    [Fact]
    public void 新建_加末尾_内容trim()
    {
        var data = WithTodos(Todo("A"));
        var r = TodoOpsService.AddTodo(data, "  B  ", Now);
        Assert.True(r.Ok);
        Assert.Equal(2, r.Data.Todos.Count);
        Assert.Equal("B", r.Data.Todos[1].Content);
        Assert.Equal(Today, LocalDate.KeyOfIso(r.Data.Todos[1].CreatedAt));
    }

    [Fact]
    public void 新建_超上限拒绝()
    {
        var data = WithTodos(Enumerable.Range(0, 10).Select(i => Todo($"T{i}")).ToArray());
        var r = TodoOpsService.AddTodo(data, "B", Now);
        Assert.False(r.Ok);
        Assert.Equal(TodoOpsService.ReasonLimitReached, r.Reason);
    }

    [Fact]
    public void 新建_空内容拒绝()
    {
        var r = TodoOpsService.AddTodo(WithTodos(), "   ", Now);
        Assert.False(r.Ok);
        Assert.Equal(TodoOpsService.ReasonEmptyContent, r.Reason);
    }

    // ---- 编辑 ----
    [Fact]
    public void 编辑_改内容_其余字段不变()
    {
        var t = Todo("A");
        var r = TodoOpsService.EditTodo(WithTodos(t), t.Id, "  A2  ");
        Assert.True(r.Ok);
        Assert.Equal("A2", r.Data.Todos[0].Content);
        Assert.Equal(t.Id, r.Data.Todos[0].Id);
        Assert.Equal(t.CreatedAt, r.Data.Todos[0].CreatedAt);
    }

    [Fact]
    public void 编辑_空内容拒绝()
    {
        var t = Todo("A");
        var r = TodoOpsService.EditTodo(WithTodos(t), t.Id, "");
        Assert.False(r.Ok);
    }

    // ---- 删除 ----
    [Fact]
    public void 删除_单条()
    {
        var a = Todo("A");
        var r = TodoOpsService.DeleteTodo(WithTodos(a, Todo("B")), a.Id);
        Assert.True(r.Ok);
        Assert.Single(r.Data.Todos);
    }

    [Fact]
    public void 批量删除_只删存在的_保留顺序()
    {
        var a = Todo("A"); var b = Todo("B"); var c = Todo("C");
        var r = TodoOpsService.DeleteTodos(WithTodos(a, b, c), [a.Id, c.Id, "missing"]);
        Assert.True(r.Ok);
        Assert.Equal(["B"], r.Data.Todos.Select(t => t.Content).ToList());
    }

    // ---- 完成/退回 ----
    [Fact]
    public void 完成_移到已完成末尾_写完成时间_跨天创建标逾期()
    {
        var t = Todo("A", "2026-10-12T01:00:00.000Z"); // 昨天 → 完成后逾期
        var r = TodoOpsService.CompleteTodo(WithTodos(t), t.Id, Now);
        Assert.True(r.Ok);
        Assert.Empty(r.Data.Todos);
        var done = Assert.Single(r.Data.Completed);
        Assert.NotNull(done.CompletedAt);
        Assert.True(done.Overdue);
    }

    [Fact]
    public void 退回_加待办末尾_按今日重算逾期_受上限()
    {
        var d = Done("A", "2026-10-13T00:00:00.000Z");
        var data = new AppData { Completed = [d], Todos = [Todo("X")], Settings = new AppSettings { MaxItems = 2 } };
        var r = TodoOpsService.ReturnTodo(data, d.Id, Today, Now);
        Assert.True(r.Ok);
        var back = Assert.Single(r.Data.Todos, t => t.Id == d.Id);
        Assert.Null(back.CompletedAt);
        // 退回按"未完成口径"重算：创建日(10-12)≠今天(10-13) → 逾期（口径正确行为）
        Assert.True(back.Overdue);

        var full = new AppData { Completed = [d], Todos = [Todo("1"), Todo("2")], Settings = new AppSettings { MaxItems = 2 } };
        var r2 = TodoOpsService.ReturnTodo(full, d.Id, Today, Now);
        Assert.False(r2.Ok);
        Assert.Equal(TodoOpsService.ReasonLimitReached, r2.Reason);
    }

    // ---- 模板 ----
    [Fact]
    public void 模板_新建_查重_上限()
    {
        var r = TodoOpsService.AddTemplate(new AppData(), " 喝水 ", Now);
        Assert.True(r.Ok);
        Assert.Equal("喝水", Assert.Single(r.Data.Templates).Content);

        var r2 = TodoOpsService.AddTemplate(r.Data, "喝水", Now);
        Assert.False(r2.Ok);
        Assert.Equal(TodoOpsService.ReasonDuplicateTemplate, r2.Reason);

        var full = new AppData { Templates = Enumerable.Range(0, 10)
            .Select(i => new TemplateItem { Id = $"t{i}", Content = $"T{i}", CreatedAt = "2026-10-12T01:00:00.000Z" }).ToList() };
        var r3 = TodoOpsService.AddTemplate(full, "新的", Now);
        Assert.False(r3.Ok);
        Assert.Equal(TodoOpsService.ReasonTemplateLimitReached, r3.Reason);
    }

    [Fact]
    public void 模板_编辑查重排除自己_删除()
    {
        var t1 = new TemplateItem { Id = "t1", Content = "A", CreatedAt = "2026-10-12T01:00:00.000Z" };
        var t2 = new TemplateItem { Id = "t2", Content = "B", CreatedAt = "2026-10-12T01:00:00.000Z" };
        var data = new AppData { Templates = [t1, t2] };

        var r = TodoOpsService.EditTemplate(data, t1.Id, "B"); // 与 t2 重复
        Assert.False(r.Ok);

        var r2 = TodoOpsService.EditTemplate(data, t1.Id, "A "); // 与自己相同（trim 后）→ 允许
        Assert.True(r2.Ok);

        var r3 = TodoOpsService.DeleteTemplate(data, t1.Id);
        Assert.True(r3.Ok);
        Assert.Equal(["B"], r3.Data.Templates.Select(x => x.Content).ToList());
    }

    // ---- 模板一键加入 ----
    [Fact]
    public void 模板一键加入_模板保留_受待办上限()
    {
        var tpl = new TemplateItem { Id = "t1", Content = "  喝水  ", CreatedAt = "2026-10-12T01:00:00.000Z" };
        var data = new AppData { Templates = [tpl] };

        var r = TodoOpsService.ApplyTemplate(data, tpl.Id, Now);
        Assert.True(r.Ok);
        Assert.Equal("喝水", Assert.Single(r.Data.Todos).Content);
        Assert.Single(r.Data.Templates); // 模板保留

        var full = new AppData
        {
            Templates = [tpl],
            Todos = Enumerable.Range(0, 10).Select(i => Todo($"T{i}")).ToList(),
        };
        var r2 = TodoOpsService.ApplyTemplate(full, tpl.Id, Now);
        Assert.False(r2.Ok);
        Assert.Equal(TodoOpsService.ReasonLimitReached, r2.Reason);
    }
}
