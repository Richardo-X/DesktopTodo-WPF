using System.Windows;
using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>缩进白条窗：命中事件上报 ClusterController。</summary>
public partial class TuckBarWindow : Window
{
    public TuckBarWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? BarHit;

    private void RaiseHit() => BarHit?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 弹出途径**只有**"鼠标碰到白条本体"（用户 2026-11-04 裁定 D5，照 ele BarWindow：
    /// 仅 mouseenter，无点击弹出）。mouseenter 是系统真实命中分发——被其他窗口遮挡时天然不弹。
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        RaiseHit();
    }

    public ClusterController? Controller { get; set; }
}
