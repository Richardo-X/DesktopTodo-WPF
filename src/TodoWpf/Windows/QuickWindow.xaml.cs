using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>快捷添加（模板库）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class QuickWindow : Window
{
    public QuickWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根（缩放期间切换为不透明底）。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容按设计尺寸（310×640）绘制，缩放由 Viewbox（Stretch=Fill）随窗口尺寸自动完成：
    /// 本窗钉住右缘（右对齐），窗口变宽变窄时右缘恒贴窗口右缘。
    /// </summary>
    public void SetContentScale(double scale)
    {
    }

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
