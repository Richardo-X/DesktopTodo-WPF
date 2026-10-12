using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        _scale = scale;
        var transform = (ScaleTransform)ContentScale;
        if (transform.ScaleX != scale)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }

        ApplyHitRegion(scale);
    }

    /// <summary>当前缩放比（供区域重算）。</summary>
    private double _scale = 1;

    /// <summary>
    /// 命中区开关。**整体移动期间必须关掉**：窗口区域会裁剪绘制，而快速移动时窗口表面更新晚于
    /// 位置更新——区域会把"上一帧画面"按新位置裁一刀，表现为卡片缺一块、内容被切（用户 2026-10-13 截图）。
    /// 移动中不需要命中区，松手后再装上即可。
    /// </summary>
    public void SetHitRegionEnabled(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (enabled)
        {
            ApplyHitRegion(_scale);
        }
        else
        {
            SetWindowRgn(hwnd, IntPtr.Zero, true); // 清空区域 → 恢复整窗矩形
        }
    }

    // 卡面之外那圈"画布余量"是透明的，但它仍会**吞掉鼠标**（实测 WindowFromPoint 命中的是本窗），
    // 所以用窗口区域把命中范围收窄到卡面本身：区域外的点击穿透到桌面，区域内的照常。
    // 区域只影响裁剪与命中，不改变窗口矩形与原点，因此不会引入残影。
    private void ApplyHitRegion(double scale)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || ActualWidth <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var cardW = DesignWidth * scale;
        var cardH = DesignHeight * scale;
        var left = (int)Math.Round((ActualWidth - cardW) * dpi.DpiScaleX);
        var right = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        var bottom = (int)Math.Round(cardH * dpi.DpiScaleY);

        var rgn = CreateRectRgn(left, 0, right, bottom);
        if (rgn != IntPtr.Zero)
        {
            // SetWindowRgn 接管该区域的所有权（系统负责释放），成功后不能再 DeleteObject
            SetWindowRgn(hwnd, rgn, true);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr hRgn, bool redraw);

    /// <summary>卡面右缘在窗口内的 DIP 坐标（TraceResize 诊断用）。</summary>
    public double CardRightInWindowDip() =>
        CardRoot.TransformToAncestor(this).Transform(new Point(CardRoot.ActualWidth, 0)).X;

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
