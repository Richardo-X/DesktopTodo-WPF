using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TodoWpf.Core.Models;

namespace TodoWpf.Core.Persistence;

/// <summary>
/// data.json 编解码（与 Electron 版逐字段兼容）：
/// 读取＝宽容归一化（缺字段补默认、非法值回退、非对象内容按首次启动档处理）；
/// 写出＝camelCase、2 空格缩进、LF 行尾、中文与 HTML 敏感字符不转义（形态对齐 JSON.stringify(data, null, 2)）。
/// 已知形态差异（解析兼容不受影响）：非 BMP 字符（emoji）由 .NET 编码器写作代理对转义（\uD83D\uDE00），
/// JSON.stringify 写原字符——两者 JSON 语义等价，互读无碍。
/// </summary>
public static class JsonCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>解析并归一化；JSON 语法非法时抛 <see cref="JsonException"/>（仓储层据此按损坏档留档）。</summary>
    public static AppData Read(string json)
    {
        var node = JsonNode.Parse(json);
        return node is JsonObject root
            ? JsonNormalization.NormalizeAppData(root)
            : Defaults.DefaultAppData();
    }

    public static string Write(AppData data)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            JsonSerializer.Serialize(writer, data, SerializerOptions);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// 只取文件里的数据格式版本 `v`（C4 兼容检测用）：不做归一化、不构造对象。
    /// 无 v 字段 / 非整数 / JSON 非法 → null（调用方按历史档处理）。
    /// </summary>
    public static int? ReadSchemaVersion(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json) is JsonObject root
                   && root["v"] is JsonValue value
                   && value.TryGetValue<int>(out var version)
                ? version
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
