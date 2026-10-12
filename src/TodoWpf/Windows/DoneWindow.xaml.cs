using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>已完成（当天）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class DoneWindow : Window
{
    public DoneWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根（缩放期间切换为不透明底）。</summary>
    public Border Card => CardRoot;

    /// <summary>按屏等比缩放内容（只改变换，不触发布局）。</summary>
    public void SetContentScale(double scale)
    {
        ContentScale.ScaleX = scale;
        ContentScale.ScaleY = scale;
    }

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
