using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>新外观两窗布局（批次 04）：默认高度公式、缩放只算主窗、模板窗只跟随缩放不跟随高度。</summary>
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
        Assert.Equal(0.7, PanelLayout.MinScale);
        Assert.Equal(1.15, PanelLayout.MaxScale);
    }

    [Fact]
    public void DefaultMainHeight_HalfOfWorkArea_ClampedTo448And640()
    {
        Assert.Equal(448, PanelLayout.DefaultMainHeight(704));
        Assert.Equal(508, PanelLayout.DefaultMainHeight(1016));
        Assert.Equal(640, PanelLayout.DefaultMainHeight(1400));
    }

    [Fact]
    public void MaxMainHeight_StopsAtViewportBottom()
    {
        Assert.Equal(928, PanelLayout.MaxMainHeight(1040, 100, 1.0), 6);
        Assert.Equal(928 / 1.15, PanelLayout.MaxMainHeight(1040, 100, 1.15), 6);
        Assert.Equal(448, PanelLayout.MaxMainHeight(500, 480, 1.0));
    }

    [Fact]
    public void MaxScaleFor_CountsMainWindowOnly()
    {
        var scale = PanelLayout.MaxScaleFor(new WinRect(0, 0, 1000, 2080), new WinPoint(100, 100), 640);
        Assert.Equal(1.15, scale, 6);
    }

    [Fact]
    public void MaxScaleFor_HeightLimitWins_WhenViewportShort()
    {
        var scale = PanelLayout.MaxScaleFor(new WinRect(0, 0, 1920, 700), new WinPoint(100, 100), 640);
        Assert.Equal((700 - 100 - 12) / 640.0, scale, 6);
    }

    [Fact]
    public void ClampScale_EnforcesLowerBound()
    {
        Assert.Equal(0.7, PanelLayout.ClampScale(0.3, WorkArea, new WinPoint(100, 100), 640));
    }

    [Fact]
    public void Arrange_TemplateSitsRightOfMain_GapConstant()
    {
        var frame = PanelLayout.Arrange(new WinPoint(800, 100), 1.0, 640, templateVisible: true);

        Assert.Equal(800, frame.Main.X);
        Assert.Equal(360, frame.Main.Width);
        Assert.Equal(640, frame.Main.Height);
        Assert.Equal(800 + 360 + 4, frame.Template.X);
        Assert.Equal(260, frame.Template.Width);
        Assert.Equal(100, frame.Template.Y);
        Assert.True(frame.Template.Visible);
    }

    [Fact]
    public void Arrange_ChangingMainHeight_DoesNotMoveTemplate()
    {
        var anchor = new WinPoint(800, 100);
        var tall = PanelLayout.Arrange(anchor, 1.0, 700, templateVisible: true);
        var shortMain = PanelLayout.Arrange(anchor, 1.0, 448, templateVisible: true);

        Assert.Equal(shortMain.Template.X, tall.Template.X, 6);
        Assert.Equal(shortMain.Template.Y, tall.Template.Y, 6);
        Assert.Equal(shortMain.Template.Height, tall.Template.Height, 6);
        Assert.NotEqual(shortMain.Main.Height, tall.Main.Height);
    }

    [Fact]
    public void Arrange_ScaleAppliesToBoth_GapConstant()
    {
        var frame = PanelLayout.Arrange(new WinPoint(800, 100), 1.15, 640, templateVisible: true);

        Assert.Equal(360 * 1.15, frame.Main.Width, 6);
        Assert.Equal(260 * 1.15, frame.Template.Width, 6);
        Assert.Equal(800 + 360 * 1.15 + 4, frame.Template.X, 6);
    }

    [Fact]
    public void Arrange_TemplateHidden_StillGivesGeometry()
    {
        var frame = PanelLayout.Arrange(new WinPoint(100, 100), 1.0, 640, templateVisible: false);

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
