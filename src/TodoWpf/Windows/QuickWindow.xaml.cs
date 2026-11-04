using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpf.Core.Services;

namespace TodoWpf.Windows;

/// <summary>模板窗（主窗右侧）：列表绑定真实模板；窗口行为由 ClusterController 接管。</summary>
public partial class QuickWindow : Window
{
    /// <summary>模板行（真实数据投影）。</summary>
    public ObservableCollection<TemplateRowView> TemplateRows { get; } = new();

    public QuickWindow()
    {
        InitializeComponent();
        DataContext = this;
        WindowShell.ApplyFixed(this);
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e) =>
        Controller?.OpenNewTemplate();

    private sealed class RelayCommand<T> : System.Windows.Input.ICommand
        where T : class
    {
        private readonly Func<T?, bool> _can;
        private readonly Action<T?> _run;

        public RelayCommand(Func<T?, bool> can, Action<T?> run)
        {
            _can = can;
            _run = run;
        }

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? p) => _can(p as T);

        public void Execute(object? p) => _run(p as T);
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根。</summary>
    public Border Card => CardRoot;

    /// <summary>内容按设计尺寸（260×当前高度）绘制，缩放走 LayoutTransform。</summary>
    public void SetContentScale(double scale)
    {
        var transform = (ScaleTransform)ContentScale;
        if (transform.ScaleX != scale)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    /// <summary>设置设计画布高度（布局高度，不含缩放）。</summary>
    public void SetContentHeight(double height)
    {
        if (Math.Abs(CardContent.Height - height) > 0.01)
        {
            CardContent.Height = height;
        }
    }

    /// <summary>上一次刷新时的批量态（用于判断能否走"只追加"快路径）。</summary>
    private bool _rowsBatchActive;

    /// <summary>内容缩放比（气泡定位/尺寸换算用）。</summary>
    public double ContentScaleFactor => ((ScaleTransform)ContentScale).ScaleX;

    /// <summary>整批刷新模板行 + 计数 + 空态。批量为 true 时行投影带选中/批量标志。</summary>
    public void SetTemplateRows(IReadOnlyList<TemplateRowView> rows, int maxTemplates, bool batchActive = false)
    {
        // 增量同步：新列表是旧列表的延长时只追加新增项，避免整表清空重建导致闪烁。
        // ⚠ 但**批量态发生变化时必须整表重建**——否则退出批量时旧行的 Selected/BatchMode 不会被清掉，
        //   表现为"退出批量后仍显示选中样式"（用户 2026-11-04 实测）。
        var appendOnly = batchActive == _rowsBatchActive && rows.Count >= TemplateRows.Count;
        if (appendOnly)
        {
            for (var i = 0; i < TemplateRows.Count; i++)
            {
                if (TemplateRows[i].Id != rows[i].Id)
                {
                    appendOnly = false;
                    break;
                }
            }
        }

        if (appendOnly)
        {
            for (var i = TemplateRows.Count; i < rows.Count; i++)
            {
                TemplateRows.Add(rows[i]);
            }
        }
        else
        {
            TemplateRows.Clear();
            foreach (var row in rows)
            {
                TemplateRows.Add(row);
            }
        }

        _rowsBatchActive = batchActive;
        TemplateCount.Text = $"{rows.Count}/{maxTemplates}";
        TemplateEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 满 10 条：计数胶囊变强调色（原型 .cntchip.full）
        var full = rows.Count >= maxTemplates;
        TemplateCount.Foreground = full
            ? (Brush)FindResource("Brush.Accent")
            : (Brush)FindResource("Brush.Muted");
        TemplateCountChip.Background = full
            ? (Brush)FindResource("Brush.SoftAccent")
            : (Brush)FindResource("Brush.CounterChip");
    }

    /// <summary>
    /// 模板批量条推送（原型 #tplBatchbar 与 .tplfoot 同位等高替换）：
    /// 计数/删除可用性/全选文案 + ＋新建冻结（原型 btnTplNew.disabled = tplBatch）。
    /// </summary>
    public void SetTemplateBatch(bool active, int selected, bool canDelete, int total)
    {
        TemplateBatchBar.Visibility = active ? Visibility.Visible : Visibility.Hidden;
        ActionRow.Visibility = active ? Visibility.Hidden : Visibility.Visible;
        TemplateBatchCount.Text = $"已选 {selected} 项";
        TemplateBatchDeleteBtn.IsEnabled = canDelete;
        TemplateBatchAllBtn.Content = active && total > 0 && selected == total ? "取消全选" : "全选";
        AddTemplateBtn.IsEnabled = !active;
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.DataContext is TemplateRowView row)
        {
            Controller?.ApplyTemplateRow(row.Id);
        }
    }

