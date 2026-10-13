using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpf.Core.Services;

namespace TodoWpf.Windows;

/// <summary>已完成（当天）壳窗口：列表绑定真实数据；窗口行为由 ClusterController 接管。</summary>
public partial class DoneWindow : Window
{
    /// <summary>已完成行（真实数据投影）。</summary>
    public ObservableCollection<DoneRowView> DoneRows { get; } = new();

    public DoneWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容按设计尺寸（310×640）绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale。
    /// 本窗钉住左上角——它的左缘跟着主窗右缘走，这一段平移由**窗口矩形**承担。
    /// </summary>
    public void SetContentScale(double scale)
    {
        var transform = (ScaleTransform)ContentScale;
        if (transform.ScaleX != scale)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    /// <summary>整批刷新已完成行 + 标题计数 + 空态。</summary>
    public void SetDoneRows(IReadOnlyList<DoneRowView> rows, int count)
    {
        DoneRows.Clear();
        foreach (var row in rows)
        {
            DoneRows.Add(row);
        }

        DoneCount.Text = $"已完成 · {count}";
        DoneEmpty.Visibility = count == 0 ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>卡面内容。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
