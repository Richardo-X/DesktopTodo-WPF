using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>快捷添加（模板库）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class QuickWindow : Window
{
    public QuickWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容层显隐（对照测试用；当前恒为可见）。
    /// </summary>
    public void SetContentVisible(bool visible) =>
        CardContent.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

    /// <summary>
    /// 内容按设计尺寸（310×640）绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale，
    /// 窗口矩形按目标档逐帧摆位——与主窗/已完成窗**同一套**。
    /// （曾试过"固定画布 + 渲染层缩放 + 窗口区域"，能消拖影，但会带来卡片/内容错位与裁剪，
    ///  用户 2026-10-13 复现后整体撤回：三窗统一最重要。）
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

    public ClusterController? Controller { get; set; }
}
