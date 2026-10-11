namespace TodoWpf.Core.Models;

/// <summary>模板（常用事项单条文字）。</summary>
public sealed record TemplateItem
{
    public string Id { get; init; } = "";

    public string Content { get; init; } = "";

    /// <summary>创建时间：ISO 8601 UTC。</summary>
    public string CreatedAt { get; init; } = "";
}
