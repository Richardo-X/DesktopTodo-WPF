using System.Windows;
using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>
/// 详情窗（方案 H 420×438；体验说明书 ③.2）：已完成/历史事项只读查看。
/// 三行属性（创建/完成/状态）+ 1px 分隔线 + 无框正文；标题栏右端「删除」走确认框，
/// 删除由调用方回调执行（数据删除 + 关闭）。
/// </summary>
public partial class DetailDialog : Window
{
    private DetailDialog(string title, string createdText, string completedText,
                         bool overdue, string content, Action<DetailDialog> onConfirmedDelete,
                         ClusterController? controller)
    {
        InitializeComponent();
        // 2026-11-05 裁定：二级窗弃 WPF 投影，改 DWM 系统圆角（与主窗同壳）
        WindowShell.ApplyFixed(this);
        TitleText.Text = title;
        CreatedValue.Text = createdText;
        CompletedValue.Text = completedText;
        StateValue.Text = overdue ? "已逾期" : "未逾期";
        StateValue.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            overdue ? "Brush.Overdue" : "Brush.Muted");
        ContentView.Text = content;
        if (controller is not null)
        {
            DialogScale.Attach(this, CardRoot, ScaleGrip, "detail", 420, 438, controller);
        }

        DeleteBtn.Click += (_, _) =>
        {
            if (ConfirmDialog.Confirm(
                    $"删除这条{(title.Contains("历史") ? "历史" : "已完成")}事项？",
                    "删除后无法恢复。", "删除", danger: true, anchor: this))
            {
                onConfirmedDelete(this);
            }
        };
    }

    /// <summary>打开详情（模态，与主窗视觉矩形两轴居中）。</summary>
    public static void Show(string title, string createdText, string completedText,
                            bool overdue, string content, Action<DetailDialog> onConfirmedDelete,
                            ClusterController? controller = null)
    {
        var dialog = new DetailDialog(title, createdText, completedText, overdue, content, onConfirmedDelete, controller)
        {
            Owner = System.Windows.Application.Current.Windows
                .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办"),
        };
        CenterOnOwner(dialog);
        dialog.ShowDialog();
    }

    /// <summary>与主窗当前视觉矩形两轴居中（越界钳回所在屏工作区，需求 R2）。</summary>
    private static void CenterOnOwner(DetailDialog dialog)
    {
        var anchor = dialog.Owner ?? System.Windows.Application.Current.Windows
            .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办");
        if (anchor is null)
        {
            return;
        }

        // 同设置窗：物理像素定位必须在可见前（SourceInitialized）
        dialog.SourceInitialized += (_, _) => MonitorLayout.CenterOnOwnerPhysical(dialog, anchor, edgeDips: 8);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
