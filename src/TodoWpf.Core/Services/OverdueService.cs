using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>
/// 逾期判定统一口径（需求文档 §2.9）：
/// 未完成 = 创建日期 ≠ 今天；已完成 = 创建日期 ≠ 完成日期；
/// 从已完成退回待办时按今日重算；导入记录同样按此口径推算。
/// </summary>
public static class OverdueService
{
    /// <summary>待办（未完成）是否逾期。</summary>
    public static bool IsActiveOverdue(TodoItem item, string todayKey)
    {
        if (item.CompletedAt is not null)
        {
            return false;
        }

        return LocalDate.KeyOfIso(item.CreatedAt) != todayKey;
    }

    /// <summary>已完成事项是否"已逾期"。</summary>
    public static bool IsCompletedOverdue(TodoItem item)
    {
        if (item.CompletedAt is null)
        {
            return false;
        }

        return LocalDate.KeyOfIso(item.CreatedAt) != LocalDate.KeyOfIso(item.CompletedAt);
    }

    /// <summary>批量重算未完成事项的逾期标记（跨天结算/退回后调用）；保持顺序，已完成项原样保留。</summary>
    public static List<TodoItem> RecalcActiveOverdue(IEnumerable<TodoItem> todos, string todayKey) =>
        todos
            .Select(item => item.CompletedAt is null
                ? item with { Overdue = IsActiveOverdue(item, todayKey) }
                : item)
            .ToList();

    /// <summary>勾选完成：写入完成时间并按完成口径重算逾期标记。</summary>
    public static TodoItem MarkCompleted(TodoItem item, DateTimeOffset now)
    {
        var completed = item with { CompletedAt = IsoTime.Format(now) };
        return completed with { Overdue = IsCompletedOverdue(completed) };
    }

    /// <summary>已完成退回待办：清空完成时间、按今日重算逾期。</summary>
    public static TodoItem ReturnToActive(TodoItem item, string todayKey)
    {
        var active = item with { CompletedAt = null };
        return active with { Overdue = IsActiveOverdue(active, todayKey) };
    }
}
