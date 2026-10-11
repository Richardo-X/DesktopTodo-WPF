namespace TodoWpf.Core.Models;

/// <summary>内容校验失败原因（UI 层映射为需求原文提示）。</summary>
public enum ContentError
{
    /// <summary>trim 后为空。</summary>
    EmptyContent,

    /// <summary>超过 300 字。</summary>
    TooLong,
}

public readonly record struct ContentValidation(bool Ok, ContentError? Reason)
{
    public static ContentValidation Pass => new(true, null);

    public static ContentValidation Fail(ContentError reason) => new(false, reason);
}

/// <summary>领域错误：携带机器可读原因。</summary>
public sealed class DomainException : Exception
{
    public DomainException(ContentError reason)
        : base($"domain error: {reason}")
    {
        Reason = reason;
    }

    public ContentError Reason { get; }
}

/// <summary>内容规则（需求文档 §2.4）：trim 后非空且 ≤300 字；计数口径与旧版 JS .length 一致（UTF-16 码元）。</summary>
public static class ContentRules
{
    public const int MaxLength = 300;

    public static ContentValidation Validate(string? raw)
    {
        var content = (raw ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            return ContentValidation.Fail(ContentError.EmptyContent);
        }

        if (content.Length > MaxLength)
        {
            return ContentValidation.Fail(ContentError.TooLong);
        }

        return ContentValidation.Pass;
    }

    /// <summary>校验通过返回 trim 后的内容，否则抛 <see cref="DomainException"/>。</summary>
    public static string RequireValid(string? raw)
    {
        var result = Validate(raw);
        if (!result.Ok)
        {
            throw new DomainException(result.Reason!.Value);
        }

        return (raw ?? string.Empty).Trim();
    }
}
