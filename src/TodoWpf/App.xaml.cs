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

    private static Mutex? _instanceMutex;

    private static EventWaitHandle? _wakeEvent;

    private static ResourceDictionary? _dayTokens;
    private static ResourceDictionary? _nightTokens;

    /// <summary>时钟显示（设置页 12/24 制，默认 24 小时制；批次 04 接设置）。</summary>
    public static string TimeFormat => "HH:mm";

    public static bool IsNight { get; private set; }

    /// <summary>中文长日期（时钟用）。实现统一在 Core 的 MainViewService.CnDateText（2026-11-06 去重）。</summary>
    public static string FormatCnDate(DateTime local) =>
        TodoWpf.Core.Services.MainViewService.CnDateText(new DateTimeOffset(local));

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：已开启时再启动只调出现有窗口。
        // 注意：互斥锁必须由进程静态持有到退出——若用 using 声明，OnStartup 返回即释放，
        // 第二实例会照常启动（批次 03 验收发现两实例并跑、同写一个数据文件）。
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                using var existing = EventWaitHandle.OpenExisting(WakeEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 唤醒事件尚不存在（对端启动竞态）：忽略，对端随后会显示自己的窗口
            }

            Shutdown();
            return;
        }

        var wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        _wakeEvent = wake;
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
        merged.Add(Load("Themes/DateField.xaml"));

        if (_dayTokens["Color.Accent"].ToString()?.EndsWith("0A63B8", StringComparison.OrdinalIgnoreCase) == true)
        {
            IsNight = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 托盘图标要显式释放：否则进程退出后图标可能残留在通知区（App.Tray 此前只写不读、
        // TrayIcon.Dispose 从未被调用，2026-11-06 查漏清理）
        Tray?.Dispose();
        Tray = null;

        _wakeEvent?.Dispose();
        if (_instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 未持有（例如启动即退出的第二实例）：忽略
            }

            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        base.OnExit(e);
    }

    private static ResourceDictionary Load(string relative) =>
        new() { Source = new Uri($"pack://application:,,,/{relative}") };
}
