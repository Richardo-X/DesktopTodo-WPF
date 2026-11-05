using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// 按窗口**所在显示器**取工作区（DIP）。
///
/// 为什么不能直接用 SystemParameters.WorkArea：它只返回**主显示器**的工作区。
/// 窗口拖到副屏（尤其是竖屏）后，钳制仍按主屏的高度算，窗口到主屏高度就上不去——
/// 用户看到的就是"竖屏中间有一条水平界线"。
/// </summary>
internal static class MonitorLayout
{
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>窗口所在显示器的工作区，单位 DIP。</summary>
    public static WinRect WorkAreaDips(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return PrimaryWorkAreaDips();
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return PrimaryWorkAreaDips();
        }

        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        var work = info.Work;
        return new WinRect(
            work.Left / scale,
            work.Top / scale,
            (work.Right - work.Left) / scale,
            (work.Bottom - work.Top) / scale);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// **鼠标当前所在显示器**的工作区（DIP）。
    /// 拖动时用它决定"整簇该落在哪块屏"：鼠标移到另一块屏 → 目标屏切换 → 整簇切过去
    /// （2026-11-04：此前按主窗所在屏钳制，导致主窗永远跨不过边界、彻底无法跨屏）。
    /// </summary>
    public static WinRect WorkAreaDipsAtCursor()
    {
        if (!GetCursorPos(out var point))
        {
            return PrimaryWorkAreaDips();
        }

        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return PrimaryWorkAreaDips();
        }

        uint dpiX = 96, dpiY = 96;
        if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0 || dpiX == 0)
        {
            dpiX = 96;
        }

        var scale = dpiX / 96.0;
        var work = info.Work;
        return new WinRect(
            work.Left / scale,
            work.Top / scale,
            (work.Right - work.Left) / scale,
            (work.Bottom - work.Top) / scale);
    }

    private static WinRect PrimaryWorkAreaDips()
    {
        var area = SystemParameters.WorkArea;
        return new WinRect(area.Left, area.Top, area.Width, area.Height);
    }

    // ---------- 物理像素定位（混合 DPI 多屏专用，2026-11-06） ----------

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfoNative
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfoNative(IntPtr monitor, ref MonitorInfoNative info);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// 把对话框用**物理像素**定位到「锚窗所在屏、两轴居中于锚窗」，越界钳回该屏工作区。
    /// 必须在 <c>SourceInitialized</c>（窗口可见之前）调用，两个原因（2026-11-06 实测）：
    /// ① 在 Loaded 里才设 Left/Top 时窗口已经显示过，用户会看到它从默认位置跳过来（残影）；
    /// ② 混合 DPI 下 Left/Top 按**窗口当前所在屏**的 DPI 解释（DIP 空间按屏不连续），
    ///    按锚窗 DIP 空间算出的坐标会被解释到别的屏去（实测确认框/设置窗被放到 2K 屏，应用在 4K 屏）。
    /// 尺寸不动（SWP_NOSIZE）——调用时 WPF 已按创建时的 DPI 定好物理尺寸。
    /// </summary>
    public static void CenterOnOwnerPhysical(Window dialog, Window owner, double edgeDips)
    {
        var hwnd = new WindowInteropHelper(dialog).Handle;
        var ownerHwnd = new WindowInteropHelper(owner).Handle;
        if (hwnd == System.IntPtr.Zero || ownerHwnd == System.IntPtr.Zero)
        {
            return;
        }

        if (!GetWindowRect(ownerHwnd, out var ownerRect) || !GetWindowRect(hwnd, out var dialogRect))
        {
            return;
        }

        var monitor = MonitorFromWindow(ownerHwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (monitor == System.IntPtr.Zero || !GetMonitorInfoNative(monitor, ref info))
        {
            return;
        }

        var work = info.Work;
        var edge = (int)System.Math.Round(edgeDips * GetDpiForWindow(ownerHwnd) / 96.0);

        // 尺寸也按目标屏的 DPI 换算：窗口一出生就在目标屏上、DIP 账目自洽，
        // 不会出现"先按主屏尺寸创建、跨屏后又被 WM_DPICHANGED 放大"的二段式跳变。
        var ownerDpi = GetDpiForWindow(ownerHwnd);
        var ownerScale = ownerDpi == 0 ? 1.0 : ownerDpi / 96.0;
        int w = (int)System.Math.Round(dialog.Width * ownerScale);
        int h = (int)System.Math.Round(dialog.Height * ownerScale);
        int x = ownerRect.Left + ((ownerRect.Right - ownerRect.Left) - w) / 2;
        int y = ownerRect.Top + ((ownerRect.Bottom - ownerRect.Top) - h) / 2;
        x = System.Math.Min(System.Math.Max(x, work.Left + edge), System.Math.Max(work.Left + edge, work.Right - w - edge));
        y = System.Math.Min(System.Math.Max(y, work.Top + edge), System.Math.Max(work.Top + edge, work.Bottom - h - edge));
        SetWindowPos(hwnd, System.IntPtr.Zero, x, y, w, h, SwpNoZOrder | SwpNoActivate);
    }
}