    // ---------- 模板批量（批次05②） ----------

    private void TemplateBatchBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleTemplateBatch();

    private void TemplateBatchAll_Click(object sender, RoutedEventArgs e) => Controller?.ToggleBatchAll();

    private void TemplateBatchExit_Click(object sender, RoutedEventArgs e) => Controller?.ExitBatch();

    private void TemplateBatchDelete_Click(object sender, RoutedEventArgs e) => Controller?.DeleteSelected();

    /// <summary>批量态勾选框点击 = 切换选中（Click 不会被绑定回灌触发，无双切）。</summary>
    private void TplCheck_Click(object sender, RoutedEventArgs e)
    {
        if (Controller is not { IsTemplateBatch: true }
            || (sender as System.Windows.Controls.CheckBox)?.DataContext is not TemplateRowView row)
        {
            return;
        }

        Controller.ToggleBatchSelection(row.Id);
    }

    /// <summary>批量切选就地改投影（INPC），不重建容器。</summary>
    public void UpdateRowSelection(string id, bool selected)
    {
        foreach (var row in TemplateRows)
        {
            if (row.Id == id)
            {
                row.Selected = selected;
                return;
            }
        }
    }

    /// <summary>
    /// 点模板行：批量中=切换选中；常规态双击=编辑（同主窗：用 ClickCount 而非 MouseBinding 双击手势，
    /// 避免吞掉快速连点的第 2 击）。
    /// </summary>
    private void TplRow_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as System.Windows.Controls.Border)?.DataContext is not TemplateRowView row)
        {
            return;
        }

        if (Controller is { IsTemplateBatch: true })
        {
            Controller.ToggleBatchSelection(row.Id);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            Controller?.OpenEditTemplate(row.Id);
            e.Handled = true;
        }
    }

    // ---------- 悬停气泡（批次05①） ----------

    private void TplText_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is TextBlock tb)
        {
            Controller?.HoverText(tb, RowOf(tb));
        }
    }

    private void TplText_MouseLeave(object sender, MouseEventArgs e) => Controller?.EndHover();

    /// <summary>模板行容器：文本向上找首个 Grid（行根）。</summary>
    private static FrameworkElement RowOf(TextBlock tb)
    {
        DependencyObject node = tb;
        while (System.Windows.Media.VisualTreeHelper.GetParent(node) is { } parent)
        {
            if (parent is System.Windows.Controls.Grid)
            {
                return (FrameworkElement)parent;
            }

            node = parent;
        }

        return tb;
    }

    /// <summary>卡面内容。</summary>
    public FrameworkElement CardBody => CardContent;

    /// <summary>
    /// 批量态整窗冻结（原型 .frozen-win：可读不可点，opacity .7）。
    /// 根 Border 断命中 ⇒ 标题栏拖动/行点击/按钮全部失效，内容照常显示。
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        CardRoot.IsHitTestVisible = !frozen;
        CardRoot.Opacity = frozen ? 0.7 : 1.0;
    }

    public ClusterController? Controller { get; set; }
}
