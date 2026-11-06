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


    // ---------- 下限口径（2026-11-06 查漏补测：MinMainHeight/MinScaleFor 此前无直接用例） ----------

    [Fact]
    public void MinMainHeight_屏高四分之一与绝对下限取大()
    {
        // 屏高 1/4 = 360 > 192 → 取 360
        Assert.Equal(1440 / 4.0, PanelLayout.MinMainHeight(1440), 6);
        // 屏高 1/4 = 150 < 192 → 取绝对下限 192
        Assert.Equal(192, PanelLayout.MinMainHeight(600), 6);
        // 恰好相等时取 192（Math.Max 同值时返回该值）
        Assert.Equal(192, PanelLayout.MinMainHeight(768), 6);
    }

    [Fact]
    public void MinScaleFor_由最小布局高换算()
    {
        Assert.Equal(PanelLayout.MinMainHeight(1440) / PanelLayout.MainBaseHeight, PanelLayout.MinScaleFor(1440), 6);
        Assert.Equal(192 / PanelLayout.MainBaseHeight, PanelLayout.MinScaleFor(600), 6);
    }

    [Fact]
    public void ClampScale_下界就是MinScaleFor_上界就是MaxScaleFor()
    {
        var anchor = new WinPoint(0, 0);
        var below = PanelLayout.ClampScale(0.01, WorkArea, anchor);
        var above = PanelLayout.ClampScale(99.0, WorkArea, anchor);

        Assert.Equal(PanelLayout.MinScaleFor(WorkArea.Height), below, 6);
        Assert.Equal(PanelLayout.MaxScaleFor(WorkArea, anchor), above, 6);
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

    // ---------- 拖动钳制（修复轮，ele clampAnchorForEdges 口径；用户 2026-11-04 裁定 D2） ----------

    [Fact]
    public void DragClamp_KeepsMainWindowVisibleOnAllSides()
    {
        var wa = new WinRect(0, 0, 1920, 1040);
        const double mainW = 360;

        // 左上越界：主窗左缘最多到 wa.x + 90 - 主窗宽（保证右缘仍露出 90）；顶不越工作区顶
        var tl = PanelLayout.ClampAnchorForDrag(new WinPoint(-500, -300), mainW, wa);
        Assert.Equal(90 - mainW, tl.X, 6);
        Assert.Equal(0, tl.Y, 6);

        // 右下越界：主窗左缘最多到 wa.Right - 90；顶最多到 wa.Bottom - 50
        var br = PanelLayout.ClampAnchorForDrag(new WinPoint(5000, 5000), mainW, wa);
        Assert.Equal(1920 - 90, br.X, 6);
        Assert.Equal(1040 - 50, br.Y, 6);
    }

    [Fact]
    public void DragClamp_InsideBounds_Unchanged()
    {
        var wa = new WinRect(0, 0, 1920, 1040);
        var p = new WinPoint(800, 300);
        var clamped = PanelLayout.ClampAnchorForDrag(p, 360, wa);
        Assert.Equal(p.X, clamped.X, 6);
        Assert.Equal(p.Y, clamped.Y, 6);
    }

    [Fact]
    public void DragClamp_UsesGivenWorkAreaOffset()
    {
        // 副屏（工作区有偏移）：钳制相对该屏计算
        var wa = new WinRect(1920, 0, 2560, 1400);
        var tl = PanelLayout.ClampAnchorForDrag(new WinPoint(0, -100), 360, wa);
        Assert.Equal(1920 + 90 - 360, tl.X, 6);
        Assert.Equal(0, tl.Y, 6);
    }
}
