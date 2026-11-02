using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpf.Core.Models;
using TodoWpf.Core.Services;

namespace TodoWpf.Windows;

/// <summary>待办主窗（应用主页）：列表绑定真实数据；窗口行为由 ClusterController 接管。</summary>
public partial class MainTodoWindow : Window
{
    /// <summary>待办行（真实数据投影；ItemsSource 绑定）。</summary>
    public ObservableCollection<TodoRowView> Rows { get; } = new();

    /// <summary>已完成行（同窗下半段）。</summary>
    public ObservableCollection<DoneRowView> DoneRows { get; } = new();

    public MainTodoWindow()
    {
        InitializeComponent();
        DataContext = this;
        EditRowCommand = new RelayCommand<TodoRowView>(row => row is not null, row => Controller?.OpenEditTodo(row.Id));
        WindowShell.ApplyFixed(this);
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>缩放柄命中区（自绘鼠标捕获，见控制器）。</summary>
    public FrameworkElement ScaleGripHandle => ScaleGrip;

    /// <summary>卡面根。</summary>
    public Border Card => CardRoot;

    /// <summary>内容按设计尺寸（360×当前布局高度）绘制，缩放走 LayoutTransform。</summary>
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

    /// <summary>卡面内容。</summary>
    public FrameworkElement CardBody => CardContent;

    /// <summary>整批刷新待办行。</summary>
    public void SetRows(IReadOnlyList<TodoRowView> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        TodoEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>整批刷新已完成行。</summary>
    public void SetDoneRows(IReadOnlyList<DoneRowView> rows)
    {
        DoneRows.Clear();
        foreach (var row in rows)
        {
            DoneRows.Add(row);
        }

        DoneCount.Text = rows.Count.ToString();
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e) => Controller?.OpenNewTodo();

    private void BatchToggle_Click(object sender, RoutedEventArgs e) => Controller?.ToggleBatch();

    private void RowComplete_Checked(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.CheckBox)?.DataContext is not TodoRowView row)
        {
            return;
        }

        if (InBatch)
        {
            Controller?.ToggleBatchSelection(row.Id);
        }
        else
        {
            Controller?.CompleteRow(row.Id);
        }
    }

    /// <summary>批量条可见/计数/删除可用性由控制器推送。</summary>
    public void SetBatchBar(bool visible, int selected, bool canDelete)
    {
        InBatch = visible;
        ActionBar.Visibility = visible ? Visibility.Hidden : Visibility.Visible;
        BatchBar.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        BatchCount.Text = $"已选 {selected} 项";
        BatchDeleteBtn.IsEnabled = canDelete;
    }

    /// <summary>是否处于批量模式（勾选行为分支用）。</summary>
    public bool InBatch { get; private set; }

    private void BatchExit_Click(object sender, RoutedEventArgs e) => Controller?.ExitBatch();

    private void BatchDelete_Click(object sender, RoutedEventArgs e) => Controller?.DeleteSelected();

    /// <summary>双击行 → 编辑。</summary>
    public System.Windows.Input.ICommand EditRowCommand { get; }

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

    public void SetCount(int current, int max) => CountText.Text = $"{current}/{max}";

    public void SetClock(string time, string date)
    {
        ClockTime.Text = time;
        ClockDate.Text = date;
    }

    /// <summary>模板窗开关态：开启时 ⚡ 用主题色。</summary>
    public void SetPanelToggles(bool quickVisible, bool doneVisible)
    {
        // 原型 .sidebtn.on：边框与图标同时变强调色；仅改图标时夜间 Muted→Accent 差异过小，看不出来
        QuickBtn.Foreground = quickVisible
            ? (Brush)FindResource("Brush.Accent")
            : (Brush)FindResource("Brush.Muted");
        QuickBtn.BorderBrush = quickVisible
            ? (Brush)FindResource("Brush.Accent")
            : (Brush)FindResource("Brush.CardBorder");
    }

    public void SetTopMost(bool on)
    {
        Topmost = on;
        // 始终用动态资源引用：开→强调色，关→弱化色；两者都跟随主题切换。
        // 不能用 ClearValue：XAML 里的 DynamicResource 本身就是本地值，会被一并清掉导致图标消失。
        PinIcon.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            on ? "Brush.Accent" : "Brush.Faint");
    }

    public void SetTuckLock(bool on)
    {
        TuckLockBtn.Foreground = on ? (Brush)FindResource("Brush.Accent") : (Brush)FindResource("Brush.Faint");
        TuckLockBtn.ToolTip = on ? "缩进锁定：已开启（贴边保持弹出）" : "缩进锁定";
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleTheme();

    private void PinBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleTopMost();

    private void TuckLockBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleTuckLock();

    private void HideBtn_Click(object sender, RoutedEventArgs e) => Controller?.HideClusterToTray();

    private void QuickBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleQuick();

    private void DoneBtn_Click(object sender, RoutedEventArgs e) => Controller?.ToggleDone();

    private void DoneSort_Click(object sender, RoutedEventArgs e) => Controller?.ToggleDoneSort();

    /// <summary>协调器（创建后注入）。</summary>
    public ClusterController? Controller { get; set; }
}
