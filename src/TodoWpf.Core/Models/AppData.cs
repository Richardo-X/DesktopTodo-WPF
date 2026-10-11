namespace TodoWpf.Core.Models;

/// <summary>本地单文件数据（data.json），字段与 Electron 版逐字段兼容。</summary>
public sealed record AppData
{
    /// <summary>数据格式版本（恒为 1）。</summary>
    public int V { get; init; } = 1;

    public List<TodoItem> Todos { get; init; } = new();

    /// <summary>当天完成（跨天结算归档进 Archive 后清空）。</summary>
    public List<TodoItem> Completed { get; init; } = new();

    /// <summary>历史记录：按完成日（本地日期键 YYYY-MM-DD）归档。</summary>
    public Dictionary<string, List<TodoItem>> Archive { get; init; } = new();

    public List<TemplateItem> Templates { get; init; } = new();

    public AppSettings Settings { get; init; } = new();

    public UiState UiState { get; init; } = new();

    /// <summary>深拷贝可变集合（元素本身为不可变 record，浅拷列表即可）。</summary>
    public AppData DeepCopy() => this with
    {
        Todos = [.. Todos],
        Completed = [.. Completed],
        Archive = Archive.ToDictionary(kv => kv.Key, kv => new List<TodoItem>(kv.Value)),
        Templates = [.. Templates],
        UiState = UiState with { ChildScales = new Dictionary<string, double>(UiState.ChildScales) },
    };
}
