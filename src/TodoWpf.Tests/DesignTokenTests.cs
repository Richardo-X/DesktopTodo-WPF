using System.Text.RegularExpressions;
using System.Xml.Linq;
using TodoWpf.Tests.TestSupport;

namespace TodoWpf.Tests;

/// <summary>
/// 设计令牌锁定（批次 02）：基准 = docs/参考/交互原型-v3.4-taste.html + v1.1.1 L1 体验说明书。
/// 令牌是后续所有批次的唯一颜色/尺寸来源，改值必须显式改这里并被真机验收。
/// </summary>
public class DesignTokenTests
{
    // ---------- 日/夜配色（原型 :root 与 [data-theme=night] 逐值对齐） ----------

    [Fact]
    public void DayTheme_HasPrototypeValues()
    {
        var tokens = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Day.xaml");

        Assert.Equal("#4F7391", Hex(tokens["Color.Accent"]));
        Assert.Equal("#5F86A5", Hex(tokens["Color.AccentHover"]));
        Assert.Equal("#F7FAFC", Hex(tokens["Color.AccentFg"]));
        Assert.Equal("#F3F6FB", Hex(tokens["Color.WinBgBase"]));
        Assert.Equal("#33383F", Hex(tokens["Color.Text"]));
        Assert.Equal("#5C636C", Hex(tokens["Color.Muted"]));
        Assert.Equal("#676D76", Hex(tokens["Color.Faint"]));
        Assert.Equal("#C42B1C", Hex(tokens["Color.Danger"]));
        Assert.Equal("#A83A34", Hex(tokens["Color.Overdue"]));
        Assert.Equal("#F9FAFC", Hex(tokens["Color.CardSolid"]));
        Assert.Equal("#222222", Hex(tokens["Color.BubbleFg"]));
    }

    [Fact]
    public void NightTheme_HasPrototypeValues()
    {
        var tokens = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Night.xaml");

        Assert.Equal("#8FB0C9", Hex(tokens["Color.Accent"]));
        Assert.Equal("#A3C0D6", Hex(tokens["Color.AccentHover"]));
        Assert.Equal("#0E1418", Hex(tokens["Color.AccentFg"]));
        Assert.Equal("#1E1E21", Hex(tokens["Color.WinBgBase"]));
        Assert.Equal("#DDE2E8", Hex(tokens["Color.Text"]));
        Assert.Equal("#A8B0BA", Hex(tokens["Color.Muted"]));
        Assert.Equal("#9099A3", Hex(tokens["Color.Faint"]));
        Assert.Equal("#2C2C2E", Hex(tokens["Color.CardSolid"]));
        Assert.Equal("#3A3A3C", Hex(tokens["Color.BubbleBg"]));
    }

    [Fact]
    public void DangerAndOverdue_AreSharedAcrossThemes()
    {
        var day = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Day.xaml");
        var night = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Night.xaml");

        // danger 两主题同值；overdue 在新外观里日夜各一档（1C 原型）
        Assert.Equal(Hex(day["Color.Danger"]), Hex(night["Color.Danger"]));
        Assert.Equal("#A83A34", Hex(day["Color.Overdue"]));
        Assert.Equal("#E08A83", Hex(night["Color.Overdue"]));
    }

    [Fact]
    public void SemiTransparentBrushes_MatchPrototypeAlphas()
    {
        var day = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Day.xaml");
        var night = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Night.xaml");

        // #AARRGGBB（WPF 顺序）；α 换算：0.94→F0、0.7→B3、0.055→0E、0.09→17、0.05→0D、0.75→BF、0.13→21
        Assert.Equal("#F0F3F6FB", Argb(day["Brush.WinBg"])); // 基准 α.94
        Assert.Equal("#B3FFFFFF", Argb(day["Brush.Card"]));
        Assert.Equal("#17000000", Argb(day["Brush.CardBorder"]));
        Assert.Equal("#0D000000", Argb(day["Brush.Hover"]));
        Assert.Equal("#BFFFFFFF", Argb(day["Brush.Btn"]));
        Assert.Equal("#21000000", Argb(day["Brush.BtnBorder"]));

        Assert.Equal("#F51E1E21", Argb(night["Brush.WinBg"])); // 基准 α.96
        Assert.Equal("#0EFFFFFF", Argb(night["Brush.Card"]));
        Assert.Equal("#1CFFFFFF", Argb(night["Brush.CardBorder"]));
        Assert.Equal("#0FFFFFFF", Argb(night["Brush.Hover"]));
        Assert.Equal("#14FFFFFF", Argb(night["Brush.Btn"]));
        Assert.Equal("#26FFFFFF", Argb(night["Brush.BtnBorder"]));
        Assert.Equal("#29FFFFFF", Argb(night["Brush.BtnHover"]));
    }

