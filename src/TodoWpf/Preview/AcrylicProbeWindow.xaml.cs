using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TodoWpf.Preview;

/// <summary>
/// 磨砂实验窗（C2 判定载体）：SetWindowCompositionAttribute ACCENT_ENABLE_ACRYLICBLURBEHIND。
/// 已知取舍：该通道 Win10 1803+/Win11 通用；拖动时的性能手感属于实验观察项。
/// </summary>
public partial class AcrylicProbeWindow : Window
{
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const uint AccentFlagsDrawAll = 2;

    /// <summary>磨砂底色（白天卡面色调 #F3F6FB、α≈60%）：SetWindowCompositionAttribute 用 AABBGGRR。</summary>
    private const uint AcrylicGradientColorDay = 0x99FBF6F3;

    private bool _acrylicOn;

    public AcrylicProbeWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SetAcrylic(true);
    }

    private void Titlebar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void AcrylicToggle_Click(object sender, RoutedEventArgs e) => SetAcrylic(!_acrylicOn);

    private void SetAcrylic(bool on)
    {
        _acrylicOn = on;
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        SetBlur(hwnd, on ? AcrylicGradientColorDay : null);
        AcrylicToggle.Content = on ? "磨砂：关" : "磨砂：开";
        StateText.Text = on ? "DWM blur behind 已启用" : "DWM blur behind 已关闭（半透明对照）";
    }

    private static void SetBlur(IntPtr hwnd, uint? gradientColor)
    {
        var policy = new AccentPolicy
        {
            AccentState = gradientColor is null ? AccentDisabled : AccentEnableAcrylicBlurBehind,
            AccentFlags = AccentFlagsDrawAll,
            GradientColor = gradientColor ?? 0,
        };

        var policySize = Marshal.SizeOf(policy);
        var policyPtr = Marshal.AllocHGlobal(policySize);
        try
        {
            Marshal.StructureToPtr(policy, policyPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.AccentPolicy,
                SizeOfData = policySize,
                Data = policyPtr,
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }
    }

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
