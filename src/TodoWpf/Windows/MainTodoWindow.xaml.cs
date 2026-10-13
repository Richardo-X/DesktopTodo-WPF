using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpf.Core.Models;
using TodoWpf.Core.Services;

namespace TodoWpf.Windows;

/// <summary>待办主窗（应用主页）：列表绑定真实数据；窗口行为由 ClusterController 接管。</summary>
public partial class MainTodoWindow : Window
{
    /// <summary>待办行（真实数据投影；ItemsSource 绑定）。</summary>
    public ObservableCollection<TodoRowView> Rows { get; } = new();

    public MainTodoWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>缩放柄命中区（自绘鼠标捕获，见控制器）。</summary>
    public FrameworkElement ScaleGripHandle => ScaleGrip;

    /// <summary>卡面根。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容按设计尺寸（470×640）绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale。
    /// 本窗左上角＝锚点（钉住角），窗口矩形按目标档逐帧摆位。
    /// </summary>
    public void SetContentScale(double scale)
    {
        var transform = (ScaleTransform)ContentScale;
        if (transform.ScaleX != scale)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    /// <summary>卡面内容。</summary>
    public FrameworkElement CardBody => CardContent;

    /// <summary>整批刷新待办行（替换式：行数少，整刷成本可忽略；避免逐条 diff 的复杂度）。</summary>
    public void SetRows(IReadOnlyList<TodoRowView> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        TodoEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Hidden;
    }

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
