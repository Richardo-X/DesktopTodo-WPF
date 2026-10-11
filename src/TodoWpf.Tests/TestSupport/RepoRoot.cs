using System.Xml.Linq;

namespace TodoWpf.Tests.TestSupport;

/// <summary>定位仓库根目录（从测试输出目录向上找到含 TodoWpf.slnx 的目录）。</summary>
internal static class RepoRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "TodoWpf.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("未找到仓库根目录（TodoWpf.slnx）");
        }

        return dir.FullName;
    }

    public static string File(params string[] relative) =>
        System.IO.Path.Combine([Path, .. relative]);

    public static string ReadText(params string[] relative) =>
        System.IO.File.ReadAllText(File(relative));

    public static XDocument LoadXaml(params string[] relative) =>
        XDocument.Load(File(relative), LoadOptions.None);
}

/// <summary>XAML 令牌读取辅助。</summary>
internal static class XamlTokenReader
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>key → 元素（含 x:Key 的任意元素）。</summary>
    public static Dictionary<string, XElement> LoadKeyed(params string[] relative)
    {
        var doc = RepoRoot.LoadXaml(relative);
        return doc
            .Descendants()
            .Where(e => e.Attribute(X + "Key") is not null)
            .GroupBy(e => e.Attribute(X + "Key")!.Value)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public static Dictionary<string, XElement> LoadKeyedMany(params string[][] files) =>
        files.SelectMany(LoadKeyed).ToDictionary(kv => kv.Key, kv => kv.Value);
}
