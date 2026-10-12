using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

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

    /// <summary>设计尺寸（XAML 中 CardRoot 的固定 Width/Height）。</summary>
    private const double DesignWidth = 310;

    private const double DesignHeight = 640;

    /// <summary>
    /// 缩放走渲染层（不触发布局）。本窗钉住左上角，但它的左缘跟着主窗右缘走
    /// （缩放时会平移）——所以卡面除缩放外还要渲染层平移：窗口矩形恒定不变 ⇒ 无拖影、无闪动。
    /// </summary>
    /// <param name="scale">目标缩放比。</param>
    /// <param name="offsetX">卡面相对画布左缘的平移量（设计单位）。</param>
    public void SetContentTransform(double scale, double offsetX)
    {
        var group = (TransformGroup)ContentTransform;
        var scaleTransform = (ScaleTransform)group.Children[0];
        if (scaleTransform.ScaleX != scale)
        {
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;
        }

        var offset = (TranslateTransform)group.Children[1];
        if (offset.X != offsetX)
        {
            offset.X = offsetX;
        }

        ApplyHitRegion(scale, offsetX);
    }

    /// <summary>把命中范围收窄到卡面（区域只影响裁剪/命中，不改窗口矩形或原点）。</summary>
    private void ApplyHitRegion(double scale, double offsetX)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || ActualWidth <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = (int)Math.Round(offsetX * dpi.DpiScaleX);
        var right = (int)Math.Round((offsetX + DesignWidth * scale) * dpi.DpiScaleX);
        var bottom = (int)Math.Round(DesignHeight * scale * dpi.DpiScaleY);

        var rgn = CreateRectRgn(left, 0, right, bottom);
        if (rgn != IntPtr.Zero)
        {
            SetWindowRgn(hwnd, rgn, true);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr hRgn, bool redraw);

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
