using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 二级窗等比缩放（原型 wireDialogGrip 口径；用户 2026-11-04 裁定：二级窗只做等比缩放、不做长度缩放）：
/// 右下手柄拖动 = 横向位移 + 纵向位移×(高/宽) 折算，500px ≈ ±1.0 档，钳 0.7~1.2；
/// 内容 LayoutTransform + 窗口尺寸同步（左上角不动、向右下生长）；按组存 UiState.ChildScales。
/// </summary>
internal static class DialogScale
{
    public const double Min = 0.7;
    public const double Max = 1.2;

    public static double Get(ClusterController? controller, string group) =>
        controller?.Data.UiState.ChildScales.TryGetValue(group, out var v) == true
            ? Math.Clamp(v, Min, Max)
            : 1.0;

    public static void Attach(Window win, FrameworkElement root, FrameworkElement grip,
                              string group, double baseW, double baseH, ClusterController? controller)
    {
        var scale = Get(controller, group);
        var transform = new ScaleTransform(scale, scale);
        root.LayoutTransform = transform;
        win.Width = baseW * scale;
        win.Height = baseH * scale;

        Point startMouse = default;
        double startScale = scale;
        var dragging = false;

        grip.MouseLeftButtonDown += (_, e) =>
        {
            dragging = true;
            startMouse = ToDip(win, e);
            startScale = transform.ScaleX;
            _ = grip.CaptureMouse();
            e.Handled = true;
        };
        grip.MouseMove += (_, e) =>
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var m = ToDip(win, e);
            var next = Math.Clamp(
                startScale + ((m.X - startMouse.X) + (m.Y - startMouse.Y) * (baseH / baseW)) / 500,
                Min, Max);
            if (Math.Abs(next - transform.ScaleX) < 0.001)
            {
                return;
            }

            Apply(transform, win, baseW, baseH, next);
        };
        grip.MouseLeftButtonUp += (_, _) => End();
        grip.LostMouseCapture += (_, _) => End();

        void End()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (grip.IsMouseCaptured)
            {
                grip.ReleaseMouseCapture();
            }

            controller?.SetChildScale(group, transform.ScaleX);
        }
    }

    private static void Apply(ScaleTransform transform, Window win, double baseW, double baseH, double s)
    {
        transform.ScaleX = s;
        transform.ScaleY = s;
        win.Width = baseW * s;
        win.Height = baseH * s;
    }

    /// <summary>指针屏幕位置（DIP）。PointToScreen 是物理像素，按窗口 DPI 换算。</summary>
    private static Point ToDip(Window win, MouseEventArgs e)
    {
        var device = win.PointToScreen(e.GetPosition(win));
        var dpi = VisualTreeHelper.GetDpi(win);
        return new Point(device.X / dpi.DpiScaleX, device.Y / dpi.DpiScaleY);
    }
}
