using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

    /// <summary>
    /// 内容层显隐（临时测试用）：隐藏后窗口里只剩卡面底色 + 1px 描边（Brush.WinBg / Brush.GlassEdge），
    /// 内容不再参与重绘。
    /// </summary>
    public void SetContentVisible(bool visible) =>
        CardContent.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

    /// <summary>卡面根（缩放期间切换为不透明底）。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容按设计尺寸绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale，
    /// 与控制器按同一 scale 算出的窗口矩形逐帧一致（不会有"内容与窗口差一截"的余量问题）。
    /// 左上角即本窗钉住角，钉住的边不随缩放位移。
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

    /// <summary>内容右缘在窗口内的 DIP 坐标（调试用：应恒等于窗口宽度）。</summary>
    public double ContentRightInDip() =>
        CardContent.TransformToAncestor(this).Transform(new Point(CardContent.ActualWidth, 0)).X;

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
