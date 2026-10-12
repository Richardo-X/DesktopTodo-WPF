using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 真 Acrylic 磨砂（C2 判定：做真 Acrylic，替代 Electron 版"半透明无模糊"）。
/// 通道：SetWindowCompositionAttribute + ACCENT_ENABLE_ACRYLICBLURBEHIND（Win10 1803+ / Win11 通用）。
/// 卡面令牌的 α 相应下调（真模糊下不再需要接近不透明），使壁纸隐约透出。
/// 已知取舍：模糊作用于窗口矩形；圆角外的像素会呈现模糊（若真机观感不佳，改用 Win11 圆角偏好或 Region 裁剪）。
/// </summary>
internal static class AcrylicBackdrop
{
    private const int AccentDisabled = 0;

    private const int AccentEnableAcrylicBlurBehind = 4;

    private const uint AccentFlagsDrawAll = 2;

    /// <summary>白天磨砂底色（#F3F6FB，α≈75%）：DWM 使用 AABBGGRR 字节序。</summary>
    private const uint DayTint = 0xBFFBF6F3;

    /// <summary>夜间磨砂底色（#1E1E21，α≈75%）。</summary>
    private const uint NightTint = 0xBF211E1E;

    public static void Apply(Window window, bool night)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        SetBlur(hwnd, night ? NightTint : DayTint);
    }

    private static void SetBlur(IntPtr hwnd, uint gradientColor)
    {
        var policy = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = AccentFlagsDrawAll,
            GradientColor = gradientColor,
        };

        var size = Marshal.SizeOf(policy);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.AccentPolicy,
                SizeOfData = size,
                Data = ptr,
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        _ = AccentDisabled; // 供将来"关闭磨砂"回退使用
    }

    // ---------- 实验通道 B：Win11 22H2+ 系统背板（Acrylic） ----------
    // 要求窗口"非逐像素透明"（AllowsTransparency=false），由 DWM 画背板并在系统层做圆角裁切。
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmwaWindowCornerPreference = 33;

    private const int DwmsbtTransientWindow = 3; // Acrylic

    private const int DwmwcpRound = 2;

    public static bool TrySystemBackdrop(Window window)
    {
        if (Environment.OSVersion.Version.Build < 22621)
        {
            return false; // Win11 22H2 起才支持系统背板
        }

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var backdrop = DwmsbtTransientWindow;
        var ok1 = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0;
        var round = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref round, sizeof(int));
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
        return ok1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    private enum WindowCompositionAttribute
    {
        AccentPolicy = 19,
    }

    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private struct AccentPolicy
    {
        public int AccentState;
        public uint AccentFlags;
        public uint GradientColor;

#pragma warning disable 649 // AnimationId 置 0（API 占位参数）
        public uint AnimationId;
#pragma warning restore 649
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
}
