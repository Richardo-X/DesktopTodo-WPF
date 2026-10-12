using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>快捷添加（模板库）壳窗口：假数据；拖动联动由 ClusterController 接管。</summary>
public partial class QuickWindow : Window
{
    /// <summary>内容设计宽度（XAML 中 CardContent 的固定 Width）。</summary>
    private const double DesignWidth = 310;

    public QuickWindow()
    {
        InitializeComponent();

        // 窗口尺寸由控制器按帧设定：尺寸一到就重算左移量。
        // 只在 SetContentScale 里算不够——首次布局前 ActualWidth 还是 0，那时算出的位移是错的。
        SizeChanged += (_, _) => RefreshOffset();
    }

    public event EventHandler<MouseButtonEventArgs>? TitlebarPressed;

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e) =>
        TitlebarPressed?.Invoke(this, e);

    /// <summary>卡面根（缩放期间切换为不透明底）。</summary>
    public Border Card => CardRoot;

    /// <summary>
    /// 按屏等比缩放内容（只写变换，不触发布局——缩放逐帧平滑的关键）。
    /// 本窗钉住的是**右缘**：内容固定 310 宽，缩放到窗口宽度后若还差一点
    /// （窗口矩形是整数物理像素、排版宽度是逻辑像素，换算后常有零点几像素差），
    /// 就左移差额，保证内容右缘与窗口右缘逐帧重合。
    /// </summary>
    public void SetContentScale(double scale)
    {
        var scaleTransform = (ScaleTransform)ContentScale.Children[0];
        if (scaleTransform.ScaleX != scale)
        {
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;
        }

        RefreshOffset();
    }

    private void RefreshOffset()
    {
        var scale = ((ScaleTransform)ContentScale.Children[0]).ScaleX;
        var offset = DesignWidth * scale - ActualWidth;
        var translate = (TranslateTransform)ContentScale.Children[1];
        if (translate.X != offset)
        {
            translate.X = offset;
        }
    }

    /// <summary>卡面内容（缩放期间冻结）。</summary>
    public FrameworkElement CardBody => CardContent;

    public ClusterController? Controller { get; set; }
}
