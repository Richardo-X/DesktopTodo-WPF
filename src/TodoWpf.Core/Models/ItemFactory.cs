using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Models;

/// <summary>事项/模板工厂：统一 id、时间戳与内容校验。</summary>
public static class ItemFactory
{
    public static TodoItem CreateTodoItem(string? content, DateTimeOffset now, string? id = null) => new()
    {
        Id = id ?? Ids.NewId(),
        Content = ContentRules.RequireValid(content),
        CreatedAt = IsoTime.Format(now),
        CompletedAt = null,
        Overdue = false,
    };

    public static TemplateItem CreateTemplateItem(string? content, DateTimeOffset now, string? id = null) => new()
    {
        Id = id ?? Ids.NewId(),
        Content = ContentRules.RequireValid(content),
        CreatedAt = IsoTime.Format(now),
    };
}
