using TodoWpf.Core.Models;

namespace TodoWpf.Tests;

/// <summary>默认档（需求文档 §2.8/§2.9）：上限 10 / 24 小时制 / 不自启 / 正序；面板全开、白天、不置顶、不锁缩进。</summary>
public class DefaultsTests
{
    [Fact]
    public void DefaultAppData_MatchesBaseline()
    {
        var data = Defaults.DefaultAppData();

        Assert.Equal(1, data.V);
        Assert.Empty(data.Todos);
        Assert.Empty(data.Completed);
        Assert.Empty(data.Archive);
        Assert.Empty(data.Templates);

        Assert.Equal(10, data.Settings.MaxItems);
        Assert.False(data.Settings.Hour12);
        Assert.False(data.Settings.AutoStart);
        Assert.Equal("asc", data.Settings.DoneSort);

        Assert.Null(data.UiState.Pos);
        Assert.False(data.UiState.PosIsTodoAnchor);
        Assert.Equal(1, data.UiState.ClusterScale);
        Assert.Empty(data.UiState.ChildScales);
        Assert.True(data.UiState.QuickVisible);
        Assert.True(data.UiState.DoneVisible);
        Assert.Equal("day", data.UiState.Theme);
        Assert.False(data.UiState.TopMost);
        Assert.False(data.UiState.TuckLock);
        Assert.Null(data.UiState.LastSettleDate);
    }

    [Fact]
    public void DefaultCollections_AreIndependentPerInstance()
    {
        var first = Defaults.DefaultAppData();
        var second = Defaults.DefaultAppData();

        first.Todos.Add(ItemFactory.CreateTodoItem("污染", DateTimeOffset.Now));

        Assert.Empty(second.Todos);
    }

    [Fact]
    public void DeepCopy_DetachesMutableCollections()
    {
        var original = Defaults.DefaultAppData() with
        {
            Todos = [ItemFactory.CreateTodoItem("原始", DateTimeOffset.Now)],
            Templates = [ItemFactory.CreateTemplateItem("模板", DateTimeOffset.Now)],
        };

        var copy = original.DeepCopy();
        copy.Todos.Add(ItemFactory.CreateTodoItem("新增", DateTimeOffset.Now));
        copy.Templates.Clear();

        Assert.Single(original.Todos);
        Assert.Single(original.Templates);
        Assert.Equal(2, copy.Todos.Count);
    }
}
