using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpf.Core.Models;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;

namespace TodoWpf.Windows;

/// <summary>历史窗一行（当日归档条目的展示投影）。</summary>
public sealed record HistRowView(string Id, string Content, bool Overdue, string TimeText,
                                 bool Selected = false, bool BatchMode = false);

/// <summary>
/// 历史窗（原型 openHistory；420×560）：单日导航 + 动作行两态 + 只读列表 + 导出区间。
/// 双击行 = 只读详情（详情删除后本窗刷新）；批量删除走确认框；数据操作全部经控制器落盘。
/// </summary>
public partial class HistoryDialog : Window
{
    private readonly ClusterController _controller;
    private string _cur;
    private bool _batching;
    private readonly HashSet<string> _selected = new();

    public System.Collections.ObjectModel.ObservableCollection<HistRowView> Rows { get; } = new();

    private HistoryDialog(ClusterController controller)
    {
        InitializeComponent();
        // 2026-11-05 裁定：二级窗弃 WPF 投影，改 DWM 系统圆角（与主窗同壳）
        WindowShell.ApplyFixed(this);
        _controller = controller;
        DataContext = this;

        var today = LocalDate.Key(DateTimeOffset.Now);
        // 默认昨天（用户 2026-11-05）：当天的完成项只在主窗「已完成」里，
        // 跨天结算后才进历史 → 历史窗默认看昨天。
        _cur = ShiftDateKey(today, -1);

        CurDatePicker.SelectedDate = ParseDate(_cur);
        FromPicker.SelectedDate = ParseDate(EarliestDate());
        ToPicker.SelectedDate = ParseDate(today);

        // 可选范围（用户 2026-11-05：未来日期要置灰）：用 BlackoutDates 而不是
        // DisplayDateStart/End —— 后者会让超范围的日格**根本不生成**（日历下半片空白）。
        // BlackoutDates 则保留日格、按 IsBlackedOut 渲染成灰字且不可点。
        LimitSelectableRange(CurDatePicker, EarliestDate(), today);
        LimitSelectableRange(FromPicker, EarliestDate(), today);
        LimitSelectableRange(ToPicker, EarliestDate(), today);
        DialogScale.Attach(this, CardRoot, ScaleGrip, "history", 420, 560, controller);
        UpdateSortIcon();
        Render();
    }


    /// <summary>
    /// 把可选日期限制在 [minKey, maxKey]：范围外一律加入 BlackoutDates（灰显不可点）。
    /// 必须在日历已创建后调用——DatePicker 的 Calendar 在我们的模板 Popup 里，
    /// 所以挂 CalendarOpened，取 PART_Popup 的 Child（就是 Calendar）。
    /// </summary>
    private static void LimitSelectableRange(DatePicker picker, string minKey, string maxKey)
    {
        picker.CalendarOpened += (_, _) =>
        {
            if (picker.Template?.FindName("PART_Popup", picker) is not System.Windows.Controls.Primitives.Popup popup
                || popup.Child is not System.Windows.Controls.Calendar calendar
                || calendar.Tag is "range-limited")
            {
                return;
            }

            var min = ParseDate(minKey) ?? DateTime.Today;
            var max = ParseDate(maxKey) ?? DateTime.Today;
            var floor = new DateTime(1900, 1, 1);
            var ceiling = new DateTime(2100, 12, 31);

            if (min > floor)
            {
                calendar.BlackoutDates.Add(new CalendarDateRange(floor, min.AddDays(-1)));
            }

            if (max < ceiling)
            {
                calendar.BlackoutDates.Add(new CalendarDateRange(max.AddDays(1), ceiling));
            }

            calendar.Tag = "range-limited";
        };
    }

