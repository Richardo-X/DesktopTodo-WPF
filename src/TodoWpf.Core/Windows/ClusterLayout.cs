namespace TodoWpf.Core.Windows;

/// <summary>
/// 三窗簇布局几何（纯计算）：
/// 基准 470×640（主）+ 310×640（侧）×2、缝隙固定 4px 不随缩放；
/// 等比缩放上 limit=半屏高与工作区宽的较小者，下限 0.7（矮屏半屏低于下限时上限优先，内容仍完整可见）。
/// </summary>
public static class ClusterLayout
{
    public const double MainWidth = 360;

    public const double MainHeight = 640;

    public const double SideWidth = 260;

    public const double SideHeight = 640;

    public const double Gap = 4;

    /// <summary>缩放下限（内容完整可见口径）。</summary>
    public const double MinScale = 0.7;

    /// <summary>可见面板总宽（含缝隙）。</summary>
    public static double TotalWidth(bool quickVisible, bool doneVisible) =>
        MainWidth
        + (quickVisible ? Gap + SideWidth : 0)
        + (doneVisible ? Gap + SideWidth : 0);

    /// <summary>当前工作区允许的最大缩放。</summary>
    public static double MaxScale(WinRect workArea, bool quickVisible, bool doneVisible)
    {
        var heightLimit = workArea.Height / 2 / MainHeight;
        var widthLimit = workArea.Width / TotalWidth(quickVisible, doneVisible);
        return Math.Min(heightLimit, widthLimit);
    }

    /// <summary>把任意缩放值钳回合法区间；矮屏（上限低于下限）时取上限（内容完整可见优先于下限数值）。</summary>
    public static double ClampScale(double scale, WinRect workArea, bool quickVisible, bool doneVisible)
    {
        var upper = MaxScale(workArea, quickVisible, doneVisible);
        if (upper < MinScale)
        {
            return upper;
        }

        return Math.Clamp(scale, MinScale, upper);
    }

    /// <summary>
    /// 布局三窗：锚点=待办主窗左上角；快捷窗右缘钉在主窗左缘减缝隙、已完成窗左缘钉在主窗右缘加缝隙、顶边对齐。
    /// 隐藏面板的几何给出"若显示应在"的值（Visible=false）。
    /// </summary>
    public static ClusterFrame Arrange(WinRect workArea, WinPoint anchor, double scale, bool quickVisible, bool doneVisible)
    {
        var mainW = MainWidth * scale;
        var mainH = MainHeight * scale;
        var sideW = SideWidth * scale;
        var sideH = SideHeight * scale;

        var main = new ClusterPanelFrame(new WinRect(anchor.X, anchor.Y, mainW, mainH), true);
        var quick = new ClusterPanelFrame(new WinRect(anchor.X + mainW + Gap, anchor.Y, sideW, sideH), quickVisible);
        var done = new ClusterPanelFrame(new WinRect(anchor.X + mainW + Gap + sideW + Gap, anchor.Y, sideW, sideH), doneVisible);

        return new ClusterFrame(main, quick, done);
    }

    /// <summary>首启位置：屏幕右侧偏上、不贴边（可见总宽超出工作区时改为水平居中）。</summary>
    public static WinPoint FirstLaunchAnchor(WinRect workArea, double scale, bool quickVisible, bool doneVisible)
    {
        var totalW = TotalWidth(quickVisible, doneVisible) * scale
                     + (quickVisible ? 0 : 0); // 缝隙已计入 TotalWidth
        // TotalWidth 两侧只算了两条缝隙：以可见侧数量校准（quick 缝隙只在可见时计入）
        var x = workArea.Right - 40 - totalW;
        if (x < workArea.X)
        {
            x = workArea.X + (workArea.Width - totalW) / 2;
        }

        return new WinPoint(x, workArea.Y + 80);
    }
}
