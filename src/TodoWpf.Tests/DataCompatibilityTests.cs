using System.Text.Json.Nodes;
using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;

namespace TodoWpf.Tests;

/// <summary>数据格式兼容判定（C4 版本兼容检测的纯逻辑部分）。</summary>
public class DataCompatibilityTests
{
    [Fact]
    public void SameAsCurrent_IsCompatible()
    {
        Assert.Equal(DataCompatibilityResult.Compatible, DataCompatibility.Check(AppData.CurrentSchemaVersion));
    }

    [Fact]
    public void NewerThanCurrent_IsTooNew()
    {
        Assert.Equal(DataCompatibilityResult.TooNew, DataCompatibility.Check(AppData.CurrentSchemaVersion + 1));
    }

    [Fact]
    public void OlderThanReadableFloor_IsTooOld()
    {
        Assert.Equal(
            DataCompatibilityResult.TooOld,
            DataCompatibility.Check(AppData.MinReadableSchemaVersion - 1));
    }

    [Fact]
    public void MissingVersion_TreatedAsLegacyCompatible()
    {
        // 历史档可能没有 v 字段（Electron 兼容档）→ 按 v1 处理，可读。
        Assert.Equal(DataCompatibilityResult.Compatible, DataCompatibility.Check(null));
    }
}

/// <summary>从 JSON 文本里只取数据格式版本 v（不归一化、不构造对象）。</summary>
public class ReadSchemaVersionTests
{
    [Fact]
    public void Reads_TopLevel_V()
    {
        Assert.Equal(7, JsonCodec.ReadSchemaVersion("{\"v\":7,\"todos\":[]}"));
    }

    [Fact]
    public void Missing_V_Returns_Null()
    {
        Assert.Null(JsonCodec.ReadSchemaVersion("{\"todos\":[]}"));
    }

    [Fact]
    public void NonInteger_V_Returns_Null()
    {
        Assert.Null(JsonCodec.ReadSchemaVersion("{\"v\":\"1\"}"));
    }

    [Fact]
    public void Invalid_Json_Returns_Null()
    {
        Assert.Null(JsonCodec.ReadSchemaVersion("{ not json"));
    }

    [Fact]
    public void Empty_Text_Returns_Null()
    {
        Assert.Null(JsonCodec.ReadSchemaVersion(""));
    }
}

/// <summary>
/// 数据形状守卫（C4「提醒开发者」机制）：
/// data.json 的顶层字段集合或写出的 v 值一旦变化，这里失败 →
/// 必须显式决定：是兼容新增（登记进金样）还是**不兼容变更**（提升 AppData.CurrentSchemaVersion
/// 并同步本测试期望），从而保证任何格式变更都不会悄悄溜过去。
/// </summary>
public class DataShapeGuardTests
{
    private static readonly string[] GoldenTopLevelFields =
        ["archive", "completed", "settings", "templates", "todos", "uiState", "v"];

    [Fact]
    public void TopLevelFields_MatchDeclaredSchema()
    {
        var root = JsonNode.Parse(JsonCodec.Write(Defaults.DefaultAppData()))!.AsObject();
        var fields = root.Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(GoldenTopLevelFields, fields);
    }

    [Fact]
    public void WrittenSchemaVersion_EqualsDeclared()
    {
        var root = JsonNode.Parse(JsonCodec.Write(Defaults.DefaultAppData()))!.AsObject();

        Assert.Equal(AppData.CurrentSchemaVersion, (int)root["v"]!);
    }
}
