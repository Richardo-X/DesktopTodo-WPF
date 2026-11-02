using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// 悬停气泡（批次05①；原型 .bubble 口径 + 规格-窗口尺寸与缩放 §D 例外条款）：
/// **独立置顶穿透透明小窗**（2026-10-30 定：透明 + 圆角 + 投影的独立窗口，不受"系统外壳"方案约束）——
/// 行在窗缘时气泡可越出主窗；WS_EX_TRANSPARENT 保证纯展示层不接任何鼠标。
/// 显示条件 = 内容确实被截断（FormattedText 宽 > 可用宽 + 1，同原型 scrollWidth 判定）；
/// 默认弹行左侧、贴屏缘翻右侧；定位钳在工作区内；箭头纵向对准行中心。
/// </summary>
public partial class BubbleWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExNoActivate = 0x08000000;

    public BubbleWindow()
    {
        InitializeComponent();
        Opacity = 0; // 透明常驻
    }

    /// <summary>源初建：加穿透 + 工具窗 + 不激活扩展样式。</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var current = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(current | WsExTransparent | WsExToolWindow | WsExNoActivate));
    }

    /// <summary>内容是否被截断（判定 + 全文，供悬停入口预检）。</summary>
    public static bool IsTruncated(TextBlock tb, string text)
    {
        if (tb.ActualWidth <= 0 || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(tb);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            tb.Foreground,
            dpi.PixelsPerDip);
        return formatted.Width > tb.ActualWidth + 1;
    }

    /// <summary>
    /// 显示气泡。row=锚定行元素；scale=所属窗内容缩放比；workArea=所在屏工作区（DIP）。
    /// </summary>
    public void Show(FrameworkElement row, string text, double scale, WinRect workArea)
    {
        BubbleText.Text = text;
        RootScale.ScaleX = scale;
        RootScale.ScaleY = scale;
        UpdateLayout(); // 透明常驻窗保持可见布局，直接可测视觉尺寸

        var bw = ActualWidth;
        var bh = ActualHeight;
        if (bw <= 0 || bh <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(row);
        var rowTop = row.PointToScreen(new Point(0, 0));
        var rowX = rowTop.X / dpi.DpiScaleX;
        var rowY = rowTop.Y / dpi.DpiScaleY;
        var rowW = row.ActualWidth * scale;
        var rowH = row.ActualHeight * scale;
        var cy = rowY + rowH / 2;

        // 横向：默认行左侧（右缘 = 行左缘 - 14 缩放后）；贴屏缘翻右侧；再钳回工作区
        var gap = 14 * scale;
        const double edge = 8;
        var sideLeft = true;
        var x = rowX - gap - bw;
        if (x < workArea.X + edge)
        {
            sideLeft = false;
            x = rowX + rowW + gap;
        }

        x = Math.Min(Math.Max(x, workArea.X + edge), Math.Max(workArea.X + edge, workArea.Right - bw - edge));
        var y = Math.Min(Math.Max(cy - bh / 2, workArea.Y + edge), Math.Max(workArea.Y + edge, workArea.Bottom - bh - edge));

        // 箭头纵向对准行中心：屏幕距离 ÷ 缩放 = 窗内容局部坐标，再钳进泡体内
        var arrowTop = Math.Min(Math.Max((cy - y) / scale - 6, 10), bh / scale - 12);
        ColArrowLeft.Width = new GridLength(sideLeft ? 0 : 6);
        ColArrowRight.Width = new GridLength(sideLeft ? 6 : 0);
        ArrowLeft.Visibility = sideLeft ? Visibility.Collapsed : Visibility.Visible;
        ArrowRight.Visibility = sideLeft ? Visibility.Visible : Visibility.Collapsed;
        ArrowLeft.Margin = new Thickness(0, arrowTop, 0, 0);
        ArrowRight.Margin = new Thickness(0, arrowTop, 0, 0);

        Left = x;
        Top = y;

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>隐藏（瞬时归零；窗口保持常驻，规避再显示的闪现）。</summary>
    public new void Hide()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
