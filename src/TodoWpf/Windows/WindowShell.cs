using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

    /// <summary>固定尺寸窗口：无缩放边框，仅系统圆角。</summary>
    public static void ApplyFixed(Window window)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
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

    /// <summary>
    /// 标题栏拖动（二级窗通用）：**不用 WPF 的 DragMove()**——它没有范围限制，
    /// 窗口能被拖出屏幕抓不回来（需求 §一 要求"可单独拖动（限屏内）"，2026-11-06 查漏发现）。
    /// 这里手写拖动：按住标题栏后每帧按**物理像素**算新位置，再钳进窗口所在屏的工作区。
    /// 与主窗拖动（ClusterController.DragMove_MouseMove）同一口径：拖动中持续钳制、不是松手才回弹。
    /// </summary>
    public static void BeginDialogDrag(Window window, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero
            || !MonitorLayout.TryGetCursorPosition(out var startCursorX, out var startCursorY))
        {
            return;
        }

        // 起点用"窗口左上角的屏幕物理坐标"，位移用光标物理位移 —— 全程物理像素，不碰 DIP
        var origin = window.PointToScreen(new Point(0, 0));
        var originX = (int)Math.Round(origin.X);
        var originY = (int)Math.Round(origin.Y);

        var dragging = true;

        void OnMove(object? _, MouseEventArgs args)
        {
            if (!dragging)
            {
                return;
            }

            if (args.LeftButton != MouseButtonState.Pressed)
            {
                End();
                return;
            }

            if (!MonitorLayout.TryGetCursorPosition(out var x, out var y))
            {
                return;
            }

            MonitorLayout.MovePhysicalClamped(
                hwnd,
                originX + (x - startCursorX),
                originY + (y - startCursorY));
        }

        void OnUp(object? sender, MouseButtonEventArgs args) => End();

        void End()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            window.MouseMove -= OnMove;
            window.MouseUp -= OnUp;
            if (window.IsMouseCaptured)
            {
                window.ReleaseMouseCapture();
            }

            // 拖动结束把 WPF 的 Left/Top 同步回真实位置（后续代码/定位逻辑读到的是新位置）
            var device = window.PointToScreen(new Point(0, 0));
            var scale = VisualTreeHelper.GetDpi(window);
            window.Left = device.X / scale.DpiScaleX;
            window.Top = device.Y / scale.DpiScaleY;
        }

        window.CaptureMouse();
        window.MouseMove += OnMove;
        window.MouseUp += OnUp;
    }
}
