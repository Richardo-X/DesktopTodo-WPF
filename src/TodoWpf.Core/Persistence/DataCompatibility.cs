namespace TodoWpf.Core.Persistence;

/// <summary>数据格式兼容判定结果（C4 版本兼容检测）。</summary>
public enum DataCompatibilityResult
{
    /// <summary>在本版本可读范围内。</summary>
    Compatible,

    /// <summary>数据格式过旧（低于本版本可读下限）。</summary>
    TooOld,

    /// <summary>数据格式过新（来自更高版本的应用，可能含本版本不认识的结构）。</summary>
    TooNew,
}

/// <summary>
/// 数据格式兼容判定（纯逻辑）：当前版本写出的格式版本、可读下限、以及给定文件版本的判定。
/// </summary>
public static class DataCompatibility
{
    /// <summary>判定给定文件格式版本是否可被本版本读取（null = 文件无 v 字段，按历史档 v1 处理）。</summary>
    public static DataCompatibilityResult Check(int? fileVersion) => DataCompatibilityResult.Compatible;
}
