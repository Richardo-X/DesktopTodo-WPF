using System.Text.Json;
using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>data.json 兼容读写：旧档全字段保真、缺字段补默认、非法值回退、写出形态与 Electron 版一致。</summary>
public class JsonCodecTests
{
    [Fact]
    public void Read_LegacyReproFixture_FullFidelity()
    {
        var data = JsonCodec.Read(Fixtures.Text("electron-repro-data.json"));

        Assert.Equal(2, data.Todos.Count);
        Assert.Equal(10, data.Completed.Count);
        Assert.Single(data.Templates);
        Assert.Empty(data.Archive);

        Assert.Equal("c4324ec2-fee3-45f1-add1-4e7a8aeabaa8", data.Todos[0].Id);
        Assert.Equal("1", data.Todos[0].Content);
        Assert.Equal("2026-09-24T17:15:01.832Z", data.Todos[0].CreatedAt);
        Assert.Null(data.Todos[0].CompletedAt);
        Assert.False(data.Todos[0].Overdue);

        Assert.Equal("77aae163-34d2-406b-8331-eab4f952e736", data.Completed[6].Id);
        Assert.True(data.Completed[6].Overdue);
        Assert.Equal("2026-09-25T08:58:38.640Z", data.Completed[6].CompletedAt);

        Assert.Equal("a36aee3d-b687-4362-ac7a-1aa332f0212e", data.Templates[0].Id);

        Assert.Equal(10, data.Settings.MaxItems);
        Assert.False(data.Settings.Hour12);
        Assert.True(data.Settings.AutoStart);
        Assert.Equal("asc", data.Settings.DoneSort);

        Assert.Equal(745d, data.UiState.Pos!.X);
        Assert.Equal(408d, data.UiState.Pos!.Y);
        Assert.Equal(1.125, data.UiState.ClusterScale);
        Assert.Equal(1.6, data.UiState.ChildScales["todo"]);
        Assert.Equal(0.978, data.UiState.ChildScales["settings"]);
        Assert.True(data.UiState.QuickVisible);
        Assert.True(data.UiState.DoneVisible);
        Assert.Equal("day", data.UiState.Theme);
        Assert.False(data.UiState.TopMost);
        Assert.False(data.UiState.TuckLock);
        Assert.Equal("2026-09-25", data.UiState.LastSettleDate);
    }

    [Fact]
    public void Read_LegacyFixture_WithoutAnchorFlag_KeepsRawPosition()
    {
        // 旧档无 posIsTodoAnchor 字段：坐标原样保留，不套用 Electron 版的一次性迁移（WPF 几何模型另定）
        var data = JsonCodec.Read(Fixtures.Text("electron-repro-data.json"));

        Assert.False(data.UiState.PosIsTodoAnchor);
        Assert.Equal(745d, data.UiState.Pos!.X);
    }

    [Fact]
    public void Read_FullFixture_PreservesEverything()
    {
        var data = JsonCodec.Read(Fixtures.Text("full-data.json"));

        Assert.Equal(2, data.Todos.Count);
        Assert.Equal(3, data.Completed.Count);
        Assert.Equal("买牛奶 🥛", data.Todos[1].Content);
        Assert.Null(data.Completed[2].CompletedAt);

        Assert.Equal(2, data.Archive.Count);
        Assert.Equal("归档事项一", data.Archive["2026-10-07"][0].Content);
        Assert.True(data.Archive["2026-10-06"][0].Overdue);

        Assert.Equal(2, data.Templates.Count);
        Assert.Equal("每日站会", data.Templates[0].Content);

        Assert.Equal(5, data.Settings.MaxItems);
        Assert.True(data.Settings.Hour12);
        Assert.False(data.Settings.AutoStart);
        Assert.Equal("desc", data.Settings.DoneSort);

        Assert.True(data.UiState.PosIsTodoAnchor);
        Assert.Equal(1.125, data.UiState.ClusterScale);
        Assert.False(data.UiState.QuickVisible);
        Assert.True(data.UiState.DoneVisible);
        Assert.Equal("night", data.UiState.Theme);
        Assert.True(data.UiState.TopMost);
        Assert.True(data.UiState.TuckLock);
        Assert.Equal("2026-10-09", data.UiState.LastSettleDate);
    }