    /// <summary>打开历史记录（模态；从设置窗打开时嵌套）。</summary>
    public static void Show(ClusterController controller)
    {
        var dialog = new HistoryDialog(controller)
        {
            Owner = AppWindows.Main(),
        };
        if (dialog.Owner is not null)
        {
            // 同设置窗：物理像素定位必须在可见前（SourceInitialized）
            dialog.SourceInitialized += (_, _) => MonitorLayout.CenterOnOwnerPhysical(dialog, dialog.Owner, edgeDips: 8);
        }

        dialog.ShowDialog();
    }

    // ---------- 渲染 ----------

    private void Render()
    {
        var today = LocalDate.Key(DateTimeOffset.Now);
        var earliest = EarliestDate();
        var sort = _controller.Data.Settings.DoneSort;
        var source = _controller.Data.Archive.TryGetValue(_cur, out var bucket)
            ? bucket
            : new List<TodoItem>();
        var list = sort == "desc"
            ? source.OrderByDescending(t => t.CompletedAt, StringComparer.Ordinal).ToList()
            : source.OrderBy(t => t.CompletedAt, StringComparer.Ordinal).ToList();
        Rows.Clear();
        foreach (var item in list)
        {
            var completed = IsoTime.ParseLocal(item.CompletedAt!);
            Rows.Add(new HistRowView(
                item.Id,
                MainViewService.RowText(item.Content),   // 行只显示一行（与待办/已完成/模板同一规则）
                OverdueService.IsCompletedOverdue(item),
                completed.ToString("HH:mm"),
                Selected: _batching && _selected.Contains(item.Id),
                BatchMode: _batching));
        }

        var date = ParseDate(_cur) ?? DateTime.Today;
        DateTitle.Text = $"{date.Month}月{date.Day}日 星期{"日一二三四五六"[(int)date.DayOfWeek]}";
        CountText.Text = $"{list.Count} 条";
        EmptyText.Text = _cur == today
            ? "今天完成的在「已完成」里，结算后进入历史"
            : "该日期暂无已完成事项";
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        PrevBtn.IsEnabled = !_batching && string.CompareOrdinal(_cur, earliest) > 0;
        NextBtn.IsEnabled = !_batching && string.CompareOrdinal(_cur, today) < 0;
        TodayBtn.IsEnabled = !_batching && _cur != today;
        CurDatePicker.IsEnabled = !_batching;

        NormalBar.Visibility = _batching ? Visibility.Hidden : Visibility.Visible;
        BatchEnterBtn.Visibility = _batching ? Visibility.Hidden : Visibility.Visible;
        BatchBar.Visibility = _batching ? Visibility.Visible : Visibility.Hidden;
        SelCountText.Text = $"已选 {_selected.Count} 项";
        BatchDeleteBtn.IsEnabled = _selected.Count > 0;
        BatchAllBtn.Content = _batching && list.Count > 0 && _selected.Count == list.Count ? "取消全选" : "全选";
        ExportPanel.Opacity = _batching ? 0.45 : 1.0;
        ExportPanel.IsHitTestVisible = !_batching;
    }

    private void UpdateSortIcon()
    {
        SortIcon.Data = (Geometry)FindResource(
            _controller.Data.Settings.DoneSort == "desc" ? "Icon.SortDesc" : "Icon.SortAsc");
    }

    // ---------- 导航 ----------

