using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>
/// 主窗一行待办的展示投影。**可观察对象**（非 record）：批量勾选只改 Selected 属性、
/// 由绑定就地刷新——若每次换新实例，ItemsControl 会重建该行容器，快速连点会丢点击
/// （用户 2026-11-04 反馈「快速点击无法快速切换」的根因）。
/// </summary>
public sealed class TodoRowView : System.ComponentModel.INotifyPropertyChanged
{
    private bool _selected;

    public TodoRowView(string id, string content, bool overdue,
                       bool completed = false, bool selected = false, bool batchMode = false,
                       string? fullContent = null)
    {
        Id = id;
        Content = content;
        FullContent = fullContent ?? content;
        Overdue = overdue;
        Completed = completed;
        _selected = selected;
        BatchMode = batchMode;
    }

    public string Id { get; }

    /// <summary>行内显示文本（只一行，见 MainViewService.RowText）。</summary>
    public string Content { get; }

    /// <summary>完整内容（可含换行）；悬停气泡显示它，详情窗另有数据源。</summary>
    public string FullContent { get; }

    public bool Overdue { get; }

    public bool Completed { get; }

    /// <summary>批量态标志（整表刷新时确定，不单独变更）。</summary>
    public bool BatchMode { get; }

    /// <summary>批量选中态（就地可变，绑定直接刷新）。</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed record DoneRowView(string Id, string Content, bool Overdue, string CompletedTimeText)
{
    /// <summary>完整内容（可含换行）；悬停气泡显示它。</summary>
    public string FullContent { get; init; } = "";
}

/// <summary>模板行投影（同 TodoRowView：Selected 就地可变）。</summary>
public sealed class TemplateRowView : System.ComponentModel.INotifyPropertyChanged
{
    private bool _selected;

    public TemplateRowView(string id, string content, bool selected = false, bool batchMode = false,
                           string? fullContent = null)
    {
        Id = id;
        Content = content;
        FullContent = fullContent ?? content;
        _selected = selected;
        BatchMode = batchMode;
    }

    public string Id { get; }

    /// <summary>行内显示文本（只一行）。</summary>
    public string Content { get; }

    /// <summary>完整内容（可含换行）；悬停气泡显示它。</summary>
    public string FullContent { get; }

    public bool BatchMode { get; }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 三窗列表/计数的展示口径（纯函数，可单测）：
/// 待办 = data.Todos 原序（新事项加末尾）；已完成 = 按 DoneSort 排序（默认正序=完成时间升序）；
/// 模板 = data.Templates 原序。计数 = 待办条数/上限。逾期标 = 各自口径（OverdueService）。
/// </summary>
public static class MainViewService
{
    public static string CountText(int activeCount, int maxItems) => $"{activeCount}/{maxItems}";

    /// <summary>
    /// 列表行显示文本：**只显示一行**（编辑窗允许 Shift+Enter 换行，但列表行不能变成两行——
    /// 用户 2026-11-06 反馈）。规则：取第一个换行之前的内容；**开头的换行先跳过**
    /// （否则"先敲回车再写字"会让行内容为空）；只处理换行，不 trim 其它空白。
    /// 只影响"行怎么显示"——存储内容与详情/悬停气泡仍是全文。
    /// </summary>
    public static string RowText(string? content)
    {
        const char LF = (char)10;
        const char CR = (char)13;

        var text = content ?? string.Empty;
        var start = 0;
        while (start < text.Length && (text[start] == LF || text[start] == CR))
        {
            start++;
        }

        var rest = text[start..];
        var breakAt = rest.IndexOfAny([LF, CR]);
        return breakAt < 0 ? rest : rest[..breakAt];
    }

    /// <summary>
    /// 首行之后是否还有内容（决定是否弹悬停气泡）：跳过开头换行后，第一个换行**之后还有非空白文字**。
    /// 背景（用户 2026-11-06）：列表行只显示第一行后，"首行放得下、换行后还有字"的内容在行里看不全，
    /// 必须靠气泡显示全文；而"只有尾随换行/换行后全是空白"不算（行里已经看全了）。
    /// </summary>
    public static bool HasContentAfterFirstLine(string? content)
    {
        const char LF = (char)10;
        const char CR = (char)13;

        var text = content ?? string.Empty;
        var start = 0;
        while (start < text.Length && (text[start] == LF || text[start] == CR))
        {
            start++;
        }

        var rest = text[start..];
        var breakAt = rest.IndexOfAny([LF, CR]);
        return breakAt >= 0 && rest[(breakAt + 1)..].Trim().Length > 0;
    }

    public static bool CanAddTodo(int activeCount, int maxItems) => activeCount < maxItems;

    public static List<TodoRowView> TodoRows(AppData data, string todayKey, BatchState? batch = null) =>
        data.Todos
            .Select(item => new TodoRowView(
                item.Id,
                RowText(item.Content),
                OverdueService.IsActiveOverdue(item, todayKey),
                false,
                batch is { IsActive: true } && batch.IsSelected(item.Id),
                batch is { IsActive: true },
                item.Content))
            .ToList();

    public static List<DoneRowView> DoneRows(AppData data, string sort)
    {
        var items = sort == "desc"
            ? data.Completed.OrderByDescending(item => item.CompletedAt, StringComparer.Ordinal).ToList()
            : data.Completed.OrderBy(item => item.CompletedAt, StringComparer.Ordinal).ToList();

        return items
            .Select(item => new DoneRowView(
                item.Id,
                RowText(item.Content),
                OverdueService.IsCompletedOverdue(item),
                CompletedTimeText(item.CompletedAt))
            {
                FullContent = item.Content,
            })
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

    public static List<TemplateRowView> TemplateRows(AppData data, BatchState? batch = null) =>
        data.Templates
            .Select(t => new TemplateRowView(
                t.Id,
                RowText(t.Content),
                batch is { IsActive: true } && batch.IsSelected(t.Id),
                batch is { IsActive: true },
                t.Content))
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