    // ---------- L1 视觉精修继承（已验收资产，不得回退） ----------

    [Fact]
    public void L1_ShadowTintIsBlueBlack_NotPureBlack()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        // rgba(10,20,40,…) 蓝黑色调（L1 第 2 项）
        Assert.Equal("#0A1428", Hex(common["Color.ShadowTint"]));
        // 确认框双层投影的透明度分档（.5 / .25）
        Assert.Equal("0.5", Text(common["Double.DialogShadowOpacity"]));
        Assert.Equal("0.25", Text(common["Double.DialogShadowOpacitySoft"]));
    }

    [Fact]
    public void L1_MaskAndBlockedVeil_UseTintedColors()
    {
        var day = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Day.xaml");

        // 蒙层 rgba(10,20,40,.46) → #750A1428；模态置灰 rgba(20,20,25,.20) → #33141419
        Assert.Equal("#750A1428", Argb(day["Brush.Mask"]));
        Assert.Equal("#33141419", Argb(day["Brush.BlockedVeil"]));
    }

    [Fact]
    public void L1_PressFeedback_AndEasingCurve()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        // 按压 scale .96（L1 第 4 项）
        Assert.Equal("0.96", Text(common["Double.PressScale"]));
        // 统一缓出曲线 cubic-bezier(.22,.61,.36,1)（L1 第 9 项）
        Assert.Equal("0.22,0.61", common["KeySpline.EaseOut"].Attribute("ControlPoint1")!.Value);
        Assert.Equal("0.36,1", common["KeySpline.EaseOut"].Attribute("ControlPoint2")!.Value);
        // 过渡时长：120ms 按压 / 180ms 变色 / 150ms 行悬停
        Assert.Equal("0:0:0.12", Text(common["Duration.Press"]));
        Assert.Equal("0:0:0.18", Text(common["Duration.Hover"]));
        Assert.Equal("0:0:0.15", Text(common["Duration.Row"]));
    }

    [Fact]
    public void L1_DialogSpec_420_Radius10()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        Assert.Equal("420", Text(common["Size.DialogWidth"]));
        Assert.Equal("10", Text(common["Radius.Dialog"]));
    }

    [Fact]
    public void ShadowComparison_ProvidesBlueAndPureBlackVariants()
    {
        // 验收反馈（批次 02）：确认框投影需提供三档对比样张——双层蓝黑 / 单层蓝黑 / 单层纯黑对照。
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        Assert.True(common.ContainsKey("Effect.DialogShadowMain"), "缺单层蓝黑主投影");
        Assert.True(common.ContainsKey("Effect.DialogShadowSoft"), "缺双层软投影");
        Assert.True(common.ContainsKey("Effect.DialogShadowBlack"), "缺单层纯黑对照投影");
        Assert.Equal("#000000", Hex(common["Color.ShadowBlack"]));
    }

    [Fact]
    public void DialogDefaultShadow_IsSingleLayerBlue()
    {
        // 验收裁定（2026-10-12，用户真机）：双层投影颜色偏深，默认先用单层蓝黑；
        // 双层保留为备选样张（外观预览可切换）。
        var doc = RepoRoot.LoadXaml("src", "TodoWpf", "Preview", "MainPreviewWindow.xaml");

        var outer = doc.Descendants().First(e => (string?)e.Attribute(xName) == "ShadowLayerOuter");
        Assert.True(outer.Attribute("Effect") is null, "默认应为单层投影：外层不应挂软投影");

        var inner = doc.Descendants().First(e => (string?)e.Attribute(xName) == "ShadowLayerInner");
        Assert.Equal("{StaticResource Effect.DialogShadowMain}",
            inner.Attribute("Effect")?.Value ?? "");
    }

    private static readonly XName xName = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name";

    // ---------- 尺寸与圆角阶梯 ----------

    [Fact]
    public void ButtonLadder_MatchesSpec()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        // 图标按钮 32 / 主按钮 30 / 小按钮 26 / 步进器 28 / 勾选框（＋框） 20
        Assert.Equal("32", Text(common["Size.IconButton"]));
        Assert.Equal("30", Text(common["Size.Button"]));
        Assert.Equal("26", Text(common["Size.SmallButton"]));
        Assert.Equal("28", Text(common["Size.Stepper"]));
        Assert.Equal("20", Text(common["Size.Checkbox"]));
        // 标题栏 42×36 / 返回 34；图标 16/13/11/12
        Assert.Equal("42", Text(common["Size.TitlebarButtonWidth"]));
        Assert.Equal("36", Text(common["Size.TitlebarHeight"]));
        Assert.Equal("34", Text(common["Size.BackButtonWidth"]));
        Assert.Equal("16", Text(common["Size.IconLarge"]));
        Assert.Equal("13", Text(common["Size.IconMedium"]));
        Assert.Equal("11", Text(common["Size.IconSmall"]));
        Assert.Equal("12", Text(common["Size.IconCheck"]));
    }

    [Fact]
    public void RadiusLadder_Is6_8_10_With4ForGrip()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        Assert.Equal("6", Text(common["Radius.Control"]));
        Assert.Equal("8", Text(common["Radius.Row"]));
        Assert.Equal("10", Text(common["Radius.Dialog"]));
        Assert.Equal("4", Text(common["Radius.Grip"]));
    }

    [Fact]
    public void WindowMetrics_MatchPrototype()
    {
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        Assert.Equal("360", Text(common["Size.MainWindowWidth"]));
        Assert.Equal("640", Text(common["Size.MainWindowHeight"]));
        Assert.Equal("310", Text(common["Size.SideWindowWidth"]));
        Assert.Equal("4", Text(common["Size.ClusterGap"]));
        Assert.Equal("38", Text(common["Size.ClockTimeFont"]));
        Assert.Equal("15", Text(common["Size.ClockDateFont"]));
    }

    // ---------- 图标集（单族：原型线性 SVG，统一 24 视箱） ----------

    [Fact]
    public void IconFamily_ContainsAllRequiredGlyphs()
    {
        var icons = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Icons.xaml");

        string[] required =
        [
            "Icon.Check", "Icon.Plus", "Icon.ChevL", "Icon.ChevR", "Icon.X", "Icon.Clock", "Icon.BoltOptical",
            "Icon.Trash", "Icon.Undo", "Icon.Bolt", "Icon.PanelCheck", "Icon.Gear",
            "Icon.Moon", "Icon.Sun", "Icon.Pin", "Icon.TuckLock", "Icon.Grip",
            "Icon.SortAsc", "Icon.SortDesc",
        ];

        foreach (var key in required)
        {
            Assert.True(icons.ContainsKey(key), $"缺少图标 {key}");
            Assert.True(HasGeometry(icons[key]), $"图标 {key} 几何为空");
        }
    }

    private static bool HasGeometry(XElement geometry) =>
        (geometry.Attribute("Figures")?.Value.Length ?? 0) > 0
        || (geometry.Attribute("Rect")?.Value.Length ?? 0) > 0
        || geometry.Elements().Any();

    [Fact]
    public void IconFamily_StrokeWidthsComeFromPrototypeFamily()
    {
        // 单族纪律：线宽只允许原型家族使用的 1.7 / 1.8 / 2 / 3（勾）/ 3.4（品牌勾）/ 2.5（pin）。
        // StrokeThickness 属于 Shape 层（Path 使用处），不属于 Geometry 资源——扫描全部消费方 XAML。
        var allowed = new HashSet<string> { "1.7", "1.8", "2", "3", "3.4", "2.5" };

        string[] consumerFiles =
        [
            "src/TodoWpf/Themes/Controls.xaml",
            "src/TodoWpf/Preview/MainPreviewWindow.xaml",
            "src/TodoWpf/Preview/AcrylicProbeWindow.xaml",
        ];

        foreach (var relative in consumerFiles)
        {
            var path = RepoRoot.File(relative.Split('/'));
            if (!File.Exists(path))
            {
                continue; // 红笔阶段文件尚未创建，由后续用例补上
            }

            foreach (var attr in XDocument.Load(path).Descendants().Attributes()
                         .Where(a => a.Name.LocalName is "StrokeThickness"))
            {
                Assert.True(allowed.Contains(attr.Value), $"{relative} 出现非家族线宽 {attr.Value}");
            }
        }
    }

    // ---------- 禁止裸样式纪律 ----------

    [Fact]
    public void NoBareHexColors_OutsideTokenDictionaries()
    {
        // 设计纪律（design/设计说明.md §三）：取色只允许出现在令牌字典里；
        // 控件样式与窗口 XAML 的所有属性值一律经资源引用取色。
        // （规格陈列层的说明文字里的 hex 是给用户看的标注文本，不属于取色，不在锁定范围。）
        string[] consumerFiles =
        [
            "src/TodoWpf/Themes/Controls.xaml",
            "src/TodoWpf/Themes/ReducedMotion.xaml",
            "src/TodoWpf/Preview/MainPreviewWindow.xaml",
            "src/TodoWpf/Preview/AcrylicProbeWindow.xaml",
        ];

        var hexPattern = new Regex(@"^#[0-9A-Fa-f]{3,8}$");
        foreach (var relative in consumerFiles)
        {
            var path = RepoRoot.File(relative.Split('/'));
            if (!File.Exists(path))
            {
                continue; // 红笔阶段文件尚未创建，由后续用例补上
            }

            var doc = XDocument.Load(path);
            var bare = doc.Descendants()
                .SelectMany(e => e.Attributes())
                .Where(a => a.Name.LocalName != "Key" && hexPattern.IsMatch(a.Value))
                .Select(a => $"{a.Name.LocalName}={a.Value}")
                .ToList();
            Assert.True(bare.Count == 0, $"{relative} 出现裸颜色取值 {string.Join(", ", bare.Take(5))}（应走令牌资源）");
        }
    }

    [Fact]
    public void ClusterWindows_UseTokenizedCardBackground()
    {
        // 回归（批次 03 验收发现）：三张卡片窗必须带令牌化卡面（面板底色 + 玻璃窗沿 + 圆角 10），
        // 否则窗口透明、图标文字直接压在桌面上不可读。曾因漏写卡面 Border 发生一次。
        string[] clusterWindows =
        [
            "src/TodoWpf/Windows/MainTodoWindow.xaml",
            "src/TodoWpf/Windows/QuickWindow.xaml",
            "src/TodoWpf/Windows/DoneWindow.xaml",
        ];

        foreach (var relative in clusterWindows)
        {
            var doc = RepoRoot.LoadXaml(relative.Split('/'));
            var card = doc.Descendants()
                .Where(e => e.Name.LocalName == "Border")
                .FirstOrDefault(e =>
                    e.Attribute("Background")?.Value == "{DynamicResource Brush.WinBg}"
                    || e.Attribute("Background")?.Value == "{DynamicResource Brush.Shell}");

            Assert.True(card is not null, $"{relative} 缺少令牌化背景 Border（Brush.WinBg 或 Brush.Shell）");

            // 新主窗走系统外壳（WindowChrome + DWM 圆角），不再自绘玻璃窗沿与圆角
            if (relative.Contains("MainTodoWindow"))
            {
                continue;
            }

            Assert.Equal("{DynamicResource Brush.GlassEdge}", card!.Attribute("BorderBrush")?.Value ?? "");
        }
    }

    [Fact]
    public void ReducedMotion_OverridesAllDurations_ToZero()
    {
        var reduced = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "ReducedMotion.xaml");
        var common = XamlTokenReader.LoadKeyed("src", "TodoWpf", "Themes", "Tokens.Common.xaml");

        var durationKeys = common
            .Where(kv => kv.Key.StartsWith("Duration."))
            .Select(kv => kv.Key);

        foreach (var key in durationKeys)
        {
            Assert.True(reduced.ContainsKey(key), $"ReducedMotion 缺少 {key} 覆盖");
            Assert.Equal("0:0:0", Text(reduced[key]));
        }
    }

    // ---------- 辅助 ----------

    private static string Hex(XElement color) =>
        "#" + (color.Attribute("Value")?.Value ?? color.Value).TrimStart('#').Trim().ToUpperInvariant();

    private static string Argb(XElement brush) =>
        "#" + brush.Attribute("Color")!.Value.TrimStart('#').ToUpperInvariant();

    private static string Text(XElement element) =>
        (element.Attribute("Value")?.Value ?? element.Value).Trim();
}
