using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// 原子窗口定位：把窗口几何（x, y, 宽, 高）作为**一次提交**交给系统。
///
/// 为什么必须"一次"：
///  WPF 的 <c>Window.Left</c> / <c>Width</c> 是两个独立属性，写入会各自触发一次 SetWindowPos。
///  逐帧缩放时先写宽度、后写位置，两者之间存在真实中间态——"已是新宽度、仍在旧位置"。
///  主窗（位置=锚点，不动）只有宽度变化，所以看不出问题；但**右缘钉在主窗左缘的快捷窗**
///  宽度先变大、左坐标还没来，渲染上就像先往右弹了一格再被拉回去（用户观察到的抖动）。
///  用一次提交就没有这个中间态。
///
/// 为什么用 DeferWindowPos 批量：
///  三张卡片是三个独立 HWND，逐个 SetWindowPos 时系统可能在两窗之间插入一次呈现，
///  造成"这一帧里三窗位置/尺寸不同步"。BeginDeferWindowPos/DeferWindowPos/EndDeferWindowPos
///  把三窗几何放进同一个批处理、一次性生效，尽量让它们同帧落位。
///
/// 坐标：内部为逻辑像素（DIP），提交前按各窗口 DPI 换算为物理像素并取整（避免亚像素重采样）。
/// </summary>
/// <summary>钉边区：该边在缩放中应保持精确不动（右缘钉出的窗口用 Right，其余用 Left）。</summary>
internal enum PinEdge
{
    Left,
    Right,
}

internal static class WindowPlacement
{
    private const uint SwpNoZOrder = 0x0004;

    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// 丢弃客户区旧像素（不搬旧内容）。
    /// 默认行为会把旧像素按**左上角**搬移后再补新区域，而我们是"右缘钉住、向左变宽"——
    /// 两套锚定方式不一致，就会在重绘前的一帧看到"画出来的右缘向左退一点再弹回"（用户报告的缩放抖动）。
    /// 加此标志让系统整块作废、交由应用当帧重画，从根上取消那次"旧内容贴新位置"。
    /// </summary>
    private const uint SwpNoCopyBits = 0x0100;

    /// <summary>一次提交单个窗口几何（非批量场景）。</summary>
    public static void SetBounds(Window window, WinRect rect) => SetBoundsBatch([(window, rect, PinEdge.Left)]);

    /// <summary>把一批窗口的几何放到同一个批处理里一次性提交（尽量同帧落位）。</summary>
    public static void SetBoundsBatch(IReadOnlyList<(Window Window, WinRect Rect, PinEdge Pin)> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        var batch = BeginDeferWindowPos(targets.Count);
        if (batch == IntPtr.Zero)
        {
            return;
        }

        foreach (var (window, rect, pin) in targets)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                continue;
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            var sx = dpi.DpiScaleX;
            var sy = dpi.DpiScaleY;

            // 两条边的物理像素坐标各自取整后，尺寸由二者相减得出：
            // 这样"钉住的那条边"精确落在同一物理像素上（逐帧缩放时不会 ±1px 抖动），
            // 自由边落在其理想位置的最近像素上。上边（Top）恒为钉住边。
            var leftPx = (int)Math.Round(rect.X * sx);
            var rightPx = (int)Math.Round(rect.Right * sx);
            var topPx = (int)Math.Round(rect.Y * sy);
            var bottomPx = (int)Math.Round(rect.Bottom * sy);

            var x = pin == PinEdge.Right ? leftPx : leftPx;
            var cx = Math.Max(1, rightPx - leftPx);
            var y = topPx;
            var cy = Math.Max(1, bottomPx - topPx);

            batch = DeferWindowPos(
                batch, hwnd, IntPtr.Zero, x, y, cx, cy, SwpNoZOrder | SwpNoActivate | SwpNoCopyBits);
            if (batch == IntPtr.Zero)
            {
                return; // 失败时放弃本批；下一帧会重新请求
            }
        }

        _ = EndDeferWindowPos(batch);
    }

    /// <summary>窗口右缘的物理像素（调试用）。返回 int.MinValue 表示拿不到句柄。</summary>
    public static int PhysicalRight(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return int.MinValue;
        }

        return rect.Right;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr BeginDeferWindowPos(int windowCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DeferWindowPos(
        IntPtr windowPosInfo, IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EndDeferWindowPos(IntPtr windowPosInfo);
}
