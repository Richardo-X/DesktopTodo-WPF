using System.Xml.Linq;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>
/// 行内几何配平闸门（2026-11-04 教训）。
/// 起因：RowCard 用 Height=44 + Padding="11,12"，而 WPF 的 Padding 是「左右,上下」——
/// ele 的 padding:11px 12px 是「上下 11 / 左右 12」，写反后行内可用高 = 44-24-2 = 18 &lt; 勾选框 20，
/// 勾选框被挤溢出（真机表现为"勾选框上下被切"）。这类"纯数字配平"错误编译与单测都不报，
/// 故在此用**从 XAML 实读数值再断言不等式**的方式做闸门。
/// </summary>
public class RowLayoutInvariantTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>读某个 Style 里某属性的 Setter 值。</summary>
    private static string? SetterValue(string styleKey, string property, params string[] file)
    {
        var doc = RepoRoot.LoadXaml(file);
        var style = doc
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" && e.Attribute(X + "Key")?.Value == styleKey);
        return style?
            .Elements()
            .Where(e => e.Name.LocalName == "Setter" && e.Attribute("Property")?.Value == property)
            .Select(e => e.Attribute("Value")?.Value)
            .FirstOrDefault();
    }

    /// <summary>解析 Thickness："a" / "左右,上下" / "左,上,右,下"。</summary>
    private static (double Left, double Top, double Right, double Bottom) ParseThickness(string value)
    {
        var parts = value.Split(',').Select(p => double.Parse(p.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts.Length switch
        {
            1 => (parts[0], parts[0], parts[0], parts[0]),
            2 => (parts[0], parts[1], parts[0], parts[1]),
            _ => (parts[0], parts[1], parts[2], parts[3]),
        };
    }

    private static double Number(string? value) =>
        double.Parse(value ?? throw new InvalidOperationException("缺少属性值"), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void 待办行卡_行内可用高度_必须容得下勾选框()
    {
        string[] file = ["src", "TodoWpf", "Windows", "MainTodoWindow.xaml"];

        var cardHeight = Number(SetterValue("RowCard", "Height", file));
        var padding = ParseThickness(SetterValue("RowCard", "Padding", file)!);
        var border = ParseThickness(SetterValue("RowCard", "BorderThickness", file)!);
        var checkSize = Number(SetterValue("RowCheck", "Height", file));

        var innerHeight = cardHeight - padding.Top - padding.Bottom - border.Top - border.Bottom;

        Assert.True(
            innerHeight >= checkSize,
            $"行内可用高 {innerHeight} < 勾选框 {checkSize}（行高 {cardHeight}，上下内边距 {padding.Top}/{padding.Bottom}，上下边框 {border.Top}/{border.Bottom}）" +
            "——勾选框会被挤溢出。注意 WPF Padding 写法是「左右,上下」。");
    }

    [Fact]
    public void 待办行文字_行高必须容得进行内可用高()
    {
        var doc = RepoRoot.LoadXaml("src", "TodoWpf", "Windows", "MainTodoWindow.xaml");
        string[] file = ["src", "TodoWpf", "Windows", "MainTodoWindow.xaml"];

        var cardHeight = Number(SetterValue("RowCard", "Height", file));
        var padding = ParseThickness(SetterValue("RowCard", "Padding", file)!);
        var border = ParseThickness(SetterValue("RowCard", "BorderThickness", file)!);
        var innerHeight = cardHeight - padding.Top - padding.Bottom - border.Top - border.Bottom;

        // 待办行文字（绑定 Content 且带行高的那个 TextBlock）
        var lineHeights = doc
            .Descendants()
            .Where(e => e.Name.LocalName == "TextBlock"
                        && e.Attribute("LineHeight") is not null
                        && e.Attribute("Text")?.Value?.Contains("Content") == true)
            .Select(e => Number(e.Attribute("LineHeight")!.Value))
            .ToList();

        Assert.NotEmpty(lineHeights);
        foreach (var lh in lineHeights)
        {
            Assert.True(lh <= innerHeight, $"文字行高 {lh} > 行内可用高 {innerHeight}，文字会溢出或被裁");
        }
    }

    [Fact]
    public void 模板窗行_必须容得下勾选框()
    {
        string[] file = ["src", "TodoWpf", "Windows", "QuickWindow.xaml"];
        var doc = RepoRoot.LoadXaml(file);

        // 模板行是 Grid Height="44"，无内边距
        var rowHeight = doc
            .Descendants()
            .Where(e => e.Name.LocalName == "Grid" && e.Attribute("Height")?.Value == "44")
            .Select(e => Number(e.Attribute("Height")!.Value))
            .FirstOrDefault();
        Assert.True(rowHeight > 0, "未找到模板行高度（Grid Height=44）");

        var checkSize = Number(SetterValue("RowCheck", "Height", file));
        Assert.True(rowHeight >= checkSize, $"模板行高 {rowHeight} < 勾选框 {checkSize}");
    }
}
