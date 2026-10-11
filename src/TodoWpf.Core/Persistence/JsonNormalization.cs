using System.Text.Json.Nodes;
using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Persistence;

/// <summary>
/// 旧档容忍归一化（对齐 Electron 版 normalize* 行为）：
/// 缺字段补默认、非法枚举回退默认、越界上限夹到 [1,10]、非整数上限按默认处理；
/// 元素级宽容读取（类型不对的字段取默认、非对象元素跳过）；不套用 Electron 的 pos 语义迁移。
/// </summary>
internal static class JsonNormalization
{
    public static AppData NormalizeAppData(JsonObject root) => new()
    {
        V = 1,
        Todos = ReadTodoItems(root["todos"]),
        Completed = ReadTodoItems(root["completed"]),
        Archive = ReadArchive(root["archive"]),
        Templates = ReadTemplates(root["templates"]),
        Settings = NormalizeSettings(root["settings"] as JsonObject),
        UiState = NormalizeUiState(root["uiState"] as JsonObject),
    };

    private static List<TodoItem> ReadTodoItems(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return new List<TodoItem>();
        }

        var items = new List<TodoItem>(array.Count);
        foreach (var element in array)
        {
            if (element is JsonObject obj)
            {
                items.Add(ReadTodoItem(obj));
            }
        }

        return items;
    }

    private static TodoItem ReadTodoItem(JsonObject obj) => new()
    {
        Id = AsString(obj["id"]) ?? "",
        Content = AsString(obj["content"]) ?? "",
        CreatedAt = AsString(obj["createdAt"]) ?? "",
        CompletedAt = AsString(obj["completedAt"]),
        Overdue = AsBool(obj["overdue"]) ?? false,
    };

    private static List<TemplateItem> ReadTemplates(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return new List<TemplateItem>();
        }

        var items = new List<TemplateItem>(array.Count);
        foreach (var element in array)
        {
            if (element is not JsonObject obj)
            {
                continue;
            }

            items.Add(new TemplateItem
            {
                Id = AsString(obj["id"]) ?? "",
                Content = AsString(obj["content"]) ?? "",
                CreatedAt = AsString(obj["createdAt"]) ?? "",
            });
        }

        return items;
    }

    private static Dictionary<string, List<TodoItem>> ReadArchive(JsonNode? node)
    {
        var archive = new Dictionary<string, List<TodoItem>>();
        if (node is not JsonObject obj)
        {
            return archive;
        }

        foreach (var (key, value) in obj)
        {
            if (value is JsonArray array)
            {
                archive[key] = ReadTodoItems(array);
            }
        }

        return archive;
    }

    private static AppSettings NormalizeSettings(JsonObject? raw)
    {
        var defaults = Defaults.DefaultSettings();
        if (raw is null)
        {
            return defaults;
        }

        var maxItems = AsIntegralNumber(raw["maxItems"]);
        var doneSort = AsString(raw["doneSort"]);

        return new AppSettings
        {
            MaxItems = maxItems is null
                ? defaults.MaxItems
                : Math.Clamp(maxItems.Value, AppSettings.MinMaxItems, AppSettings.MaxMaxItems),
            Hour12 = AsBool(raw["hour12"]) ?? defaults.Hour12,
            AutoStart = AsBool(raw["autoStart"]) ?? defaults.AutoStart,
            DoneSort = doneSort is "asc" or "desc" ? doneSort : defaults.DoneSort,
        };
    }

    private static UiState NormalizeUiState(JsonObject? raw)
    {
        var defaults = Defaults.DefaultUiState();
        if (raw is null)
        {
            return defaults;
        }

        Position? pos = null;
        if (raw["pos"] is JsonObject posObj)
        {
            var x = AsNumber(posObj["x"]);
            var y = AsNumber(posObj["y"]);
            if (x is not null && y is not null)
            {
                pos = new Position { X = x.Value, Y = y.Value };
            }
        }

        var childScales = new Dictionary<string, double>();
        if (raw["childScales"] is JsonObject childScalesObj)
        {
            foreach (var (key, value) in childScalesObj)
            {
                var scale = AsNumber(value);
                if (scale is > 0)
                {
                    childScales[key] = scale.Value;
                }
            }
        }

        var clusterScale = AsNumber(raw["clusterScale"]);
        var theme = AsString(raw["theme"]);
        var lastSettleDate = AsString(raw["lastSettleDate"]);

        return new UiState
        {
            Pos = pos,
            PosIsTodoAnchor = AsBool(raw["posIsTodoAnchor"]) ?? defaults.PosIsTodoAnchor,
            ClusterScale = clusterScale is > 0 ? clusterScale.Value : defaults.ClusterScale,
            ChildScales = childScales,
            QuickVisible = AsBool(raw["quickVisible"]) ?? defaults.QuickVisible,
            DoneVisible = AsBool(raw["doneVisible"]) ?? defaults.DoneVisible,
            Theme = theme is "day" or "night" ? theme : defaults.Theme,
            TopMost = AsBool(raw["topMost"]) ?? defaults.TopMost,
            TuckLock = AsBool(raw["tuckLock"]) ?? defaults.TuckLock,
            LastSettleDate = LocalDate.IsValidKey(lastSettleDate) ? lastSettleDate : defaults.LastSettleDate,
        };
    }

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool? AsBool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;

    private static double? AsNumber(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var result) ? result : null;

    private static int? AsIntegralNumber(JsonNode? node)
    {
        var value = AsNumber(node);
        if (value is null)
        {
            return null;
        }

        var number = value.Value;
        if (double.IsNaN(number) || double.IsInfinity(number) || Math.Truncate(number) != number)
        {
            return null;
        }

        return number is < int.MinValue or > int.MaxValue ? null : (int)number;
    }
}