    [Theory]
    [InlineData("""{"v":1}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("\"text\"")]
    [InlineData("[]")]
    public void Read_MissingOrNonObjectSections_FallBackToDefaults(string json)
    {
        var data = JsonCodec.Read(json);

        Assert.Equal(1, data.V);
        Assert.Empty(data.Todos);
        Assert.Empty(data.Completed);
        Assert.Empty(data.Archive);
        Assert.Empty(data.Templates);
        Assert.Equal(10, data.Settings.MaxItems);
        Assert.False(data.Settings.Hour12);
        Assert.False(data.Settings.AutoStart);
        Assert.Equal("asc", data.Settings.DoneSort);
        Assert.Null(data.UiState.Pos);
        Assert.Equal(1, data.UiState.ClusterScale);
        Assert.True(data.UiState.QuickVisible);
        Assert.True(data.UiState.DoneVisible);
        Assert.Equal("day", data.UiState.Theme);
        Assert.Null(data.UiState.LastSettleDate);
    }

    [Fact]
    public void Read_InvalidSettings_ClampsAndFallsBack()
    {
        var zero = JsonCodec.Read("""{"settings":{"maxItems":0,"hour12":"yes","doneSort":"zzz"}}""");
        Assert.Equal(1, zero.Settings.MaxItems); // 整数越界 → 夹到 [1,10]
        Assert.False(zero.Settings.Hour12);
        Assert.Equal("asc", zero.Settings.DoneSort);

        var big = JsonCodec.Read("""{"settings":{"maxItems":99,"hour12":true,"autoStart":true}}""");
        Assert.Equal(10, big.Settings.MaxItems);
        Assert.True(big.Settings.Hour12);
        Assert.True(big.Settings.AutoStart);

        var fractional = JsonCodec.Read("""{"settings":{"maxItems":2.5}}""");
        Assert.Equal(10, fractional.Settings.MaxItems); // 非整数 → 默认

        var wrongType = JsonCodec.Read("""{"settings":{"maxItems":"7"}}""");
        Assert.Equal(10, wrongType.Settings.MaxItems);
    }

    [Fact]
    public void Read_InvalidUiState_FallsBackFieldByField()
    {
        var data = JsonCodec.Read(
            """{"uiState":{"theme":"weird","clusterScale":0,"childScales":{"a":0,"b":1.5,"c":"x"},"quickVisible":false,"lastSettleDate":"2026/10/11"}}""");

        Assert.Equal("day", data.UiState.Theme);
        Assert.Equal(1, data.UiState.ClusterScale);
        Assert.False(data.UiState.ChildScales.ContainsKey("a"));
        Assert.False(data.UiState.ChildScales.ContainsKey("c"));
        Assert.Equal(1.5, data.UiState.ChildScales["b"]);
        Assert.False(data.UiState.QuickVisible);
        Assert.True(data.UiState.DoneVisible);
        Assert.Null(data.UiState.LastSettleDate);
    }

    [Fact]
    public void Read_TolerantItems_SkipsNonObjects_FallsBackOnWrongTypes()
    {
        var data = JsonCodec.Read(
            """{"todos":[{"id":123,"content":"ok","createdAt":42,"overdue":"yes"},"not-an-object"],"completed":{}}""");

        var item = Assert.Single(data.Todos);
        Assert.Equal("", item.Id);
        Assert.Equal("ok", item.Content);
        Assert.Equal("", item.CreatedAt);
        Assert.False(item.Overdue);
        Assert.Null(item.CompletedAt);
        Assert.Empty(data.Completed);
    }

    [Fact]
    public void Read_Archive_SkipsNonArrayValues()
    {
        var data = JsonCodec.Read("""{"archive":{"2026-10-01":[{"content":"a"}],"bad":"x"}}""");

        Assert.True(data.Archive.ContainsKey("2026-10-01"));
        Assert.False(data.Archive.ContainsKey("bad"));
        Assert.Single(data.Archive["2026-10-01"]);
    }

    [Fact]
    public void Read_MalformedJson_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => JsonCodec.Read("{oops"));
    }

    [Fact]
    public void Write_EmptyDefaultData_MatchesLegacyShapeExactly()
    {
        var expected =
            "{\n" +
            "  \"v\": 1,\n" +
            "  \"todos\": [],\n" +
            "  \"completed\": [],\n" +
            "  \"archive\": {},\n" +
            "  \"templates\": [],\n" +
            "  \"settings\": {\n" +
            "    \"maxItems\": 10,\n" +
            "    \"hour12\": false,\n" +
            "    \"autoStart\": false,\n" +
            "    \"doneSort\": \"asc\"\n" +
            "  },\n" +
            "  \"uiState\": {\n" +
            "    \"pos\": null,\n" +
            "    \"posIsTodoAnchor\": false,\n" +
            "    \"posIsRawPx\": false,\n" +
            "    \"updateNotifyEnabled\": true,\n" +
            "    \"lastRunVersion\": null,\n" +
            "    \"clusterScale\": 1,\n" +
            "    \"childScales\": {},\n" +
            "    \"quickVisible\": true,\n" +
            "    \"doneVisible\": true,\n" +
            "    \"theme\": \"day\",\n" +
            "    \"topMost\": false,\n" +
            "    \"tuckLock\": false,\n" +
            "    \"lastSettleDate\": null\n" +
            "  }\n" +
            "}";

        Assert.Equal(expected, JsonCodec.Write(Defaults.DefaultAppData()));
    }

    [Fact]
    public void Write_ChineseNotEscaped_HtmlCharsRaw_LfOnly_TwoSpaceIndent()
    {
        var data = Defaults.DefaultAppData() with
        {
            Todos = [TestItems.Todo("中文😀<&>", TestTime.Local(2026, 1, 1, 9))],
        };

        var text = JsonCodec.Write(data);

        Assert.Contains("\"content\": \"中文", text);
        Assert.Contains("<&>", text); // 与 JSON.stringify 一致：HTML 敏感字符不转义
        Assert.Contains("\n  \"todos\"", text);
        Assert.Contains("\n      \"content\"", text);
        Assert.DoesNotContain("\r", text);
        // 已知形态差异（解析兼容不受影响）：.NET 编码器把非 BMP 字符（emoji）转义为代理对，
        // JSON.stringify 写原字符——两者 JSON 语义等价，旧版与本版都能互相读取。
        Assert.Contains(@"\uD83D\uDE00", text);
        Assert.Equal("中文😀<&>", JsonCodec.Read(text).Todos[0].Content);
    }

    [Fact]
    public void RoundTrip_ReadWriteRead_KeepsSemantics()
    {
        var first = JsonCodec.Read(Fixtures.Text("full-data.json"));

        var second = JsonCodec.Read(JsonCodec.Write(first));

        Assert.Equal(first.Todos.Count, second.Todos.Count);
        Assert.Equal(first.Completed.Count, second.Completed.Count);
        Assert.Equal(first.Templates.Count, second.Templates.Count);
        Assert.Equal(first.Archive.Keys.OrderBy(k => k), second.Archive.Keys.OrderBy(k => k));
        Assert.Equal(first.Settings, second.Settings);
        Assert.Equal(first.UiState.Theme, second.UiState.Theme);
        Assert.Equal(first.UiState.ChildScales, second.UiState.ChildScales);
        Assert.Equal(first.UiState.LastSettleDate, second.UiState.LastSettleDate);
        Assert.Equal(first.Todos[1].Content, second.Todos[1].Content);
    }
}