    private void Prev_Click(object sender, RoutedEventArgs e) => Navigate(-1);

    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(1);

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _cur = LocalDate.Key(DateTimeOffset.Now);
        ExitBatchSilently();
        SyncDatePicker();
        Render();
    }

    private void Date_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CurDatePicker.SelectedDate is not { } date)
        {
            return;
        }

        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var earliest = EarliestDate();
        var today = LocalDate.Key(DateTimeOffset.Now);
        key = key.CompareTo(earliest) < 0 ? earliest : (key.CompareTo(today) > 0 ? today : key);
        if (key == _cur)
        {
            return;
        }

        _cur = key;
        ExitBatchSilently();
        Render();
    }

    private void Navigate(int days)
    {
        _cur = ShiftDateKey(_cur, days);
        ExitBatchSilently();
        SyncDatePicker();
        Render();
    }

    private void SyncDatePicker() => CurDatePicker.SelectedDate = ParseDate(_cur);

    // ---------- 批量 ----------

    private void BatchEnter_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.Data.Archive.TryGetValue(_cur, out var bucket) && bucket.Count > 0)
        {
            _batching = true;
            _selected.Clear();
            Render();
        }
        else
        {
            _controller.ShowToast("该日期暂无可删除的记录");
        }
    }

    private void BatchExit_Click(object sender, RoutedEventArgs e)
    {
        ExitBatchSilently();
        Render();
    }

    private void BatchAll_Click(object sender, RoutedEventArgs e)
    {
        var ids = CurrentIds();
        if (ids.Count > 0 && ids.All(_selected.Contains))
        {
            _selected.Clear();
        }
        else
        {
            foreach (var id in ids)
            {
                _selected.Add(id);
            }
        }

        Render();
    }

    private void BatchDelete_Click(object sender, RoutedEventArgs e)
    {
        var count = _selected.Count;
        if (count == 0)
        {
            return;
        }

        if (!ConfirmDialog.Confirm($"删除所选 {count} 条历史记录？", "删除后无法恢复。", "删除", danger: true, anchor: this))
        {
            return;
        }

        if (_controller.DeleteArchivedMany(_cur, _selected.ToList()))
        {
            ShowToastStatic($"已删除 {count} 条");
        }

        ExitBatchSilently();
        Render();
    }

    /// <summary>点历史行：批量中=切换选中；常规态双击=只读详情（用 ClickCount，同主窗口径）。</summary>
    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HistRowView row)
        {
            return;
        }

        if (!_batching)
        {
            if (e.ClickCount == 2)
            {
                OpenDetail(row);
                e.Handled = true;
            }

            return;
        }

        if (_selected.Contains(row.Id))
        {
            _selected.Remove(row.Id);
        }
        else
        {
            _selected.Add(row.Id);
        }

        Render();
        e.Handled = true;
    }

    // ---------- 排序 / 详情 / 导出 ----------

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        _controller.ToggleDoneSort();
        UpdateSortIcon();
        Render();
    }

    private void OpenDetail(HistRowView row)
    {
        _controller.OpenDoneDetail(row.Id);
        Render(); // 详情内删除后同步本窗
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var from = FormatDate(FromPicker.SelectedDate);
        var to = FormatDate(ToPicker.SelectedDate);
        if (from is null || to is null)
        {
            ShowToastStatic("请选择起止日期");
            return;
        }

        _controller.ExportHistoryCsv(from, to);
    }

    // ---------- 工具 ----------

    private List<string> CurrentIds() =>
        (_controller.Data.Archive.TryGetValue(_cur, out var bucket) ? bucket : new List<TodoItem>())
            .Select(t => t.Id).ToList();

    private void ExitBatchSilently()
    {
        _batching = false;
        _selected.Clear();
    }

    private string EarliestDate()
    {
        var keys = _controller.Data.Archive.Keys.ToList();
        return keys.Count == 0
            ? ShiftDateKey(LocalDate.Key(DateTimeOffset.Now), -1)
            : keys.Min(StringComparer.Ordinal)!;
    }

    private static DateTime? ParseDate(string key) =>
        DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private static string? FormatDate(DateTime? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>日期键平移：统一走 Core 的 LocalDate.Shift（2026-11-06 去重）。</summary>
    private static string ShiftDateKey(string key, int days) => LocalDate.Shift(key, days);

    private void ShowToastStatic(string message) => _controller.ShowToast(message);

    private void Back_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>标题栏拖动：走 WindowShell 的物理像素实现（拖动中持续钳屏，需求「限屏内」）。
    /// 不能用 WPF 的 DragMove()——它没有范围限制，窗口能被拖出屏幕抓不回来（2026-11-06 查漏）。</summary>
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => WindowShell.BeginDialogDrag(this, e);
}
