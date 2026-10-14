using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>主窗一行待办/已完成/模板的展示投影（纯数据，UI 层直接绑定）。</summary>
public sealed record TodoRowView(string Id, string Content, bool Overdue, bool Completed = false);

public sealed record DoneRowView(string Id, string Content, bool Overdue, string CompletedTimeText);

public sealed record TemplateRowView(string Id, string Content);

/// <summary>
/// 三窗列表/计数的展示口径（纯函数，可单测）：
/// 待办 = data.Todos 原序（新事项加末尾）；已完成 = 按 DoneSort 排序（默认正序=完成时间升序）；
/// 模板 = data.Templates 原序。计数 = 待办条数/上限。逾期标 = 各自口径（OverdueService）。
/// </summary>
public static class MainViewService
{
    public static string CountText(int activeCount, int maxItems) => $"{activeCount}/{maxItems}";

    public static bool CanAddTodo(int activeCount, int maxItems) => activeCount < maxItems;

    public static List<TodoRowView> TodoRows(AppData data, string todayKey) =>
        data.Todos
            .Select(item => new TodoRowView(item.Id, item.Content, OverdueService.IsActiveOverdue(item, todayKey)))
            .ToList();

    public static List<DoneRowView> DoneRows(AppData data, string sort)
    {
        var items = sort == "desc"
            ? data.Completed.OrderByDescending(item => item.CompletedAt, StringComparer.Ordinal).ToList()
            : data.Completed.OrderBy(item => item.CompletedAt, StringComparer.Ordinal).ToList();

        return items
            .Select(item => new DoneRowView(
                item.Id,
                item.Content,
                OverdueService.IsCompletedOverdue(item),
                CompletedTimeText(item.CompletedAt)))
            .ToList();
    }

    /// <summary>完成时间显示 HH:mm（本地时区；Electron 版口径）。</summary>
    public static string CompletedTimeText(string? completedAtIso)
    {
        if (completedAtIso is null)
        {
            return "";
        }

        var local = IsoTime.ParseLocal(completedAtIso);
        return local.ToString("HH:mm");
    }

    public static List<TemplateRowView> TemplateRows(AppData data) =>
        data.Templates
            .Select(t => new TemplateRowView(t.Id, t.Content))
            .ToList();

    /// <summary>
    /// 时钟文字（与 Electron 版 fmtTime 逐字符同口径）：
    /// 24h = "HH:mm"；12h = "h:mm AM/PM"（小时不补零、0 点=12）。
    /// </summary>
    public static string ClockText(DateTimeOffset now, bool hour12)
    {
        if (!hour12)
        {
            return now.ToString("HH:mm");
        }

        var h24 = now.Hour;
        var h = h24 % 12 == 0 ? 12 : h24 % 12;
        return $"{h}:{now.Minute:D2} {(h24 < 12 ? "AM" : "PM")}";
    }
}
