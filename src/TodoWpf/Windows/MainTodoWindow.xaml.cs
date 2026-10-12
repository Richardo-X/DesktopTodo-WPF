using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    /// <summary>卡面内容（缩放期间冻结，只留窗壳）。</summary>
    public Viewbox CardBody => CardContent;

    public void SetCount(int current, int max) => CountText.Text = $"待办 {current}/{max}";

    public void SetClock(string time, string date)
    {
        ClockTime.Text = time;
        ClockDate.Text = date;
    }

    /// <summary>⚡/☑ 开关态：开启=主题色描边+图标（成品 .icon-btn.on 口径），不做透明度变化。</summary>
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
