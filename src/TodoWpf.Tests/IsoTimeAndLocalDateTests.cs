using TodoWpf.Core.Utils;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>时间口径：存储 ISO 8601 UTC（毫秒 3 位 + Z）；日期键=本机本地时区 YYYY-MM-DD。</summary>
public class IsoTimeAndLocalDateTests
{
    [Fact]
    public void Format_ProducesJsToIsoStringShape()
    {
        var instant = TestTime.Local(2026, 1, 2, 3, 4, 5, 678);

        var text = IsoTime.Format(instant);

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", text);
        Assert.Equal(instant.UtcDateTime, IsoTime.Parse(text).UtcDateTime);
    }

    [Fact]
    public void Format_KeepsMillisecondsAtThreeDigits()
    {
        Assert.EndsWith(".000Z", IsoTime.Format(TestTime.Local(2026, 1, 1, 0, 0, 0, 0)));
        Assert.EndsWith(".040Z", IsoTime.Format(TestTime.Local(2026, 1, 1, 0, 0, 0, 40)));
        Assert.EndsWith(".400Z", IsoTime.Format(TestTime.Local(2026, 1, 1, 0, 0, 0, 400)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-time")]
    [InlineData("2026-13-45T99:99:99.000Z")]
    public void Parse_RejectsGarbage(string raw)
    {
        Assert.False(IsoTime.TryParse(raw, out _));
    }

    [Fact]
    public void Key_IsLocalAndZeroPadded()
    {
        Assert.Equal("2026-01-02", LocalDate.Key(TestTime.Local(2026, 1, 2, 9)));
    }

    [Fact]
    public void KeyOfIso_UsesLocalCalendarDay()
    {
        var lateNight = TestTime.Local(2026, 3, 4, 23, 50);

        Assert.Equal("2026-03-04", LocalDate.KeyOfIso(IsoTime.Format(lateNight)));
    }

    [Fact]
    public void KeyOfIso_Unparsable_ReturnsEmpty()
    {
        Assert.Equal("", LocalDate.KeyOfIso("garbage"));
        Assert.Equal("", LocalDate.KeyOfIso(null));
    }

    [Theory]
    [InlineData("2026-12-31", 1, "2027-01-01")]
    [InlineData("2026-01-01", -1, "2025-12-31")]
    [InlineData("2026-03-01", -1, "2026-02-28")]
    [InlineData("2024-03-01", -1, "2024-02-29")]
    [InlineData("2026-10-11", 0, "2026-10-11")]
    public void Shift_CrossesMonthAndYear(string key, int days, string expected)
    {
        Assert.Equal(expected, LocalDate.Shift(key, days));
    }

    [Fact]
    public void Shift_ThrowsOnInvalidKey()
    {
        Assert.Throws<FormatException>(() => LocalDate.Shift("2026-1-1", 1));
        Assert.Throws<FormatException>(() => LocalDate.Shift("garbage", 1));
    }

    [Theory]
    [InlineData("2026-10-11", true)]
    [InlineData("2026-1-1", false)]
    [InlineData("2026/10/11", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidKey_ChecksFixedWidthShape(string? key, bool expected)
    {
        Assert.Equal(expected, LocalDate.IsValidKey(key));
    }
}
