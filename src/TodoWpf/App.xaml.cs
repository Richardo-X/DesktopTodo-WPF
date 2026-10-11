using System.Threading;
using System.Windows;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Startup;
using TodoWpf.Windows;

namespace TodoWpf;

/// <summary>
/// 应用装配（批次 03）：单实例 → 数据引导（迁移+补结算）→ 主题字典 → 组件簇协调器 + 托盘。
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Local\DesktopTodo-WPF-SingleInstance";
    private const string WakeEventName = @"Local\DesktopTodo-WPF-Wake";

    private static readonly string[] Weekdays = ["日", "一", "二", "三", "四", "五", "六"];

    private static ResourceDictionary? _dayTokens;
    private static ResourceDictionary? _nightTokens;

    /// <summary>时钟显示（设置页 12/24 制，默认 24 小时制；批次 04 接设置）。</summary>
    public static string TimeFormat => "HH:mm";

    public static bool IsNight { get; private set; }

    public static string FormatCnDate(DateTime local) =>
        $"{local.Year}年{local.Month}月{local.Day}日 星期{Weekdays[(int)local.DayOfWeek]}";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：已开启时再启动只调出现有窗口
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            _ = EventWaitHandle.OpenExisting(WakeEventName);
            EventWaitHandle.OpenExisting(WakeEventName).Set();
            Shutdown();
            return;
        }

        var wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        var wakeThread = new Thread(() =>
        {
            while (wake.WaitOne())
            {
                Dispatcher.BeginInvoke(() => MainCluster?.ShowClusterFromTray());
            }
        })
        {
            IsBackground = true,
        };
        wakeThread.Start();

        MergeThemeDictionaries();

        _ = StartClusterAsync();
    }

    private static async Task StartClusterAsync()
    {
        var io = new SystemFileIo();
        var result = await AppBootstrap.OpenAsync(
            io,
            DataPaths.NewDataFile(),
            DataPaths.LegacyDataFile(),
            DateTimeOffset.Now,
            debounceMs: 100);

        var repository = new JsonFileRepository(DataPaths.NewDataFile(), io, debounceMs: 100);
        var controller = new ClusterController(result.Data, repository);
        MainCluster = controller;
        controller.Start();

        Tray = new TrayIcon(controller);
    }

    /// <summary>当前组件簇（单实例唤醒用）。</summary>
    public static ClusterController? MainCluster { get; private set; }

    public static TrayIcon? Tray { get; private set; }

    /// <summary>日/夜主题切换：交换两套令牌字典的合并顺序（后合并者胜）。</summary>
    public static void ApplyTheme(bool night)
    {
        if (_dayTokens is null || _nightTokens is null)
        {
            return;
        }

        var merged = Current.Resources.MergedDictionaries;
        merged.Remove(_dayTokens);
        merged.Remove(_nightTokens);
        merged.Add(night ? _dayTokens : _nightTokens);
        merged.Add(night ? _nightTokens : _dayTokens);
        IsNight = night;
    }

    private static void MergeThemeDictionaries()
    {
        var merged = Current.Resources.MergedDictionaries;
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

        if (_dayTokens["Color.Accent"].ToString()?.EndsWith("0A63B8", StringComparison.OrdinalIgnoreCase) == true)
        {
            IsNight = false;
        }
    }

    private static ResourceDictionary Load(string relative) =>
        new() { Source = new Uri($"pack://application:,,,/{relative}") };
}
