using System.Windows;
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

    public Thumb ScaleGripHandle => ScaleGrip;

    public void SetClock(string time, string date)
    {
        ClockTime.Text = time;
        ClockDate.Text = date;
    }

    /// <summary>⚡/☑ 开关态：开启=主题色描边+图标（成品 .icon-btn.on 口径），不做透明度变化。</summary>
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

    private void ApplyToggleState(Button button, bool on)
    {
        button.Foreground = (Brush)FindResource(on ? "Brush.Accent" : "Brush.Text");
        button.BorderBrush = (Brush)FindResource(on ? "Brush.Accent" : "Brush.BtnBorder");
    }

    private static void ApplyTitlebarOnState(Button button, bool on) =>
        button.Foreground = (Brush)Application.Current.Resources[on ? "Brush.Accent" : "Brush.Text"];

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
