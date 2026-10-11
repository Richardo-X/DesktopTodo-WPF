using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Tests.TestSupport;

/// <summary>测试时间工具：一律以「本机本地时间」构造瞬时，本地日期键不随测试机时区漂移。</summary>
internal static class TestTime
{
    public static DateTimeOffset Local(
        int year, int month, int day,
        int hour = 12, int minute = 0, int second = 0, int ms = 0)
    {
        var wallClock = new DateTime(year, month, day, hour, minute, second, ms, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallClock, TimeZoneInfo.Local.GetUtcOffset(wallClock));
    }
}

/// <summary>构造领域对象的便捷方法。</summary>
internal static class TestItems
{
    public static TodoItem Todo(
        string content,
        DateTimeOffset created,
        bool overdue = false,
        DateTimeOffset? completed = null,
        string? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid().ToString("D"),
            Content = content,
            CreatedAt = IsoTime.Format(created),
            CompletedAt = completed is null ? null : IsoTime.Format(completed.Value),
            Overdue = overdue,
        };
}

/// <summary>夹具文件读取（配合 csproj 的 Fixtures 复制到输出目录）。</summary>
internal static class Fixtures
{
    public static string Text(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}

internal static class TestAsync
{
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000, int stepMs = 20)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(stepMs);
        }

        return condition();
    }
}
