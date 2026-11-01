namespace TodoWpf.Core.Windows;

/// <summary>
/// 新外观两窗布局（主窗 + 模板窗）：
/// 主窗宽 360、布局高度 448 到 640（默认按工作区一半）；
/// 模板窗宽 260、贴主窗右侧、缝隙固定 4；只跟随等比缩放，不跟随主窗高度。
/// 纯计算，不含 UI。
/// </summary>
public static class PanelLayout
{
    public const double MainWidth = 360;

    public const double MainBaseHeight = 640;

    public const double MainMinHeight = 448;

    public const double TemplateWidth = 260;

    /// <summary>模板窗满 10 条时的自身高度，也是主窗最小高度依据。</summary>
    public const double TemplateMaxHeight = 448;

    public const double Gap = 4;

    public const double MinScale = 0.7;

    public const double MaxScale = 1.15;

    public const double ViewportMargin = 12;

    /// <summary>首次打开的主窗高度：工作区高的一半，夹在 448 与 640 之间。</summary>
    public static double DefaultMainHeight(double workAreaHeight) =>
        throw new NotImplementedException();

    /// <summary>主窗高度上限：工作区底部留 12px，再按缩放换算回布局高度。</summary>
    public static double MaxMainHeight(double workAreaHeight, double mainTop, double scale) =>
        throw new NotImplementedException();

    /// <summary>缩放上限：只计算主窗（模板窗是否展开不影响）。</summary>
    public static double MaxScaleFor(WinRect workArea, WinPoint anchor, double mainHeight) =>
        throw new NotImplementedException();

    /// <summary>把缩放钳回 0.7 到上限之间。</summary>
    public static double ClampScale(double scale, WinRect workArea, WinPoint anchor, double mainHeight) =>
        throw new NotImplementedException();

    /// <summary>两窗几何：模板窗左缘 = 主窗视觉右缘 + 固定 4；模板窗高度不随主窗高度变化。</summary>
    public static PanelFrame Arrange(WinPoint anchor, double scale, double mainHeight, bool templateVisible) =>
        throw new NotImplementedException();

    /// <summary>首次启动位置：工作区右侧偏上；放不下时水平居中。</summary>
    public static WinPoint FirstLaunchAnchor(WinRect workArea, double scale, bool templateVisible) =>
        throw new NotImplementedException();
}

/// <summary>两窗布局结果：主窗与模板窗的几何帧。</summary>
public sealed record PanelFrame(ClusterPanelFrame Main, ClusterPanelFrame Template);
