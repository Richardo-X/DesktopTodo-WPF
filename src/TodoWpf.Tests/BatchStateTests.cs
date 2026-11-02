using TodoWpf.Core.Services;
using Xunit;

namespace TodoWpf.Tests;

/// <summary>批次04：批量删除状态机。</summary>
public class BatchStateTests
{
    [Fact]
    public void 进入_切换选中_删除可用性()
    {
        var b = new BatchState();
        Assert.False(b.IsActive);

        b.Enter("todos");
        Assert.True(b.IsActive);
        Assert.False(b.CanDelete);

        b.Toggle("a");
        b.Toggle("b");
        Assert.True(b.CanDelete);
        Assert.Equal(2, b.SelectedCount);

        b.Toggle("a"); // 再点 = 取消
        Assert.Single(b.SelectedIds);
    }

    [Fact]
    public void 已进行中重复进入_忽略并清空旧选择()
    {
        var b = new BatchState();
        b.Enter("todos");
        b.Toggle("a");
        b.Enter("todos"); // 忽略
        Assert.Empty(b.SelectedIds);
        Assert.True(b.IsActive);
    }

    [Fact]
    public void 退出后_清空且不可删()
    {
        var b = new BatchState();
        b.Enter("templates");
        b.Toggle("t1");
        b.Exit();
        Assert.False(b.IsActive);
        Assert.False(b.CanDelete);
        Assert.Empty(b.SelectedIds);
    }

    [Fact]
    public void 未进入时Toggle忽略()
    {
        var b = new BatchState();
        b.Toggle("x");
        Assert.Empty(b.SelectedIds);
    }

    // ---------- 全选切换（批次05①，原型 batchAll 口径） ----------

    [Fact]
    public void 全选_部分选中时_全选集合()
    {
        var b = new BatchState();
        b.Enter("todos");
        b.Toggle("a");
        b.ToggleAll(["a", "b", "c"]);

        Assert.Equal(3, b.SelectedCount);
        Assert.True(b.IsSelected("b"));
    }

    [Fact]
    public void 全选_全部已选中时_清空为取消全选()
    {
        var b = new BatchState();
        b.Enter("todos");
        b.ToggleAll(["a", "b"]);
        b.ToggleAll(["a", "b"]); // 再点 = 取消全选

        Assert.Empty(b.SelectedIds);
        Assert.False(b.CanDelete);
    }

    [Fact]
    public void 全选_未进入时忽略_空集合无效果()
    {
        var b = new BatchState();
        b.ToggleAll(["x"]); // 未进入：忽略
        Assert.False(b.IsActive);

        b.Enter("todos");
        b.ToggleAll([]); // 空集合：无效果（原型 sel.size===0 时清空亦无效果）
        Assert.Empty(b.SelectedIds);
    }
}
