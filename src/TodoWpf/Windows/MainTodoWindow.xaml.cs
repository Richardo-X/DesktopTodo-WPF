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

    public void SetPanelToggles(bool quickVisible, bool doneVisible)
    {
        QuickBtn.Tag = (Geometry)FindResource(quickVisible ? "Icon.Bolt" : "Icon.Bolt");
        QuickBtn.Opacity = quickVisible ? 1 : 0.45;
        DoneBtn.Opacity = doneVisible ? 1 : 0.45;
    }

    public void SetTopMost(bool on)
    {
        PinBtn.Opacity = on ? 1 : 0.45;
        Topmost = on;
    }

    public void SetTuckLock(bool on)
    {
        TuckLockBtn.Opacity = on ? 1 : 0.45;
        TuckLockBtn.ToolTip = on ? "缩进锁定：已开启（贴边保持弹出）" : "缩进锁定";
    }

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
