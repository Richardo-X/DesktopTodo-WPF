using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>物理像素 ↔ DIP 折算（恢复跨屏正确批次的纯函数部分；存档锚点改存物理像素）。</summary>
public class DpiSpaceTests
{
    [Fact]
    public void RawToDips_DividesByScale()
    {
        var (x, y) = DpiSpace.RawToDips(3106.0, -1255.0, 1.75);

        Assert.Equal(1774.857, x, 3);
        Assert.Equal(-717.143, y, 3);
    }

    [Fact]
    public void DipsToRaw_MultipliesByScale()
    {
        var (x, y) = DpiSpace.DipsToRaw(1774.857, -717.143, 1.75);

        Assert.Equal(3106.0, x, 3);
        Assert.Equal(-1255.0, y, 3);
    }

    [Fact]
    public void RoundTrip_SameScreen_IsIdentity()
    {
        var raw = DpiSpace.DipsToRaw(1234.5, -678.9, 1.75);

        var dips = DpiSpace.RawToDips(raw.X, raw.Y, 1.75);

        Assert.Equal(1234.5, dips.X, 6);
        Assert.Equal(-678.9, dips.Y, 6);
    }

    [Fact]
    public void Scale100_IsIdentity()
    {
        var (x, y) = DpiSpace.RawToDips(1774.57, 0.0, 1.0);

        Assert.Equal(1774.57, x, 6);
        Assert.Equal(0.0, y, 6);
    }
}
