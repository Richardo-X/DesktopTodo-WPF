namespace TodoWpf.Core.Windows;

/// <summary>窗口矩形（DIP 逻辑坐标）。</summary>
public readonly record struct WinRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>窗口坐标点（DIP 逻辑坐标）。</summary>
public readonly record struct WinPoint(double X, double Y);

/// <summary>簇成员布局帧：隐藏面板仍给出"若显示应在"的几何，由 Visible 决定是否出场。</summary>
public sealed record ClusterPanelFrame(WinRect Rect, bool Visible)
{
    public double X => Rect.X;

    public double Y => Rect.Y;

    public double Width => Rect.Width;

    public double Height => Rect.Height;

    public double Right => Rect.Right;
}

/// <summary>一次簇布局计算结果（锚点=待办主窗左上角）。</summary>
public sealed record ClusterFrame(ClusterPanelFrame Main, ClusterPanelFrame Quick, ClusterPanelFrame Done);
