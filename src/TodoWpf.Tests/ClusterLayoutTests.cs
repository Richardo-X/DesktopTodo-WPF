using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>三窗簇布局几何（批次 03）：等比缩放、缝隙常量、半屏/全宽上限、首启位置。</summary>
public class ClusterLayoutTests
{
    private static readonly WinRect WorkArea = new(0, 0, 1920, 1040); // 1080p 去任务栏

    [Fact]
    public void BaseDimensions_MatchPrototype()
    {
        Assert.Equal(470, ClusterLayout.MainWidth);
        Assert.Equal(640, ClusterLayout.MainHeight);
        Assert.Equal(310, ClusterLayout.SideWidth);
        Assert.Equal(640, ClusterLayout.SideHeight);
        Assert.Equal(4, ClusterLayout.Gap);
        Assert.Equal(0.7, ClusterLayout.MinScale);
    }

    [Fact]
    public void Arrange_BothSidesVisible_ThreeAlignedWindows()
    {
        var anchor = new WinPoint(720, 100);

        var frame = ClusterLayout.Arrange(WorkArea, anchor, 1.0, quickVisible: true, doneVisible: true);

        // 快捷窗：右缘 = 主窗左缘 - 缝隙；顶边对齐
        Assert.Equal(720 - 4 - 310, frame.Quick.X);
        Assert.Equal(720 - 4, frame.Quick.Right);
        Assert.Equal(100, frame.Quick.Y);
        Assert.Equal(310, frame.Quick.Width);
        // 主窗：锚点即主窗左上角
        Assert.Equal(720, frame.Main.X);
        Assert.Equal(100, frame.Main.Y);
        Assert.Equal(470, frame.Main.Width);
        // 已完成窗：左缘 = 主窗右缘 + 缝隙
        Assert.Equal(720 + 470 + 4, frame.Done.X);
        Assert.Equal(640, frame.Done.Height);
    }

    [Fact]
    public void Arrange_HiddenSides_FollowVisibility()
    {
        var anchor = new WinPoint(100, 100);

        var onlyMain = ClusterLayout.Arrange(WorkArea, anchor, 1.0, quickVisible: false, doneVisible: false);
        // 隐藏面板仍给出"若显示应在"的几何（快捷窗右缘=锚点-4），由 Visible 决定是否出场
        Assert.Equal(100 - 4 - 310, onlyMain.Quick.X);
        Assert.False(onlyMain.Quick.Visible);
        Assert.False(onlyMain.Done.Visible);
        Assert.True(onlyMain.Main.Visible);

        var withDone = ClusterLayout.Arrange(WorkArea, anchor, 1.0, quickVisible: false, doneVisible: true);
        Assert.False(withDone.Quick.Visible);
        Assert.True(withDone.Done.Visible);
        Assert.Equal(anchor.X + 470 + 4, withDone.Done.X);
    }

    [Fact]
    public void Arrange_AppliesUniformScale_GapConstant()
    {
        var anchor = new WinPoint(800, 100);
        var frame = ClusterLayout.Arrange(WorkArea, anchor, 1.25, quickVisible: true, doneVisible: true);

        Assert.Equal(470 * 1.25, frame.Main.Width);
        Assert.Equal(640 * 1.25, frame.Main.Height);
        Assert.Equal(310 * 1.25, frame.Quick.Width);
        // 缝隙固定 4px（不随缩放）
        Assert.Equal(anchor.X - 4, frame.Quick.Right);
        Assert.Equal(anchor.X + 470 * 1.25 + 4, frame.Done.X);
    }

    [Fact]
    public void ClampScale_RespectsHalfScreenHeightLimit()
    {
        // 1080p：半屏高 520 → 520/640 ≈ 0.8125 为高度上限
        var clamped = ClusterLayout.ClampScale(1.5, WorkArea, true, true);
        Assert.Equal(520d / 640d, clamped, 10);
    }

    [Fact]
    public void ClampScale_RespectsWorkAreaWidthLimit()
    {
        // 高屏（高度限制不生效）：三窗全开总宽（含 2 条缝隙）= 1098 → s ≤ 1000/1098
        var clamped = ClusterLayout.ClampScale(1.2, new WinRect(0, 0, 1000, 2080), true, true);
        Assert.Equal(1000 / 1098.0, clamped, 10);

        // 1080p 全开：高度限制（0.8125）比宽度限制（0.9107）更紧
        var on1080p = ClusterLayout.ClampScale(1.2, new WinRect(0, 0, 1920, 1040), true, true);
        Assert.Equal(520d / 640d, on1080p, 10);

        // 只开主窗时宽度上限更宽松（1.2 在允许区间内，原样返回）
        var mainOnly = ClusterLayout.ClampScale(1.2, new WinRect(0, 0, 1000, 2080), false, false);
        Assert.Equal(1.2, mainOnly, 10);
    }

    [Fact]
    public void ClampScale_EnforcesLowerBound()
    {
        var clamped = ClusterLayout.ClampScale(0.3, WorkArea, true, true);
        Assert.Equal(0.7, clamped);
    }

    [Fact]
    public void ClampScale_ShortScreen_PrefersHeightLimitOverLowerBound()
    {
        // 矮屏（半屏 < 0.7）：上限优先，取更小值（内容仍完整可见，只是更小）
        var clamped = ClusterLayout.ClampScale(1.0, new WinRect(0, 0, 1920, 800), true, true);
        Assert.Equal(400d / 640d, clamped, 10);
    }

    [Fact]
    public void FirstLaunchAnchor_RightSideUpperOffEdge()
    {
        var anchor = ClusterLayout.FirstLaunchAnchor(WorkArea, 1.0, true, true);

        var totalWidth = (470 + 310 + 310) + 2 * ClusterLayout.Gap;
        Assert.Equal(WorkArea.Right - 40 - totalWidth, anchor.X);
        Assert.Equal(WorkArea.Y + 80, anchor.Y);
        // 不贴边：与上边缘留出距离
        Assert.True(anchor.Y > WorkArea.Y);
    }

    [Fact]
    public void FirstLaunchAnchor_NarrowScreen_CentersHorizontally()
    {
        var anchor = ClusterLayout.FirstLaunchAnchor(new WinRect(0, 0, 1000, 1040), 1.0, true, true);

        var totalWidth = (470 + 310 + 310) + 2 * ClusterLayout.Gap;
        Assert.Equal((1000 - totalWidth) / 2.0, anchor.X, 10);
    }
}
