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

    /// <summary>当前光标屏幕位置（物理像素）；失败返回 false。</summary>
    public static bool TryGetCursorPosition(out int x, out int y)
    {
        if (GetCursorPos(out var p))
        {
            x = p.X;
            y = p.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    /// <summary>窗口所在显示器句柄（hwnd 未建返回 Zero）。</summary>
    public static IntPtr MonitorOf(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == System.IntPtr.Zero
            ? System.IntPtr.Zero
            : MonitorFromWindow(hwnd, MonitorDefaultToNearest);
    }

    /// <summary>
    /// 主窗所在屏的**左/右/下**三侧是否各有相邻屏（跨屏拖动钳制放宽用，2026-11-09）。
    ///
    /// 为什么需要：拖动钳制按"当前屏 90 DIP 可见"封顶，窗口最多只露出 90 DIP；
    /// 而换屏判定要求窗口**大部分**落到另一块屏。当"主窗最小宽度的一半 &lt; 90"时
    /// （主屏 125% 时最小档宽 162 DIP、一半只有 81），中心永远差一点过不去
    /// → 卡在边缘、缩到最小也跨不了屏（用户 2026-11-09 实测）。
    /// 100% 缩放下最小宽 202.5、一半 101.25 刚好大于 90 才侥幸能过——属巧合，不是设计。
    /// 修法：该侧存在相邻屏时**不钳制**，让窗口越过交界；切屏后按新屏工作区重新钳制。
    /// 顶边不参与放宽（保留贴顶吸附语义）。
    /// </summary>
    public static (bool Left, bool Right, bool Bottom) NeighborEdges(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == System.IntPtr.Zero)
        {
            return (false, false, false);
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (monitor == System.IntPtr.Zero || !GetMonitorInfoNative(monitor, ref info))
        {
            return (false, false, false);
        }

        var m = info.Monitor;
        var cx = (m.Left + m.Right) / 2;
        var cy = (m.Top + m.Bottom) / 2;
        bool Beyond(int x, int y) =>
            MonitorFromPoint(new NativePoint { X = x, Y = y }, MonitorDefaultToNearest) != monitor;

        return (Beyond(m.Left - 64, cy), Beyond(m.Right + 64, cy), Beyond(cx, m.Bottom + 64));
    }

    /// <summary>
    /// 起点屏**右缘之外**是否还有别的屏（跨屏白板方向判定，2026-11-07 用户裁定：
    /// 白板只属于"跨屏过交界线"，同屏边缘交互不触发）。
    /// 做法：在右缘外 64px 处取探针点，最近屏若不是起点屏 = 该方向存在交界线。
    /// </summary>
    public static bool HasMonitorBeyondRight(IntPtr startMonitor, int probePhysicalY)
    {
        if (startMonitor == System.IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (!GetMonitorInfoNative(startMonitor, ref info))
        {
            return false;
        }

        var probe = new NativePoint { X = info.Monitor.Right + 64, Y = probePhysicalY };
        return MonitorFromPoint(probe, MonitorDefaultToNearest) != startMonitor;
    }

    /// <summary>起点屏**底缘之外**是否还有别的屏（口径同 HasMonitorBeyondRight）。</summary>
    public static bool HasMonitorBeyondBottom(IntPtr startMonitor, int probePhysicalX)
    {
        if (startMonitor == System.IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (!GetMonitorInfoNative(startMonitor, ref info))
        {
            return false;
        }

        var probe = new NativePoint { X = probePhysicalX, Y = info.Monitor.Bottom + 64 };
        return MonitorFromPoint(probe, MonitorDefaultToNearest) != startMonitor;
    }

    /// <summary>
    /// 拖动跨屏检测（2026-11-07 白板遮挡用）：主窗/模板窗的物理矩形并集
    /// 是否越出拖动开始时那块屏的**整屏边界**（任一像素出了起点屏即算跨屏行为）。
    /// </summary>
    public static bool IsCrossingMonitor(IntPtr startMonitor, IntPtr mainHwnd, IntPtr quickHwnd)
    {
        if (startMonitor == System.IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (!GetMonitorInfoNative(startMonitor, ref info))
        {
            return false;
        }

        foreach (var hwnd in new[] { mainHwnd, quickHwnd })
        {
            if (hwnd == System.IntPtr.Zero || !GetWindowRect(hwnd, out var r))
            {
                continue;
            }

            if (r.Left < info.Monitor.Left || r.Top < info.Monitor.Top
                || r.Right > info.Monitor.Right || r.Bottom > info.Monitor.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    /// 把物理像素点折算成"它所在屏"的 DIP 坐标（存档恢复用，2026-11-07）。
    /// 混合 DPI 下 DIP 空间按屏不连续，必须用**该点所在屏**的 DPI 折算，
    /// 不能用主窗当次的 DPI——否则 2K 存档会被按 4K 的 1.75 解释（恢复跨屏错位的根因）。
    /// </summary>
    public static WinPoint RawPointToDips(double rawX, double rawY)
    {
        var monitor = MonitorFromPoint(
            new NativePoint { X = (int)Math.Round(rawX), Y = (int)Math.Round(rawY) },
            MonitorDefaultToNearest);
        var scale = 1.0;
        if (monitor != IntPtr.Zero
            && GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out var dpiX, out var _) == 0
            && dpiX != 0)
        {
            scale = dpiX / 96.0;
        }

        var (x, y) = DpiSpace.RawToDips(rawX, rawY, scale);
        return new WinPoint(x, y);
    }

    /// <summary>
    /// 把窗口移到 (x, y)（物理像素），并**钳进它当前所在屏的工作区**（整窗可见）。
    /// 拖动中每帧调用：窗口跟着鼠标走、但永远留在屏内（需求 §一「可单独拖动（限屏内）」）。
    /// 用物理像素算：混合 DPI 下 DIP 空间按屏不连续，DIP 数学会算到别的屏去（2026-11-06 实测）。
    /// </summary>
    public static void MovePhysicalClamped(IntPtr hwnd, int x, int y)
    {
        if (hwnd == System.IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (monitor == System.IntPtr.Zero || !GetMonitorInfoNative(monitor, ref info))
        {
            SetWindowPos(hwnd, System.IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
            return;
        }

        var work = info.Work;
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        x = System.Math.Clamp(x, work.Left, System.Math.Max(work.Left, work.Right - w));
        y = System.Math.Clamp(y, work.Top, System.Math.Max(work.Top, work.Bottom - h));
        SetWindowPos(hwnd, System.IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    /// <summary>
    /// 把窗口放到 (x, y)、尺寸 (cx, cy)——全部**物理像素**，不钳制、不做任何 DPI 换算。
    /// 给"自身 DPI 不可信"的小窗用（白条：隐藏期间 DPI 停在旧屏，Show 时才更新，
    /// WPF Left/Top 两级换算的分子分母会不同代 → 位置飞出屏外，2026-11-07 实测）。
    /// </summary>
    public static void MovePhysicalSized(IntPtr hwnd, int x, int y, int cx, int cy)
    {
        if (hwnd == System.IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            hwnd, System.IntPtr.Zero, x, y, System.Math.Max(1, cx), System.Math.Max(1, cy),
            SwpNoZOrder | SwpNoActivate);
    }
}
