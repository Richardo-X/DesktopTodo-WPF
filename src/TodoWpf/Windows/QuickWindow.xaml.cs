using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>快捷添加（模板库）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class QuickWindow : Window
{
    /// <summary>设计尺寸（XAML 中 CardRoot 的固定 Width/Height）。</summary>
    private const double DesignWidth = 310;

    private const double DesignHeight = 640;

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
    /// 内容按设计尺寸绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale。
    /// 卡面（底色+描边）也跟着"目标档"走而不是铺满窗口——拖动缩放时窗口矩形会预先放大到
    /// 本次上限（方案 D：矩形冻结，避免合成器拿旧表面按新矩形重贴），卡面若铺满窗口就会
    /// 出现"一按角标整块卡跳成最大"（用户 2026-10-13 反馈）。卡面右上角对齐，
    /// 于是多出来的窗口区域全透明、不可见。
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

    /// <summary>卡面右缘在窗口内的 DIP 坐标（TraceResize 诊断用）。</summary>
    public double CardRightInWindowDip() =>
        CardRoot.TransformToAncestor(this).Transform(new Point(CardRoot.ActualWidth, 0)).X;

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
