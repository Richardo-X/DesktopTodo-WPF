using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>新外观两窗布局（批次 04）：默认高度=工作区 1/3、上限=2/3、缩放只算主窗。</summary>
public class PanelLayoutTests
{
    private static readonly WinRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void BaseDimensions_MatchPrototype()
    {
        Assert.Equal(360, PanelLayout.MainWidth);
        Assert.Equal(640, PanelLayout.MainBaseHeight);
        Assert.Equal(448, PanelLayout.MainMinHeight);
        Assert.Equal(260, PanelLayout.TemplateWidth);
        Assert.Equal(448, PanelLayout.TemplateMaxHeight);
        Assert.Equal(4, PanelLayout.Gap);
        Assert.Equal(192, PanelLayout.AbsoluteMinHeight);
        Assert.Equal(3.0, PanelLayout.MaxScale);
    }

    [Fact]
    public void DefaultMainHeight_IsThirdOfWorkArea()
    {
        Assert.Equal(1040 / 3.0, PanelLayout.DefaultMainHeight(1040), 6);
        Assert.Equal(704 / 3.0, PanelLayout.DefaultMainHeight(704), 6);
        Assert.Equal(1400 / 3.0, PanelLayout.DefaultMainHeight(1400), 6);
    }

    [Fact]
    public void MaxMainHeight_IsTwoThirdsOfWorkArea()
    {
        Assert.Equal(1040 * 2 / 3.0, PanelLayout.MaxMainHeight(1040, 80), 6);
        Assert.Equal(1400 * 2 / 3.0, PanelLayout.MaxMainHeight(1400, 80), 6);
    }

    [Fact]
    public void MaxMainHeight_StopsAtViewportBottom()
    {
        // 顶边很靠下时，可用空间小于 2/3 屏高，取可用空间
        Assert.Equal(1040 - 900 - 12, PanelLayout.MaxMainHeight(1040, 900), 6);
    }

    [Fact]
    public void MaxScaleFor_CountsMainWindowOnly()
    {
        // 宽度足够时结果取设计封顶 3.0；若把模板窗计入宽度限制，结果会小于 3.0（只算主窗才成立）
        var scale = PanelLayout.MaxScaleFor(new WinRect(0, 0, 1920, 3000), new WinPoint(100, 100));
        Assert.Equal(3.0, scale, 6);
    }

    [Fact]
    public void MaxScaleFor_TwoThirdsHeightWins()
    {
        var scale = PanelLayout.MaxScaleFor(new WinRect(0, 0, 1920, 1040), new WinPoint(100, 80));
        Assert.Equal((1040 * 2 / 3.0) / 640.0, scale, 6);
    }

    [Fact]
    public void ClampScale_EnforcesLowerBound()
    {
        // 1040 的 1/4 = 260 → 260/640 = 0.40625
        Assert.Equal(0.40625, PanelLayout.ClampScale(0.1, WorkArea, new WinPoint(100, 100)), 6);
    }

    [Fact]
    public void DefaultScale_On1080p_IsAbout054()
    {
        var height = PanelLayout.DefaultMainHeight(1040);
        Assert.Equal(0.5417, height / 640.0, 3);
    }

    [Fact]
    public void Arrange_TemplateSitsRightOfMain_GapConstant()
    {
        var frame = PanelLayout.Arrange(new WinPoint(800, 100), 1.0, templateVisible: true);

        Assert.Equal(800, frame.Main.X);
        Assert.Equal(360, frame.Main.Width);
        Assert.Equal(640, frame.Main.Height);
        Assert.Equal(800 + 360 + 4, frame.Template.X);
        Assert.Equal(260, frame.Template.Width);
        Assert.Equal(100, frame.Template.Y);
        Assert.True(frame.Template.Visible);
    }

    [Fact]
    public void Arrange_ScaleAppliesToBoth_GapConstant()
    {
        var frame = PanelLayout.Arrange(new WinPoint(800, 100), 1.15, templateVisible: true);

        Assert.Equal(360 * 1.15, frame.Main.Width, 6);
        Assert.Equal(260 * 1.15, frame.Template.Width, 6);
        Assert.Equal(800 + 360 * 1.15 + 4, frame.Template.X, 6);
    }

    [Fact]
    public void Arrange_TemplateHidden_StillGivesGeometry()
    {
        var frame = PanelLayout.Arrange(new WinPoint(100, 100), 1.0, templateVisible: false);

        Assert.False(frame.Template.Visible);
        Assert.Equal(100 + 360 + 4, frame.Template.X);
    }

    [Fact]
    public void FirstLaunchAnchor_RightSide_CentersOnNarrowScreen()
    {
        var wide = PanelLayout.FirstLaunchAnchor(new WinRect(0, 0, 1920, 1040), 1.0, templateVisible: true);
        Assert.Equal(1920 - 40 - (360 + 4 + 260), wide.X, 6);
        Assert.Equal(80, wide.Y);

        var narrow = PanelLayout.FirstLaunchAnchor(new WinRect(0, 0, 500, 1040), 1.0, templateVisible: true);
        Assert.Equal((500 - (360 + 4 + 260)) / 2.0, narrow.X, 6);
    }
}
