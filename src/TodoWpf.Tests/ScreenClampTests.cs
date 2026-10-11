using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>多屏钳制（批次 03）：改分辨率/拔显示器后组件回到主屏可见范围。</summary>
public class ScreenClampTests
{
    private static readonly WinRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void InsideArea_Unchanged()
    {
        var rect = new WinRect(500, 200, 470, 640);

        var clamped = ScreenClamp.ClampToWorkArea(rect, WorkArea);

        Assert.Equal(rect, clamped);
    }

    [Fact]
    public void OffRight_PulledBack()
    {
        var rect = new WinRect(1900, 200, 470, 640);

        var clamped = ScreenClamp.ClampToWorkArea(rect, WorkArea);

        Assert.Equal(1920 - 470, clamped.X);
        Assert.Equal(200, clamped.Y);
    }

    [Fact]
    public void AboveTop_PinnedToTop()
    {
        var rect = new WinRect(500, -200, 470, 640);

        var clamped = ScreenClamp.ClampToWorkArea(rect, WorkArea);

        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    public void OversizedWindow_PinnedToWorkAreaOrigin()
    {
        var rect = new WinRect(100, 100, 2400, 1300);

        var clamped = ScreenClamp.ClampToWorkArea(rect, WorkArea);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(2400, clamped.Width);
    }
}
