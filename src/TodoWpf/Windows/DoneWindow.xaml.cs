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

    /// <summary>
    /// 内容按设计尺寸（310×640）绘制，缩放由 Viewbox（Stretch=Fill）随窗口尺寸自动完成；
    /// 左上角即本窗钉住角，钉住的边不随缩放位移。
    /// </summary>
    public void SetContentScale(double scale)
    {
        // Viewbox 随窗口尺寸自动等比缩放（Stretch=Fill），无需逐帧写变换。
    }

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
