namespace TodoWpf.Core.Windows;

/// <summary>
/// 新外观两窗布局（主窗 + 模板窗），纯计算、不含 UI。
///
/// 尺寸规则（用户 2026-11-02 裁定，替换此前的「半屏 + clamp 448~640」）：
/// - 默认布局高 = 当前所在屏工作区高 ÷ 3；
/// - 最大布局高 = 工作区高 × 2/3，且不越过工作区底部留白；
/// - 四边不缩放，只保留右下角等比缩放（UI 层约束，不在此类）。
/// </summary>
public static class PanelLayout
{
    public const double MainWidth = 360;

    public const double MainBaseHeight = 640;

    /// <summary>缩放下限 0.7 对应的高度（640×0.7）；不再用作默认高度的兜底。</summary>
    public const double MainMinHeight = 448;

    public const double TemplateWidth = 260;

    /// <summary>模板窗满 10 条时的自身高度。</summary>
    public const double TemplateMaxHeight = 448;

    public const double Gap = 4;

    /// <summary>绝对最小高度（DIP）：低于它内容无法使用。</summary>
    public const double AbsoluteMinHeight = 192;

    /// <summary>倍数设计封顶；实际上限几乎总是由「2/3 屏高」决定，此值只防极端大屏。</summary>
    public const double MaxScale = 3.0;

    public const double ViewportMargin = 12;

    public const double DefaultHeightRatio = 1.0 / 3.0;

    public const double MaxHeightRatio = 2.0 / 3.0;

    /// <summary>首次打开的主窗视觉高度：工作区高的 1/3。</summary>
    public static double DefaultMainHeight(double workAreaHeight) =>
        workAreaHeight * DefaultHeightRatio;

    /// <summary>主窗视觉高度上限：工作区高的 2/3，且不越过工作区底部留白。</summary>
    public static double MaxMainHeight(double workAreaHeight, double mainTop) =>
        Math.Max(1, Math.Min(workAreaHeight * MaxHeightRatio, workAreaHeight - mainTop - ViewportMargin));

    /// <summary>当前允许的最大倍数：高度按 2/3 屏高，宽度只算主窗（模板窗展开不影响）。</summary>
    public static double MaxScaleFor(WinRect workArea, WinPoint anchor)
    {
        var byHeight = MaxMainHeight(workArea.Height, anchor.Y) / MainBaseHeight;
        var byWidth = (workArea.Right - anchor.X - ViewportMargin) / MainWidth;
        return Math.Max(MinScaleFor(workArea.Height), Math.Min(MaxScale, Math.Min(byHeight, byWidth)));
    }

    /// <summary>最小布局高：屏高 1/4，且不低于 192 DIP。</summary>
    public static double MinMainHeight(double workAreaHeight) =>
        Math.Max(AbsoluteMinHeight, workAreaHeight / 4.0);

    /// <summary>最小倍数：由最小布局高换算。</summary>
    public static double MinScaleFor(double workAreaHeight) =>
        MinMainHeight(workAreaHeight) / MainBaseHeight;

    /// <summary>把缩放钳回「屏高 1/4」到当前屏允许的上限之间。</summary>
    public static double ClampScale(double scale, WinRect workArea, WinPoint anchor) =>
        Math.Clamp(scale, MinScaleFor(workArea.Height), MaxScaleFor(workArea, anchor));

    /// <summary>两窗几何：模板窗左缘 = 主窗视觉右缘 + 固定 4；模板窗高度只跟随缩放。</summary>
    public static PanelFrame Arrange(WinPoint anchor, double scale, bool templateVisible)
    {
        var mainWidth = MainWidth * scale;
        var main = new ClusterPanelFrame(
            new WinRect(anchor.X, anchor.Y, mainWidth, MainBaseHeight * scale),
            true);
        var template = new ClusterPanelFrame(
            new WinRect(anchor.X + mainWidth + Gap, anchor.Y, TemplateWidth * scale, TemplateMaxHeight * scale),
            templateVisible);
        return new PanelFrame(main, template);
    }

    /// <summary>首次启动位置：工作区右侧偏上、不贴边；放不下时水平居中。</summary>
    public static WinPoint FirstLaunchAnchor(WinRect workArea, double scale, bool templateVisible)
    {
        var totalWidth = (MainWidth + (templateVisible ? Gap + TemplateWidth : 0)) * scale;
        var x = workArea.Right - 40 - totalWidth;
        if (x < workArea.X)
        {
            x = workArea.X + (workArea.Width - totalWidth) / 2;
        }

        return new WinPoint(x, workArea.Y + 80);
    }
}

/// <summary>两窗布局结果：主窗与模板窗的几何帧。</summary>
public sealed record PanelFrame(ClusterPanelFrame Main, ClusterPanelFrame Template);
