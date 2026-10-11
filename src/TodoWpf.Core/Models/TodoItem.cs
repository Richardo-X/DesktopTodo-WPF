namespace TodoWpf.Core.Models;

/// <summary>待办 / 已完成 / 历史记录共用结构：有 CompletedAt = 已完成（不设独立"完成状态"字段）。</summary>
public sealed record TodoItem
{
    public string Id { get; init; } = "";

    public string Content { get; init; } = "";

    /// <summary>创建时间：ISO 8601 UTC（毫秒 3 位 + Z，与 Electron 版 toISOString 同形态）；null 例外不存在。</summary>
    public string CreatedAt { get; init; } = "";

    /// <summary>完成时间：ISO 8601 UTC；null = 待办。</summary>
    public string? CompletedAt { get; init; }

    /// <summary>逾期标记：跨天结算置位；退回待办时按今日重算（口径见 OverdueService）。</summary>
    public bool Overdue { get; init; }
}
