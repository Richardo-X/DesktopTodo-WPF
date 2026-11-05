using System.Windows;
using System.Windows.Controls;

namespace TodoWpf.Windows;

/// <summary>
/// 确认框（原型 .cbox，D 类）：340 × 内容高；定位 = 居中于「打开它的那个窗口」
/// （原型 confirmBox 口径：优先调用方传入的锚窗），越界钳回所在屏工作区（8px 边距）。
/// 危险操作（删除）用红字红边确认键。
/// </summary>
public partial class ConfirmDialog : Window
{
    private bool _confirmed;

    private ConfirmDialog(string title, string body, string okText, bool danger)
    {
        InitializeComponent();
        // 2026-11-05 裁定：二级窗弃 WPF 投影（投影会从圆角缺口透出显脏），改 DWM 系统圆角（与主窗同壳）
        WindowShell.ApplyFixed(this);
        TitleText.Text = title;
        BodyText.Text = body;
        BodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
        OkBtn.Content = okText;
        if (danger)
        {
            OkBtn.Style = (Style)FindResource("DangerSmBtn");
        }
    }

    /// <summary>显示确认框；返回 true=用户点了确定。anchor=打开确认框的窗口（缺省主窗）。</summary>
    public static bool Confirm(string title, string body, string okText = "确定",
                               bool danger = false, Window? anchor = null)
    {
        var dialog = new ConfirmDialog(title, body, okText, danger);

        // **必须在 ShowDialog 前设 Owner**：没有 Owner 时 WPF 用主屏的 DPI 解释 Left/Top，
        // 在混合 DPI（2K@100% + 4K@175%）下会把确认框放到主屏去 —— 用户 2026-11-05 实测：
        // 覆盖导入 / 批量删除的确认框出现在 2K 屏，而应用在 4K 屏。
        // 另外四个二级窗本来就设了 Owner，所以只有确认框有这个毛病。
        anchor ??= System.Windows.Application.Current.Windows
            .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办");
        if (anchor is not null && !ReferenceEquals(anchor, dialog))
        {
            dialog.Owner = anchor;
        }

        dialog.CenterOn(anchor);
        dialog.ShowDialog();
        return dialog._confirmed;
    }

    /// <summary>居中于锚窗（两轴），越界钳回所在屏工作区（原型 EDGE=8 口径）。</summary>
    private void CenterOn(Window? anchor)
    {
        var loaded = new RoutedEventHandler((_, _) =>
        {
            if (anchor is null)
            {
                return;
            }

            var workArea = MonitorLayout.WorkAreaDips(anchor);
            const double edge = 8;
            var x = anchor.Left + (anchor.ActualWidth - ActualWidth) / 2;
            var y = anchor.Top + (anchor.ActualHeight - ActualHeight) / 2;
            Left = Math.Min(Math.Max(x, workArea.X + edge), Math.Max(workArea.X + edge, workArea.Right - ActualWidth - edge));
            Top = Math.Min(Math.Max(y, workArea.Y + edge), Math.Max(workArea.Y + edge, workArea.Bottom - ActualHeight - edge));
        });
        Loaded += loaded;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
