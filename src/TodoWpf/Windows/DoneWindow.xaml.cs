using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>已完成（当天）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class DoneWindow : Window
{
    public DoneWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根（缩放期间切换为不透明底）。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 内容按设计尺寸（310×640）绘制，缩放走 LayoutTransform：布局尺寸＝设计尺寸×scale。
    /// 本窗钉住左上角——它的左缘跟着主窗右缘走，这一段平移由**窗口矩形**承担。
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

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
