namespace TodoWpf.Core.Models;

/// <summary>应用设置（设置页四项中的持久化部分）。</summary>
public sealed record AppSettings
{
    /// <summary>待办数量上限下限。</summary>
    public const int MinMaxItems = 1;

    /// <summary>待办数量上限上限（PRD：1~10 步进，默认 10）。</summary>
    public const int MaxMaxItems = 10;

    /// <summary>待办数量上限 1~10，默认 10；不可低于当前待办数量。</summary>
    public int MaxItems { get; init; } = MaxMaxItems;

    /// <summary>12 小时制开关（默认 24 小时制）。</summary>
    public bool Hour12 { get; init; }

    /// <summary>开机自启（默认关）。</summary>
    public bool AutoStart { get; init; }

    /// <summary>已完成/历史排序："asc"（默认正序）或 "desc"，两页共用。</summary>
    public string DoneSort { get; init; } = "asc";
}
