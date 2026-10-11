using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>事项/模板工厂：字段齐备、时间戳形态与旧版一致、id 为小写 UUID。</summary>
public class ItemFactoryTests
{
    [Fact]
    public void CreateTodoItem_FillsFields_AndTrimsContent()
    {
        var now = TestTime.Local(2026, 10, 11, 9, 30, 15, 123);

        var item = ItemFactory.CreateTodoItem("  写周报  ", now);

        Assert.Equal(IsoTime.Format(now), item.CreatedAt);
        Assert.Equal("写周报", item.Content);
        Assert.Null(item.CompletedAt);
        Assert.False(item.Overdue);
        Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            item.Id);
    }

    [Fact]
    public void CreateTodoItem_UsesProvidedId()
    {
        var item = ItemFactory.CreateTodoItem("内容", TestTime.Local(2026, 1, 1), id: "fixed-id");

        Assert.Equal("fixed-id", item.Id);
    }

    [Fact]
    public void CreateTodoItem_ThrowsDomainException_OnEmpty()
    {
        var ex = Assert.Throws<DomainException>(
            () => ItemFactory.CreateTodoItem("   ", TestTime.Local(2026, 1, 1)));

        Assert.Equal(ContentError.EmptyContent, ex.Reason);
    }

    [Fact]
    public void CreateTodoItem_ThrowsDomainException_OnTooLong()
    {
        var ex = Assert.Throws<DomainException>(
            () => ItemFactory.CreateTodoItem(new string('a', 301), TestTime.Local(2026, 1, 1)));

        Assert.Equal(ContentError.TooLong, ex.Reason);
    }

    [Fact]
    public void CreateTemplateItem_FillsFields_AndTrimsContent()
    {
        var now = TestTime.Local(2026, 10, 11, 9);

        var template = ItemFactory.CreateTemplateItem("  每日站会 ", now);

        Assert.Equal("每日站会", template.Content);
        Assert.Equal(IsoTime.Format(now), template.CreatedAt);
        Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            template.Id);
    }
}
