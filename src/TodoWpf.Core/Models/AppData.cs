namespace TodoWpf.Core.Models;

/// <summary>本地单文件数据（data.json），字段与 Electron 版逐字段兼容。</summary>
public sealed record AppData
{
    /// <summary>
    /// 本版本写出的数据格式版本（C4 兼容检测的基准）。
    /// **不兼容变更时必须提升此值**——守卫测试 `DataShapeGuardTests` 会拦住未登记的格式变更。
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>本版本能读的最旧数据格式版本（低于它的旧档视为不兼容）。</summary>
    public const int MinReadableSchemaVersion = 1;

    /// <summary>数据格式版本（写出时恒为本版本声明的 <see cref="CurrentSchemaVersion"/>）。</summary>
    public int V { get; init; } = CurrentSchemaVersion;

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
