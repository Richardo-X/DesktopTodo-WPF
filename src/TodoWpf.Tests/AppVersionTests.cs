using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>SemVer 文本比较（C4：更新可用性 / 降级检测的纯函数部分）。</summary>
public class AppVersionTests
{
    [Fact]
    public void Equal_Versions_ReturnsZero()
    {
        Assert.Equal(0, AppVersion.Compare("1.0.0", "1.0.0"));
    }

    [Fact]
    public void Patch_Difference_IsDetected()
    {
        Assert.Equal(1, AppVersion.Compare("1.0.1", "1.0.0"));
        Assert.Equal(-1, AppVersion.Compare("1.0.0", "1.0.1"));
    }

    [Fact]
    public void Minor_Difference_IsDetected()
    {
        Assert.Equal(1, AppVersion.Compare("1.1.0", "1.0.9"));
    }

    [Fact]
    public void Numeric_Compare_NotLexicographic()
    {
        Assert.Equal(1, AppVersion.Compare("1.10.0", "1.9.0"));
    }

    [Fact]
    public void Missing_Segments_Count_As_Zero()
    {
        Assert.Equal(0, AppVersion.Compare("1.0", "1.0.0"));
        Assert.Equal(1, AppVersion.Compare("1.0.1", "1.0"));
    }

    [Fact]
    public void Null_And_Invalid_Count_As_Zero()
    {
        Assert.Equal(-1, AppVersion.Compare(null, "1.0.0"));
        Assert.Equal(1, AppVersion.Compare("1.0.0", null));
        Assert.Equal(0, AppVersion.Compare(null, null));
        Assert.Equal(-1, AppVersion.Compare("abc", "1.0.0"));
        Assert.Equal(-1, AppVersion.Compare("1.0.0", "abc"));
    }

    [Fact]
    public void Trailing_Text_Segment_Parses_Leading_Digits()
    {
        Assert.Equal(0, AppVersion.Compare("1.2.3-beta", "1.2.3"));
    }
}
