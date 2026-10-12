using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>待办主窗（应用主页）：业务为假数据壳，窗口行为由 ClusterController 接管。</summary>
public partial class MainTodoWindow : Window
{
    public MainTodoWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>缩放柄命中区（自绘鼠标捕获，见控制器）。</summary>
    public FrameworkElement ScaleGripHandle => ScaleGrip;

    /// <summary>卡面根（缩放期间切换为不透明底以抑制拖影）。</summary>
    public Border Card => CardRoot;

    /// <summary>设计尺寸（XAML 中 CardRoot 的固定 Width/Height）。</summary>
    private const double DesignWidth = 470;

    private const double DesignHeight = 640;

    /// <summary>
    /// 缩放走**渲染层**（不触发布局，与其它窗同一渲染节拍）：卡面固定设计尺寸、锚点＝左上角
    /// （本窗钉住角＝锚点）。窗口矩形恒为"画布"、永不改变 ⇒ 无拖影、无按下/松手闪动。
    /// 同时把窗口命中区收窄到卡面（卡面之外那圈画布余量透明但仍会吞点击，实测确认）。
    /// </summary>
    public void SetContentScale(double scale)
    {
        var transform = (ScaleTransform)ContentScale;
        if (transform.ScaleX != scale)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }

        ApplyHitRegion(scale, 0);
    }

    /// <summary>把命中范围收窄到卡面（区域只影响裁剪/命中，不改窗口矩形或原点）。</summary>
    private void ApplyHitRegion(double scale, double offsetX)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || ActualWidth <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = (int)Math.Round(offsetX * dpi.DpiScaleX);
        var right = (int)Math.Round((offsetX + DesignWidth * scale) * dpi.DpiScaleX);
        var bottom = (int)Math.Round(DesignHeight * scale * dpi.DpiScaleY);

        var rgn = CreateRectRgn(left, 0, right, bottom);
        if (rgn != IntPtr.Zero)
        {
            SetWindowRgn(hwnd, rgn, true);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr hRgn, bool redraw);

    /// <summary>卡面内容（缩放期间冻结，只留窗壳）。</summary>
    public FrameworkElement CardBody => CardContent;

    public void SetCount(int current, int max) => CountText.Text = $"{current}/{max}";

    public void SetClock(string time, string date)
    {
        ClockTime.Text = time;
        ClockDate.Text = date;
    }

    /// <summary>⚡/☑ 开关态：开启=主题色描边+图标（成品 .icon-btn.on 口径）。</summary>
    public void SetPanelToggles(bool quickVisible, bool doneVisible)
    {
        ApplyToggleState(QuickBtn, quickVisible);
        ApplyToggleState(DoneBtn, doneVisible);
    }

    public void SetTopMost(bool on)
    {
        Topmost = on;
        ApplyTitlebarOnState(PinBtn, on);
    }

    public void SetTuckLock(bool on)
    {
        ApplyTitlebarOnState(TuckLockBtn, on);
        TuckLockBtn.ToolTip = on ? "缩进锁定：已开启（贴边保持弹出）" : "缩进锁定";
    }

    private void ApplyToggleState(Button button, bool on) =>
        button.Style = (Style)FindResource(on ? "IconButtonOn" : "IconButton");

    private void ApplyTitlebarOnState(Button button, bool on) =>
        button.Style = (Style)FindResource(on ? "TitlebarButtonOn" : "TitlebarButton");

    private void ThemeBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.ToggleTheme();

    private void PinBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.ToggleTopMost();

    private void TuckLockBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.ToggleTuckLock();

    private void HideBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.HideClusterToTray();

    private void QuickBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.ToggleQuick();

    private void DoneBtn_Click(object sender, RoutedEventArgs e) =>
        Controller?.ToggleDone();

    /// <summary>协调器（创建后注入）。</summary>
    public ClusterController? Controller { get; set; }
}
