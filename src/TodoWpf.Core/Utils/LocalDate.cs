using System.Globalization;
using System.Text.RegularExpressions;

namespace TodoWpf.Core.Utils;

/// <summary>
/// 本地日期键（YYYY-MM-DD）：展示/分组/跨天判定一律用本机本地时区（对齐旧版 getFullYear/getMonth/getDate 口径）。
/// 领域代码禁止直接用 UTC 日期做比较。
/// </summary>
public static class LocalDate
{
    private const string KeyPattern = "yyyy-MM-dd";

    private static readonly Regex KeyRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    /// <summary>本地日期键。</summary>
    public static string Key(DateTimeOffset instant) =>
        instant.LocalDateTime.ToString(KeyPattern, CultureInfo.InvariantCulture);

    /// <summary>ISO 时间串 → 本地日期键；无法解析返回空串（与旧版 Invalid Date 同向：不等于今天→按逾期处理）。</summary>
    public static string KeyOfIso(string? iso) =>
        IsoTime.TryParse(iso, out var instant) ? Key(instant) : string.Empty;

    /// <summary>是否为定宽日期键（仅形态校验，不含闰年等语义）。</summary>
    public static bool IsValidKey(string? key) => key is not null && KeyRegex.IsMatch(key);

    /// <summary>日期键 ±N 天（历史翻页用）；非法键抛 <see cref="FormatException"/>。</summary>
    public static string Shift(string key, int days)
    {
        var normalized = key ?? string.Empty;
        if (!KeyRegex.IsMatch(normalized))
        {
            throw new FormatException($"invalid date key: {key}");
        }

        var parts = normalized.Split('-');
        var date = new DateOnly(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture));

        return date.AddDays(days).ToString(KeyPattern, CultureInfo.InvariantCulture);
    }
}
