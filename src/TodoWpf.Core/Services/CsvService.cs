using System.Text;
using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>
/// 历史 CSV 导出/导入（纯函数可单测；需求 §2.7/§2.8 + 原型口径）：
/// 五列 = 完成日期 / 完成时间 / 事项内容 / 创建时间 / 完成时间（后两列 YYYY-MM-DD HH:mm）；
/// UTF-8 带 BOM、CRLF；字段含逗号/引号/换行时加引号转义。
/// 导入只接受本软件导出的表头；同属性去重（内容 + 完成时间戳）；落位按完成日（今天→已完成，更早→归档）。
/// </summary>
public static class CsvService
{
    public const string ModeCover = "cover";
    public const string ModeMerge = "merge";

    private static readonly string[] Header = ["完成日期", "完成时间", "事项内容", "创建时间", "完成时间"];

    /// <summary>导出 [from, to] 闭区间（本地日期键）内的已完成与归档项；无记录返回 null。</summary>
    public static string? Export(AppData data, string from, string to)
    {
        if (!LocalDate.IsValidKey(from) || !LocalDate.IsValidKey(to) || string.CompareOrdinal(from, to) > 0)
        {
            return null;
        }

        var rows = new List<string[]> { Header };
        var keys = data.Archive.Keys
            .Where(k => string.CompareOrdinal(k, from) >= 0 && string.CompareOrdinal(k, to) <= 0)
            .OrderBy(k => k, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            foreach (var item in data.Archive[key].OrderBy(t => t.CompletedAt, StringComparer.Ordinal))
            {
                rows.Add(RowOf(key, item));
            }
        }

        if (rows.Count == 1)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // BOM：Excel 直接打开不乱码（原型口径）
        foreach (var row in rows)
        {
            sb.Append(string.Join(',', row.Select(EscapeField)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>导出文件名（需求 §2.7：待办完成记录_起始日_结束日.csv）。</summary>
    public static string ExportFileName(string from, string to) => $"待办完成记录_{from}_{to}.csv";

    /// <summary>
    /// 导入 CSV 文本。ok=false = 格式不正确；否则返回新数据与导入条数。
    /// mode=cover：清空现有全部历史（含今天已完成）后导入；merge：同属性去重合并。
    /// </summary>
    public static (AppData Data, bool Ok, int Imported) Import(
        AppData data, string text, string mode, string todayKey, DateTimeOffset now)
    {
        var rows = ParseCsv(text.TrimStart('\uFEFF'));
        if (rows.Count == 0 || rows[0].Count < 5 || rows[0][0].Trim() != "完成日期")
        {
            return (data, false, 0);
        }

        var incoming = new List<TodoItem>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 5)
            {
                continue;
            }

            var date = row[0].Trim();
            var hm = row[1].Trim();
            var content = row[2].Trim();
            if (content.Length == 0 || !LocalDate.IsValidKey(date) || !System.Text.RegularExpressions.Regex.IsMatch(hm, @"^\d{2}:\d{2}"))
            {
                continue;
            }

            var completedLocal = ParseStamp($"{date} {hm}") ?? ParseStamp(row[4].Trim());
            if (completedLocal is not { } completed || completed > now)
            {
                continue;
            }

            var created = ParseStamp(row[3].Trim()) ?? completed;
            incoming.Add(new TodoItem
            {
                Id = Ids.NewId(),
                Content = content,
                CreatedAt = IsoTime.Format(created),
                CompletedAt = IsoTime.Format(completed),
            });
        }

        if (incoming.Count == 0)
        {
            return (data, false, 0);
        }

        var next = data.DeepCopy();
        if (mode == ModeCover)
        {
            next = next with
            {
                Archive = new Dictionary<string, List<TodoItem>>(),
                Completed = next.Completed.Where(t => t.CompletedAt is null).ToList(),
            };
        }

        // 同属性去重：内容 + 完成时间戳（现有已完成 + 全部归档）
        var seen = new HashSet<string>(
            next.Completed.Select(Signature)
                .Concat(next.Archive.SelectMany(kv => kv.Value).Select(Signature)));

        var imported = 0;
        foreach (var item in incoming)
        {
            if (!seen.Add(Signature(item)))
            {
                continue;
            }

            var key = LocalDate.Key(IsoTime.ParseLocal(item.CompletedAt!));
            if (key == todayKey)
            {
                next.Completed.Add(item);
            }
            else
            {
                if (!next.Archive.TryGetValue(key, out var bucket))
                {
                    bucket = new List<TodoItem>();
                    next.Archive[key] = bucket;
                }

                bucket.Add(item);
            }

            imported++;
        }

        foreach (var key in next.Archive.Keys)
        {
            next.Archive[key].Sort((a, b) => string.CompareOrdinal(a.CompletedAt, b.CompletedAt));
        }

        return (next, true, imported);
    }

    private static string[] RowOf(string dateKey, TodoItem item)
    {
        var completed = IsoTime.ParseLocal(item.CompletedAt!);
        var created = IsoTime.ParseLocal(item.CreatedAt);
        return
        [
            dateKey,
            completed.ToString("HH:mm"),
            item.Content,
            created.ToString("yyyy-MM-dd HH:mm"),
            completed.ToString("yyyy-MM-dd HH:mm"),
        ];
    }

    private static string Signature(TodoItem item) => $"{item.Content}|{item.CompletedAt}";

    private static DateTimeOffset? ParseStamp(string text)
    {
        if (System.Text.RegularExpressions.Regex.Match(text, @"^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})")
            is not { Success: true } m)
        {
            return null;
        }

        // 按解析到的本地日期取时区偏移（夏令时安全）
        var localDateTime = new DateTime(
            int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
            int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), 0);
        return new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
    }

    private static string EscapeField(string field) =>
        field.Any(c => c is ',' or '"' or '\r' or '\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    /// <summary>标准 CSV 解析（引号转义；CRLF/LF 兼容）。</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else if (c != '\r')
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
