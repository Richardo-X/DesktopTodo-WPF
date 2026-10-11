namespace TodoWpf.Core.Windows;

/// <summary>多屏钳制：改分辨率/拔显示器后把窗口拉回工作区可见范围（超大窗口钉在工作区原点）。</summary>
public static class ScreenClamp
{
    public static WinRect ClampToWorkArea(WinRect rect, WinRect workArea)
    {
        var x = rect.Width >= workArea.Width ? workArea.X : Math.Clamp(rect.X, workArea.X, workArea.Right - rect.Width);
        var y = rect.Height >= workArea.Height ? workArea.Y : Math.Clamp(rect.Y, workArea.Y, workArea.Bottom - rect.Height);

        return rect with { X = x, Y = y };
    }
}
