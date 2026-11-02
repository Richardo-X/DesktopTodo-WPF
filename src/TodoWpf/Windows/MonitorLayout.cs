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

    private static WinRect PrimaryWorkAreaDips()
    {
        var area = SystemParameters.WorkArea;
        return new WinRect(area.Left, area.Top, area.Width, area.Height);
    }
}
