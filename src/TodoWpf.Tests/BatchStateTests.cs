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
}
