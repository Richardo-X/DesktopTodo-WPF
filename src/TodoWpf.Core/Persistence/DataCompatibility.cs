namespace TodoWpf.Core.Persistence;

using TodoWpf.Core.Models;

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
/// 数据格式兼容判定（纯逻辑）：给定文件里的数据格式版本，判断本版本能否读取。
/// 基准常量在 <see cref="AppData"/>（CurrentSchemaVersion / MinReadableSchemaVersion）。
/// </summary>
public static class DataCompatibility
{
    /// <summary>
    /// 判定给定文件格式版本是否可被本版本读取。
    /// null = 文件无 v 字段（Electron 兼容历史档）→ 按 v1 处理（可读）。
    /// </summary>
    public static DataCompatibilityResult Check(int? fileVersion)
    {
        var version = fileVersion ?? AppData.MinReadableSchemaVersion;
        if (version < AppData.MinReadableSchemaVersion)
        {
            return DataCompatibilityResult.TooOld;
        }

        return version > AppData.CurrentSchemaVersion
            ? DataCompatibilityResult.TooNew
            : DataCompatibilityResult.Compatible;
    }
}
