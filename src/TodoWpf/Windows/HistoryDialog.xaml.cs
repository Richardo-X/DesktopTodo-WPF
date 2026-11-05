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
        _cur = LatestDateKey();
        if (_cur == today)
        {
            _cur = ShiftDateKey(today, -1); // 默认看昨天（原型 openHistory 默认 yesterdayKey）
        }

        CurDatePicker.DisplayDateStart = ParseDate(EarliestDate());
        // 上限 = 今天（用户 2026-11-05）：历史里没有未来记录，未来日期灰显不可点
        CurDatePicker.DisplayDateEnd = ParseDate(today);
        CurDatePicker.SelectedDate = ParseDate(_cur);
        FromPicker.SelectedDate = ParseDate(EarliestDate());
        FromPicker.DisplayDateEnd = ParseDate(today);
        ToPicker.SelectedDate = ParseDate(today);
        ToPicker.DisplayDateEnd = ParseDate(today);
        DialogScale.Attach(this, CardRoot, ScaleGrip, "history", 420, 560, controller);
        UpdateSortIcon();
        Render();
    }

    /// <summary>打开历史记录（模态；从设置窗打开时嵌套）。</summary>
    public static void Show(ClusterController controller)
    {
        var dialog = new HistoryDialog(controller)
        {
            Owner = System.Windows.Application.Current.Windows
                .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办"),
        };
        if (dialog.Owner is not null)
        {
            dialog.Loaded += (_, _) =>
            {
                var workArea = MonitorLayout.WorkAreaDips(dialog.Owner);
                const double edge = 8;
                dialog.Left = dialog.Owner.Left + (dialog.Owner.ActualWidth - dialog.ActualWidth) / 2;
                dialog.Top = Math.Min(Math.Max(
                    dialog.Owner.Top + (dialog.Owner.ActualHeight - dialog.ActualHeight) / 2,
                    workArea.Y + edge), Math.Max(workArea.Y + edge, workArea.Bottom - dialog.ActualHeight - edge));
            };
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
                item.Content,
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

    private string LatestDateKey()
    {
        var keys = _controller.Data.Archive.Keys.ToList();
        // keys 非空时 Max/Min 必非 null（Count==0 已由上一分支处理）
        return keys.Count == 0
            ? ShiftDateKey(LocalDate.Key(DateTimeOffset.Now), -1)
            : keys.Max(StringComparer.Ordinal)!;
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

    private static string ShiftDateKey(string key, int days) =>
        (ParseDate(key) ?? DateTime.Today).AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private void ShowToastStatic(string message) => _controller.ShowToast(message);

    private void Back_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
