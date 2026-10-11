using TodoWpf.Core.Models;

namespace TodoWpf.Tests;

/// <summary>内容校验（PRD §5.1：trim 后非空且 ≤300 字；口径与旧版 JS .length 一致）。</summary>
public class ContentRulesTests
{
    [Fact]
    public void Validate_TrimsBeforeJudging()
    {
        Assert.True(ContentRules.Validate("  abc  ").Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_RejectsBlank(string raw)
    {
        var result = ContentRules.Validate(raw);

        Assert.False(result.Ok);
        Assert.Equal(ContentError.EmptyContent, result.Reason);
    }

    [Fact]
    public void Validate_AcceptsExactly300()
    {
        Assert.True(ContentRules.Validate(new string('字', 300)).Ok);
    }

    [Fact]
    public void Validate_Rejects301()
    {
        var result = ContentRules.Validate(new string('字', 301));

        Assert.False(result.Ok);
        Assert.Equal(ContentError.TooLong, result.Reason);
    }

    [Fact]
    public void Validate_CountsUtf16UnitsLikeJavaScript()
    {
        // 😀 是代理对：JS .length 与 C# string.Length 都算 2 → 150 个恰好 300 字通过
        Assert.True(ContentRules.Validate(string.Concat(Enumerable.Repeat("😀", 150))).Ok);
        Assert.False(ContentRules.Validate(string.Concat(Enumerable.Repeat("😀", 151))).Ok);
    }
}
