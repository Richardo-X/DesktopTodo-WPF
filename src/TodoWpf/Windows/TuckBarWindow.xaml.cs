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

    public event EventHandler? BarPressed;

    private void RaiseHit() => BarHit?.Invoke(this, EventArgs.Empty);

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        RaiseHit();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        BarPressed?.Invoke(this, EventArgs.Empty);
        RaiseHit();
    }

    public ClusterController? Controller { get; set; }
}
