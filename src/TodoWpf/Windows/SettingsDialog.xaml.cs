using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>
/// 设置窗（原型 openSettings + 体验说明书 ③.3；420×560）：
/// 待办上限步进（不可低于当前待办数量→置灰）/ 时制 seg / 主题 seg（即时生效落盘）/
/// 历史记录入口 / 导入历史（覆盖先确认）/ 恢复默认尺寸 / 开机自启 switch。全部即时生效并落盘。
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly ClusterController _controller;

    private SettingsDialog(ClusterController controller)
    {
        InitializeComponent();
        // 2026-11-05 裁定：二级窗弃 WPF 投影，改 DWM 系统圆角（与主窗同壳）
        WindowShell.ApplyFixed(this);
        _controller = controller;
        DialogScale.Attach(this, CardRoot, ScaleGrip, "settings", 420, 560, controller);
        Refresh();
    }

    /// <summary>打开设置窗（模态，与主窗两轴居中钳屏）。</summary>
    public static void Show(ClusterController controller)
    {
        var dialog = new SettingsDialog(controller)
        {
            Owner = System.Windows.Application.Current.Windows
                .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办"),
        };
        if (dialog.Owner is not null)
        {
            // 物理像素定位必须在窗口可见前（SourceInitialized）：
            // ① Loaded 时窗口已显示过，用户会看到它从默认位置跳到定位处（残影，2026-11-06 实测日志：
            //    SourceInitialized=(26,26) → Loaded 移动后=(713,547)）；
            // ② 混合 DPI 下 Left/Top 按当前所在屏的 DPI 解释，DIP 空间不连续 → 会落到别的屏。
            dialog.SourceInitialized += (_, _) => MonitorLayout.CenterOnOwnerPhysical(dialog, dialog.Owner, edgeDips: 8);
        }

        dialog.ShowDialog();
    }

    private void Refresh()
    {
        var settings = _controller.Data.Settings;
        MaxValue.Text = settings.MaxItems.ToString();
        // 「−」始终可点：到下限再点给 toast（用户反馈置灰看不出来也没有提示；开发自定决策#7）
        MinusBtn.IsEnabled = true;
        PlusBtn.IsEnabled = settings.MaxItems < TodoWpf.Core.Models.AppSettings.MaxMaxItems;

        Seg24.IsChecked = !settings.Hour12;
        Seg12.IsChecked = settings.Hour12;
        // 主题分段已移除（用户 2026-11-05：标题栏切换即可）
        AutoStartSwitch.IsChecked = settings.AutoStart;
    }

    // ---------- 各设置项 ----------

    private void Minus_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.Data.Settings.MaxItems - 1 < Math.Max(1, _controller.Data.Todos.Count))
        {
            _controller.ShowToast("不可低于当前待办数量");
            return;
        }

        _controller.SetMaxItems(_controller.Data.Settings.MaxItems - 1);
        Refresh();
    }

    private void Plus_Click(object sender, RoutedEventArgs e)
    {
        _controller.SetMaxItems(_controller.Data.Settings.MaxItems + 1);
        Refresh();
    }

    private void Hour_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && (Seg12.IsChecked == true) != _controller.Data.Settings.Hour12)
        {
            _controller.SetHour12(Seg12.IsChecked == true);
        }
    }

    private void History_Click(object sender, RoutedEventArgs e) => _controller.OpenHistory();

    private void ImportCover_Click(object sender, RoutedEventArgs e) =>
        _controller.ImportHistoryCsv(CsvModeCover, this);

    private void ImportMerge_Click(object sender, RoutedEventArgs e) =>
        _controller.ImportHistoryCsv(CsvModeMerge, this);

    private const string CsvModeCover = TodoWpf.Core.Services.CsvService.ModeCover;
    private const string CsvModeMerge = TodoWpf.Core.Services.CsvService.ModeMerge;

    private void ResetSize_Click(object sender, RoutedEventArgs e)
    {
        _controller.ResetWindowSize();
        Close();
    }

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        var on = AutoStartSwitch.IsChecked == true;
        AnimateSwitchKnob(on);   // 只有用户操作才播滑动动画（初值设定不播，见样式注释）
        _controller.SetAutoStart(on); // 注册表失败时控制器只提示不改数据；下方回读真实状态
        AutoStartSwitch.IsChecked = _controller.Data.Settings.AutoStart;
    }

    /// <summary>
    /// 开关滑块滑动动画（用户 2026-11-06 起改由代码触发）：只在**用户点击**时播，
    /// 打开设置页设定初值时不播（否则每次开窗都会看到一遍右滑）。
    /// FillBehavior=Stop：动画结束后回到触发器 Setter 的静态值（=动画终点），不会长期霸占属性。
    /// </summary>
    private void AnimateSwitchKnob(bool on)
    {
        if (AutoStartSwitch.Template?.FindName("knob", AutoStartSwitch)
            is not System.Windows.Shapes.Ellipse knob)
        {
            return;
        }

        knob.BeginAnimation(
            FrameworkElement.MarginProperty,
            new System.Windows.Media.Animation.ThicknessAnimation(
                fromValue: new Thickness(on ? 3 : 22, 0, 0, 0),
                toValue: new Thickness(on ? 22 : 3, 0, 0, 0),
                duration: TimeSpan.FromMilliseconds(160))
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                },
            });
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>标题栏拖动：走 WindowShell 的物理像素实现（拖动中持续钳屏，需求「限屏内」）。
    /// 不能用 WPF 的 DragMove()——它没有范围限制，窗口能被拖出屏幕抓不回来（2026-11-06 查漏）。</summary>
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => WindowShell.BeginDialogDrag(this, e);
}
