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
            dialog.Loaded += (_, _) =>
            {
                var workArea = MonitorLayout.WorkAreaDips(dialog.Owner);
                const double edge = 8;
                dialog.Left = Math.Min(Math.Max(
                    dialog.Owner.Left + (dialog.Owner.ActualWidth - dialog.ActualWidth) / 2,
                    workArea.X + edge), Math.Max(workArea.X + edge, workArea.Right - dialog.ActualWidth - edge));
                dialog.Top = Math.Min(Math.Max(
                    dialog.Owner.Top + (dialog.Owner.ActualHeight - dialog.ActualHeight) / 2,
                    workArea.Y + edge), Math.Max(workArea.Y + edge, workArea.Bottom - dialog.ActualHeight - edge));
            };
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
        _controller.SetAutoStart(on); // 注册表失败时控制器只提示不改数据；下方回读真实状态
        AutoStartSwitch.IsChecked = _controller.Data.Settings.AutoStart;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
