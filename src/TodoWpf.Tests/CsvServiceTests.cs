using TodoWpf.Core.Models;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;

namespace TodoWpf.Tests;

/// <summary>批次05③：历史 CSV 导出/导入（纯函数）。</summary>
public class CsvServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private const string Today = "2026-10-13";

    private static TodoItem Done(string content, string completedIso, string? createdIso = null) => new()
    {
        Id = Ids.NewId(),
        Content = content,
        CreatedAt = createdIso ?? "2026-10-12T01:00:00.000Z",
        CompletedAt = completedIso,
    };

    [Fact]
    public void 导出_区间过滤_五列格式()
    {
        var data = new AppData
        {
            Archive = new Dictionary<string, List<TodoItem>>
            {
                ["2026-10-11"] = [Done("两天前的事", "2026-10-11T03:00:00.000Z")],
                ["2026-10-12"] = [Done("含,逗号", "2026-10-12T03:00:00.000Z")],
            },
        };

        var csv = CsvService.Export(data, "2026-10-12", "2026-10-13");
        Assert.NotNull(csv);
        Assert.StartsWith("\uFEFF完成日期,完成时间,事项内容,创建时间,完成时间\r\n", csv);
        Assert.DoesNotContain("两天前的事", csv); // 区间外不导出
        Assert.Contains("\"含,逗号\"", csv);       // 含逗号字段加引号
        Assert.Equal("待办完成记录_2026-10-12_2026-10-13.csv", CsvService.ExportFileName("2026-10-12", "2026-10-13"));
    }

    [Fact]
    public void 导出_无记录_起止颠倒_返回null()
    {
        var data = new AppData();
        Assert.Null(CsvService.Export(data, "2026-10-01", "2026-10-02"));
        Assert.Null(CsvService.Export(new AppData
        {
            Archive = new Dictionary<string, List<TodoItem>>
            {
                ["2026-10-11"] = [Done("x", "2026-10-11T03:00:00.000Z")],
            },
        }, "2026-10-13", "2026-10-01"));
    }

    [Fact]
    public void 导入_合并_同属性去重_按完成日落位()
    {
        // 现有项与 CSV 行用同一本地时刻现算 UTC 串 → 任何测试机时区下都构成重复
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 12));
        var dupUtcIso = IsoTime.Format(new DateTimeOffset(2026, 10, 12, 11, 5, 0, localOffset));
        var existing = Done("已有一条", dupUtcIso);
        var data = new AppData
        {
            Completed = [existing],
            Archive = new Dictionary<string, List<TodoItem>>(),
        };
        var csv = "\uFEFF完成日期,完成时间,事项内容,创建时间,完成时间\r\n" +
                  "2026-10-12,11:05,已有一条,2026-10-12 09:00,2026-10-12 11:05\r\n" + // 与现有重复
                  "2026-10-12,11:06,新的一条,2026-10-12 09:00,2026-10-12 11:06\r\n";

        var (next, ok, imported) = CsvService.Import(data, csv, CsvService.ModeMerge, Today, Now);

        Assert.True(ok);
        Assert.Equal(1, imported); // 重复项跳过
        var archived = Assert.Single(next.Archive["2026-10-12"]);
        Assert.Equal("新的一条", archived.Content);
        Assert.Equal(IsoTime.Format(new DateTimeOffset(2026, 10, 12, 11, 6, 0, localOffset)), archived.CompletedAt);
    }

    [Fact]
    public void 导入_覆盖_清空全部历史后导入()
    {
        var data = new AppData
        {
            Completed = [Done("今天完成的", "2026-10-13T03:00:00.000Z")],
            Archive = new Dictionary<string, List<TodoItem>>
            {
                ["2026-10-11"] = [Done("旧历史", "2026-10-11T03:00:00.000Z")],
            },
        };
        var csv = "\uFEFF完成日期,完成时间,事项内容,创建时间,完成时间\r\n" +
                  "2026-10-12,11:06,导入的一条,2026-10-12 09:00,2026-10-12 11:06\r\n";

        var (next, ok, imported) = CsvService.Import(data, csv, CsvService.ModeCover, Today, Now);

        Assert.True(ok);
        Assert.Equal(1, imported);
        Assert.False(next.Archive.ContainsKey("2026-10-11")); // 旧归档清空
        Assert.Empty(next.Completed); // 今天完成项也清空
        Assert.Equal("导入的一条", next.Archive["2026-10-12"].Single().Content);
    }

    [Fact]
    public void 导入_表头不对_无有效行_拒绝()
    {
        var data = new AppData();
        Assert.False(CsvService.Import(data, "a,b,c,d,e\r\n1,2,3,4,5\r\n", CsvService.ModeMerge, Today, Now).Ok);
        Assert.False(CsvService.Import(
            data, "\uFEFF完成日期,完成时间,事项内容,创建时间,完成时间\r\n2026-10-12,11:06,,2026-10-12 09:00,2026-10-12 11:06\r\n",
            CsvService.ModeMerge, Today, Now).Ok); // 空内容行被滤掉 → 无有效行
    }
}
