using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TodoWpf.Windows;

/// <summary>
/// 窗口外壳样式：组件簇窗口一律加 WS_EX_TOOLWINDOW（不进任务栏、不进 Alt+Tab，需求 §2.2 / v1.4 #8）。
/// 注意 ShowInTaskbar=false 只解决任务栏；Alt+Tab 由扩展样式 WS_EX_TOOLWINDOW 决定（批次 03 验收发现遗漏）。
/// 交互模拟器（x64）下使用 GetWindowLongPtrW/SetWindowLongPtrW。
/// </summary>
internal static class ToolWindowChrome
{
    private const int GwlExStyle = -20;

    private const long WsExToolWindow = 0x00000080;

    private const long WsExAppWindow = 0x00040000;

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var current = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var updated = (current | WsExToolWindow) & ~WsExAppWindow;
        if (updated != current)
        {
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(updated));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
