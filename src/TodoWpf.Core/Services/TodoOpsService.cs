using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>增删改的操作结果（纯数据，UI 层映射提示文案）。</summary>
public sealed record TodoOpsResult(AppData Data, bool Ok, string? Reason = null);

/// <summary>
/// 待办/模板的增删改（纯函数：输入 AppData 返回新 AppData；无 IO、无 UI）。
/// 顺序口径：新事项/退回事项加末尾；完成事项加已完成末尾。
/// </summary>
public static class TodoOpsService
{
    public const string ReasonLimitReached = "limit-reached";
    public const string ReasonNotFound = "not-found";
    public const string ReasonEmptyContent = "empty-content";
    public const string ReasonTooLong = "too-long";
    public const string ReasonTemplateLimitReached = "template-limit-reached";
    public const string ReasonDuplicateTemplate = "duplicate-template";

    /// <summary>新建待办（末尾）；受待办上限与内容校验约束。</summary>
    public static TodoOpsResult AddTodo(AppData data, string? content, DateTimeOffset now)
    {
        if (data.Todos.Count >= data.Settings.MaxItems)
        {
            return new(data, false, ReasonLimitReached);
        }

        if (!ValidContent(content, out var reason))
        {
            return new(data, false, reason);
        }

        var item = ItemFactory.CreateTodoItem(content, now);
        var next = data.DeepCopy();
        next.Todos.Add(item);
        return new(next, true);
    }

    /// <summary>编辑待办内容（保存；空内容不保存——UI 层提示且不关闭）。</summary>
    public static TodoOpsResult EditTodo(AppData data, string id, string? content)
    {
        var index = data.Todos.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        if (!ValidContent(content, out var reason))
        {
            return new(data, false, reason);
        }

        var next = data.DeepCopy();
        next.Todos[index] = next.Todos[index] with { Content = content!.Trim() };
        return new(next, true);
    }

    /// <summary>永久删除待办。</summary>
    public static TodoOpsResult DeleteTodo(AppData data, string id)
    {
        var index = data.Todos.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        var next = data.DeepCopy();
        next.Todos.RemoveAt(index);
        return new(next, true);
    }

    /// <summary>批量永久删除待办（只删存在的 id；顺序保留）。</summary>
    public static TodoOpsResult DeleteTodos(AppData data, IReadOnlyCollection<string> ids)
    {
        var next = data.DeepCopy();
        var removed = next.Todos.RemoveAll(t => ids.Contains(t.Id));
        return removed == 0 ? new(data, false, ReasonNotFound) : new(next, true);
    }

    /// <summary>勾选完成：移到已完成末尾（MarkCompleted 写完成时间+按完成口径重算逾期标）。</summary>
    public static TodoOpsResult CompleteTodo(AppData data, string id, DateTimeOffset now)
    {
        var index = data.Todos.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        var next = data.DeepCopy();
        var item = next.Todos[index];
        next.Todos.RemoveAt(index);
        next.Completed.Add(OverdueService.MarkCompleted(item, now));
        return new(next, true);
    }

    /// <summary>已完成退回待办（末尾；按今日重算逾期标；受待办上限约束）。</summary>
    public static TodoOpsResult ReturnTodo(AppData data, string id, string todayKey, DateTimeOffset now)
    {
        var index = data.Completed.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        if (data.Todos.Count >= data.Settings.MaxItems)
        {
            return new(data, false, ReasonLimitReached);
        }

        var next = data.DeepCopy();
        var item = next.Completed[index];
        next.Completed.RemoveAt(index);
        next.Todos.Add(OverdueService.ReturnToActive(item, todayKey));
        return new(next, true);
    }

    /// <summary>从模板一键加入今日待办（模板保留；受待办上限；内容=模板内容）。</summary>
    public static TodoOpsResult ApplyTemplate(AppData data, string templateId, DateTimeOffset now) =>
        data.Templates.FindIndex(t => t.Id == templateId) is var index and >= 0
            ? AddTodo(data, data.Templates[index].Content, now)
            : new(data, false, ReasonNotFound);

    /// <summary>新建模板（末尾；受模板上限 10 与同内容查重）。</summary>
    public static TodoOpsResult AddTemplate(AppData data, string? content, DateTimeOffset now)
    {
        if (data.Templates.Count >= TemplateLimit.Max)
        {
            return new(data, false, ReasonTemplateLimitReached);
        }

        if (!ValidContent(content, out var reason))
        {
            return new(data, false, reason);
        }

        var trimmed = content!.Trim();
        if (data.Templates.Any(t => t.Content == trimmed))
        {
            return new(data, false, ReasonDuplicateTemplate);
        }

        var next = data.DeepCopy();
        next.Templates.Add(ItemFactory.CreateTemplateItem(trimmed, now));
        return new(next, true);
    }

    /// <summary>编辑模板内容（同内容查重；空内容不保存）。</summary>
    public static TodoOpsResult EditTemplate(AppData data, string id, string? content)
    {
        var index = data.Templates.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        if (!ValidContent(content, out var reason))
        {
            return new(data, false, reason);
        }

        var trimmed = content!.Trim();
        if (data.Templates.Any(t => t.Id != id && t.Content == trimmed))
        {
            return new(data, false, ReasonDuplicateTemplate);
        }

        var next = data.DeepCopy();
        next.Templates[index] = next.Templates[index] with { Content = trimmed };
        return new(next, true);
    }

    /// <summary>删除模板。</summary>
    public static TodoOpsResult DeleteTemplate(AppData data, string id)
    {
        var index = data.Templates.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return new(data, false, ReasonNotFound);
        }

        var next = data.DeepCopy();
        next.Templates.RemoveAt(index);
        return new(next, true);
    }

    private static bool ValidContent(string? raw, out string? reason)
    {
        var result = ContentRules.Validate(raw);
        if (result.Ok)
        {
            reason = null;
            return true;
        }

        reason = result.Reason == ContentError.TooLong ? ReasonTooLong : ReasonEmptyContent;
        return false;
    }
}
