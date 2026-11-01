using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;

namespace TodoWpf.Windows;

/// <summary>
/// 新外观的窗口外壳：不透明 + 系统外壳（WindowChrome）。
/// 只把底边 8px 交给系统做缩放，宽度由窗口自身锁死；圆角走 Win11 DWM。
/// </summary>
internal static class WindowShell
{
    private const int DwmwaWindowCornerPreference = 33;

    private const int DwmwcpRound = 2;

    /// <summary>挂上系统外壳：底边可拖拽改高，其余三边不可拖。</summary>
    public static void Apply(Window window)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0, 0, 0, 8),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        window.SourceInitialized += (_, _) => ApplyRoundCorners(window);
    }

    /// <summary>Win11 系统圆角；Win10 上调用失败即保持直角。</summary>
    private static void ApplyRoundCorners(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var preference = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
