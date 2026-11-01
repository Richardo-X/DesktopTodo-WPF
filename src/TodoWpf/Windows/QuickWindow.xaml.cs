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

    /// <summary>双击模板行 → 编辑。</summary>
    public System.Windows.Input.ICommand EditTemplateCommand { get; }

    public QuickWindow()
    {
        InitializeComponent();
        DataContext = this;
        EditTemplateCommand = new RelayCommand<TemplateRowView>(
            row => row is not null, row => Controller?.OpenEditTemplate(row.Id));
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

    /// <summary>整批刷新模板行 + 计数 + 空态。</summary>
    public void SetTemplateRows(IReadOnlyList<TemplateRowView> rows, int maxTemplates)
    {
        // 增量同步：新列表是旧列表的延长时只追加新增项，避免整表清空重建导致闪烁。
        var appendOnly = rows.Count >= TemplateRows.Count;
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

        TemplateCount.Text = $"{rows.Count}/{maxTemplates}";
        TemplateEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.DataContext is TemplateRowView row)
        {
            Controller?.ApplyTemplateRow(row.Id);
        }
    }

    /// <summary>卡面内容。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
