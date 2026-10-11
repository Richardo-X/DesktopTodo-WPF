using System.Windows;
using System.Windows.Media;
using TodoWpf.Preview;

namespace TodoWpf;

/// <summary>
/// 应用装配（批次 02 预览形态）：按序合并设计令牌与控件样式，拉起外观预览双窗。
/// 批次 03 起替换为三窗壳与窗口行为。
/// </summary>
public partial class App : Application
{
    private const double MainPreviewWidth = 470;
    private const double AcrylicProbeWidth = 400;

    private static readonly string[] Weekdays = ["日", "一", "二", "三", "四", "五", "六"];

    private static ResourceDictionary? _dayTokens;
    private static ResourceDictionary? _nightTokens;
    private static bool _isNight;

    /// <summary>时钟显示（设置页 12/24 制，默认 24 小时制；批次 04 接设置）。</summary>
    public static string TimeFormat => "HH:mm";

    public static string FormatCnDate(DateTime local) =>
        $"{local.Year}年{local.Month}月{local.Day}日 星期{Weekdays[(int)local.DayOfWeek]}";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var merged = Resources.MergedDictionaries;
        merged.Add(Load("Themes/Tokens.Common.xaml"));
        _nightTokens = Load("Themes/Tokens.Night.xaml");
        _dayTokens = Load("Themes/Tokens.Day.xaml");
        merged.Add(_nightTokens);
        merged.Add(_dayTokens); // 后合并者胜：默认白天
        if (!SystemParameters.ClientAreaAnimation)
        {
            merged.Add(Load("Themes/ReducedMotion.xaml"));
        }
        merged.Add(Load("Themes/Icons.xaml"));
        merged.Add(Load("Themes/Controls.xaml"));

        var workArea = SystemParameters.WorkArea;
        var mainLeft = Math.Max(workArea.Left, workArea.Right - MainPreviewWidth - 60);

        var main = new MainPreviewWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = mainLeft,
            Top = workArea.Top + 80,
        };
        var acrylic = new AcrylicProbeWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Math.Max(workArea.Left, mainLeft - AcrylicProbeWidth - 20),
            Top = main.Top + 60,
        };

        acrylic.Show();
        main.Show();
    }

    /// <summary>日/夜主题切换：交换两套令牌字典的合并顺序（后合并者胜）。</summary>
    public static void ApplyTheme(bool night)
    {
        if (_dayTokens is null || _nightTokens is null)
        {
            return;
        }

        var app = (App)Current;
        var merged = app.Resources.MergedDictionaries;
        merged.Remove(_dayTokens);
        merged.Remove(_nightTokens);
        merged.Add(night ? _dayTokens : _nightTokens);
        merged.Add(night ? _nightTokens : _dayTokens);
        _isNight = night;
    }

    private static ResourceDictionary Load(string relative) =>
        new() { Source = new Uri($"pack://application:,,,/{relative}") };
}
