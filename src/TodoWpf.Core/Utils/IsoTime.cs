using System.Globalization;

namespace TodoWpf.Core.Utils;

/// <summary>
/// 时间戳口径（存储侧唯一）：ISO 8601 UTC、毫秒固定 3 位、Z 结尾
/// ——与 Electron 版 <c>Date.toISOString()</c> 同形态（如 2026-09-24T17:15:01.832Z）。
/// </summary>
public static class IsoTime
{
    private const string UtcPattern = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string Format(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString(UtcPattern, CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out DateTimeOffset instant)
    {
        instant = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out instant);
    }

    public static DateTimeOffset Parse(string text) =>
        TryParse(text, out var instant)
            ? instant
            : throw new FormatException($"invalid ISO 8601 time: {text}");

    /// <summary>解析为本地时区的 DateTimeOffset（显示侧用：完成时间 HH:mm 等）。</summary>
    public static DateTimeOffset ParseLocal(string text) => Parse(text).ToLocalTime();
}
