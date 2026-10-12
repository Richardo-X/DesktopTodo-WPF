using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 原子窗口定位：一次 <c>SetWindowPos</c> 同时提交 (x, y, 宽, 高)。
/// 逐帧场景（拖动/缩放）必须用它——WPF 的 Left/Width 是两笔独立写入，
/// 中间那一帧会呈现"新宽度 + 旧位置"的错位；右缘固定的快捷窗尤其明显
/// （宽度先变、位置后变 → 渲染上像往右弹一下再回来，即用户观察到的"抖动"）。
/// 坐标为逻辑像素（DIP），内部按窗口 DPI 换算为物理像素并取整。
/// </summary>
internal static class WindowPlacement
{
    private const uint SwpNoZOrder = 0x0004;

    private const uint SwpNoActivate = 0x0010;

    public static void SetBounds(Window window, double x, double y, double width, double height)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var px = (int)Math.Round(x * dpi.DpiScaleX);
        var py = (int)Math.Round(y * dpi.DpiScaleY);
        var pw = Math.Max(1, (int)Math.Round(width * dpi.DpiScaleX));
        var ph = Math.Max(1, (int)Math.Round(height * dpi.DpiScaleY));

        _ = SetWindowPos(hwnd, IntPtr.Zero, px, py, pw, ph, SwpNoZOrder | SwpNoActivate);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
