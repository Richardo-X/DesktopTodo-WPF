using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;
using TodoWpf.Core.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// 组件簇协调器（批次 03）：三窗联动布局、手写整簇拖动＋上边吸附、等比缩放、
/// R1 缩进执行（整组上滑→白条→真实命中滑回）、面板开关、置顶、托盘显隐、多屏回正、记忆落盘。
/// 领域数据为不可变 record：会话内以"替换"方式变更（Mutate）后防抖落盘。
/// 业务按钮保持假数据（批次 04 接线）。
/// </summary>
public sealed class ClusterController : IDisposable
{
    private AppData _data;
    private readonly JsonFileRepository _repository;

    private readonly MainTodoWindow _main;
    private readonly QuickWindow _quick;
    private readonly TuckBarWindow _tuckBar;
    private readonly BubbleWindow _bubble;
    private readonly TuckStateMachine _tuck = new();

    private readonly DispatcherTimer _tuckTick;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _hoverTimer;
    private TextBlock? _hoverTextEl;
    private FrameworkElement? _hoverRow;

    private WinPoint _anchor;

    /// <summary>整簇拖动开始时主窗所在屏（跨屏白板检测基准）。</summary>
    private IntPtr _dragStartMonitor;

    /// <summary>整簇拖动开始时的布局档位（被屏缘挤压的检测基准：低于它即正在被挤）。</summary>
    private double _dragStartScale;

    /// <summary>本次拖动已触发跨屏白板（只开不关，恢复统走 EndDrag）。</summary>
    private bool _dragVeiled;

    /// <summary>启动时读到的 data.json 原始格式版本（在任何落盘之前抓取，供兼容检测）。</summary>
    private int? _fileSchemaVersion;

    /// <summary>C4 更新服务。</summary>
    private readonly UpdateService _updates = new();

    /// <summary>启动/手动检查发现的新版本（null = 无）；标题栏提示与更新窗都由它驱动。</summary>
    private UpdateService.UpdateAvailable? _pendingUpdate;

    /// <summary>模板窗上一帧的 DPI 横向缩放（拖动中侦测跨屏用，变了才补一次内容缩放）。</summary>
    private double _lastQuickDpiX = 1.0;

    /// <summary>白条最近一次的目标（主窗空间 DIP）：白条自身 DPI 变化时按它重摆。</summary>
    private double _lastBarCenterDipX;
    private double _lastBarTopDip;
    private bool _barHasTarget;
    /// <summary>用户档位（存档值 ClusterScale）。**不是**实际用于布局的值——
    /// 布局一律走 EffectiveScale()：按当前屏工作区钳制后使用。
    /// 这样"拖到小屏被钳小、拖回大屏自动恢复用户档位"，且存档不被钳制值污染
    /// （用户 2026-11-04 实测：两屏来回拖窗口会一路变小——根因就是原先把钳制值写回了这个字段）。</summary>
    private double _storedScale = 1;

    /// <summary>
    /// 实际布局用档位 = 用户档位按**当前屏工作区**钳制（最小 1/4 屏高、最大 2/3 屏高、绝对下限 192 DIP）。
    /// 只钳「生效值」，**绝不写回** `_storedScale`：
    ///   · 拖到小屏被临时压到上限 → **拖动角标永远可达**（2026-11-04 用户实测：不钳制时窗高超过屏高、角标跑到屏外抓不到）；
    ///   · 拖回大屏自动恢复用户自己的档位；
    ///   · 不会"越拖越小"（当年的根因是把钳制值写回了存档，而今已拆分）。
    /// 说明：工作区按 DIP 读取，而 DIP 已被系统缩放归一化（175% 屏上 1 DIP = 1.75 物理像素）
    /// → 钳制天然包含缩放影响，**不需要任何 DPI 特判**（用户 2026-11-04 明确）。
    /// </summary>
    private double LayoutScale(WinRect area, WinPoint anchor) =>
        PanelLayout.ClampScale(_storedScale, area, anchor);
    private bool _clusterHidden;

    // 拖动/缩放进行中状态
    private bool _dragging;
    private bool _resizeLayoutQueued;
    private bool _resizing;
    private Point _resizeStartMouse;
    private double _resizeStartMainWidth;

    /// <summary>
    /// 方案 D（仅快捷窗）：拖动期间它的窗口矩形冻结不动，只让内容在渲染层缩放预览，松手一次落位。
    /// 冻结值取"本次拖动可能达到的最大档"，保证放大时内容不会被截。
    /// 这样拖动全程不改快捷窗的窗口矩形 ⇒ 合成器没有"新矩形+旧表面"的机会 ⇒ 缝隙/残影不再出现。
    /// </summary>
    private WinPoint _dragStartAnchor;

    /// <summary>拖动起点：锚点与鼠标都用**物理像素**记录（跨屏时主窗 DPI 会变，
    /// DIP 记录的起点在换屏后含义就变了 → 窗口跳变/跟不上鼠标，2026-11-09 修）。</summary>
    private WinPoint _dragStartAnchorPhysical;

    private Point _dragStartMousePhysical;

    private WinRect _dragWorkArea;

    public ClusterController(AppData data, JsonFileRepository repository)
    {
        _data = data;
        _repository = repository;

        _main = new MainTodoWindow();
        _quick = new QuickWindow();
        _tuckBar = new TuckBarWindow();
        _bubble = new BubbleWindow();

        // 悬停气泡（批次05①）：300ms 延迟（用户 2026-11-03 裁定；原型 500ms）
        _hoverTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _hoverTimer.Tick += (_, _) => ShowHoverBubble();

        // 不进任务栏/Alt+Tab（需求 §2.2）：ShowInTaskbar=false 只管任务栏，Alt+Tab 需 WS_EX_TOOLWINDOW
        foreach (var window in (Window[])[_main, _quick, _tuckBar])
        {
            ToolWindowChrome.Apply(window);
        }


        _storedScale = _data.UiState.ClusterScale;
        _anchor = ResolveAnchor();

        _main.TitlebarPressed += OnTitlebarPressed;
        _quick.TitlebarPressed += OnTitlebarPressed;
        _quick.DpiChanged += OnQuickDpiChanged;
        _tuckBar.DpiChanged += OnTuckBarDpiChanged;
        // Show 落位后立即物理纠正：WPF Show 用"Show 那一刻才更新的白条 DPI"换算，
        // 隐藏期间停滞的旧 DPI 会把白条摆到屏外（2026-11-07 竖屏实测：悬在屏上方 941px）。
        _tuckBar.IsVisibleChanged += (_, _) =>
        {
            if (_tuckBar.IsVisible)
            {
                NormalizeTuckBarPhysical();
            }
        };
        _main.ScaleGripHandle.MouseLeftButtonDown += ScaleGrip_MouseDown;
        _tuckBar.BarHit += (_, _) => OnPointerEnter();
        // 白条也参与"指针在内"判定：从白条直接移到遮挡窗时，应用窗从未获得悬停、
        // 不会发 MouseLeave——若白条自己不发离开，指针在内将永久卡死（验收 4.2 实证）。
        _tuckBar.MouseLeave += (_, _) =>
        {
            _tuck.SetPointerInside(false);
            _tuck.NotifyPointerLeft(DateTimeOffset.Now);
        };

        // 指针进出 → 状态机（窗口级，被遮挡时系统不派发 = 真实命中语义）
        foreach (var window in (Window[])[_main, _quick])
        {
            window.MouseLeave += (_, _) => _tuck.NotifyPointerLeft(DateTimeOffset.Now);
            window.MouseEnter += (_, _) => OnPointerEnter();
        }

        // Alt+F4 = 强制退出整个应用（退出前数据落盘；×=托盘隐藏用 Hide，不触发 Closing）
        foreach (var window in (Window[])[_main, _quick, _tuckBar])
        {
            window.Closing += (_, e) =>
            {
                e.Cancel = true;
                _ = ExitAsync();
            };
        }

        _tuckTick = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _tuckTick.Tick += (_, _) => TuckTick();
        _tuckTick.Start();

        _clock = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public MainTodoWindow Main => _main;

    public QuickWindow Quick => _quick;

    /// <summary>C4 更新服务（设置页「检查更新」用）。</summary>
    public UpdateService Updates => _updates;

    /// <summary>初始化：应用记忆、接互锁、显示窗口。</summary>
    public void Start()
    {
        // C4 兼容检测：**必须在任何落盘之前**读文件里的原始格式版本——
        // 启动流程本身会立刻把 data.json 覆写成当前版本（v=当前），
        // 晚读就只能读到刚写下的 v，永远检测不出"数据来自更高版本"（2026-11-09 实测发现）。
        _fileSchemaVersion = ReadFileSchemaVersionOnce();

        // 批次04：旧侧窗不再显示。决议（2026-11-03）：重启回到关闭前的尺寸与位置——
        // 倍数取存档 ClusterScale（每次布局落盘），按当前屏钳制到 1/4~2/3；位置由 Pos 恢复。
        _data = _data with
        {
            UiState = _data.UiState with { QuickVisible = false, DoneVisible = false },
        };
        var startupArea = WorkArea();
        // 启动时位置也要钳回可见范围：存档位置来自另一块屏/更高分辨率时，
        // 窗口会整个落在屏外（本机 1024×768 实测：存档 x=1024 → 窗口 1024..1132 全在屏外，
        // 用户看到"窗口不见了"且无法点中）。需求 §2.1：分辨率变化/拔显示器 → 回可见范围。
        // 必须传真实主窗尺寸：传零尺寸矩形时钳制会允许贴到工作区右缘（窗口仍整个在屏外）
        var clampedAnchor = ScreenClamp.ClampToWorkArea(
            new WinRect(
                _anchor.X, _anchor.Y,
                PanelLayout.MainWidth * LayoutScale(startupArea, _anchor),
                PanelLayout.MainBaseHeight * LayoutScale(startupArea, _anchor)),
            startupArea);
        _anchor = new WinPoint(clampedAnchor.X, clampedAnchor.Y);

        // 尺寸规则（用户 2026-11-04 定稿）：默认档位按屏高 1/3 只在首次安装给；
        // 之后每次布局都按当前屏钳制**生效值**（1/4~2/3 屏高），但**不写回**用户档位。
        // 存档值只做绝对范围保护，避免脏数据把窗口搞成 0 或无穷大。
        _storedScale = Math.Clamp(_storedScale, 0.15, PanelLayout.MaxScale);

        ApplyThemeState();
        _main.Controller = this;
        _quick.Controller = this;
        _tuckBar.Controller = this;

        // 焦点描边仅键盘导航显示（L1 明细；鼠标点击不留蓝框）
        foreach (var window in (Window[])[_main, _quick])
        {
            FocusVisualScope.Attach(window);
        }

        _tuck.SetLocked(_data.UiState.TuckLock);
        _main.SetTuckLock(_data.UiState.TuckLock);
        _main.SetTopMost(_data.UiState.TopMost);

        ApplyTopMost(_data.UiState.TopMost);

        // C4：版本标记同步写（必须在 ApplyLayout 落盘**之前**——后台异步写会与它竞态，
        // 旧档后发先至把标记盖掉，2026-11-08 实测）；降级检测 = 当前版本低于上次运行版本。
        var currentVersion = UpdateService.CurrentVersion();
        var lastRunVersion = _data.UiState.LastRunVersion;
        var isDowngraded = lastRunVersion is not null && AppVersion.Compare(currentVersion, lastRunVersion) < 0;
        Mutate(d => d with { UiState = d.UiState with { LastRunVersion = currentVersion } });
        if (isDowngraded)
        {
            ShowToast($"当前版本 {currentVersion} 低于上次运行的 {lastRunVersion}，数据可能不完全兼容，建议先到历史窗导出备份");
        }

        RefreshAllViews();
        UpdateClock();
        ApplyLayout();
        _bubble.Show(); // 透明常驻：以 Opacity=0 待命，规避显示时闪现

        // C4：数据格式兼容检测（不兼容 → 提示 + 打开数据目录，用户 2026-11-09 裁定）
        CheckDataCompatibilityOnce();

        // C4：启动检查（后台，失败静默——定稿口径）。注意：Velopack 检查要求"已安装"，
        // 开发副本（bin 下直接跑）抛 NotInstalledException → 在这里被静默跳过，属预期。
        _ = Task.Run(async () =>
        {
            try
            {
                if (_data.UiState.UpdateNotifyEnabled)
                {
                    var available = await _updates.CheckAsync();
                    if (available is not null)
                    {
                        await _main.Dispatcher.InvokeAsync(() =>
                        {
                            _pendingUpdate = available;
                            RefreshUpdateBadge();
                        });
                    }
                }
            }
            catch
            {
                // 后台检查失败静默（含开发副本的 NotInstalled）；手动检查才明确报错
            }
        });

        // 显隐交给 ApplyLayout（按存档的面板开关决定）——此前三个窗口无条件 Show()，
        // 会把"已关闭"的面板以 WPF 默认尺寸（1536×844）显示出来（Viewbox 又把内容放大填满）= 启动巨窗 bug。
        _main.SetPanelToggles(_data.UiState.QuickVisible, _data.UiState.DoneVisible);

    }

    /// <summary>
    /// 三窗真实数据刷新（批次04）：待办行/计数/空态、已完成行（含排序与标题计数）、模板行与标题计数。
    /// 数据变化的入口统一走这里（后续 CRUD/批量/结算都只改 _data 再调它），避免多处各自维护 UI。
    /// </summary>
    public void RefreshAllViews()
    {
        EndHover(); // 行整表重建，悬停目标已失效
        var todayKey = LocalDate.Key(DateTimeOffset.Now);
        var mainBatch = _batch.IsActive && _batch.Target == "todos";
        var tplBatch = _batch.IsActive && _batch.Target == "templates";
        _main.SetRows(MainViewService.TodoRows(_data, todayKey, mainBatch ? _batch : null));
        _main.SetCount(_data.Todos.Count, _data.Settings.MaxItems);
        var doneRows = MainViewService.DoneRows(_data, _data.Settings.DoneSort);
        _main.SetDoneRows(doneRows);
        _main.SetDoneSortState(_data.Settings.DoneSort == "desc");
        _quick.SetTemplateRows(
            MainViewService.TemplateRows(_data, tplBatch ? _batch : null),
            Core.Models.TemplateLimit.Max, tplBatch);
    }

    // ---------- 布局 ----------

    private WinPoint ResolveAnchor()
    {
        var ui = _data.UiState;
        if (ui.Pos is { } pos)
        {
            // 新格式：物理像素，按"位置所在屏"的 DPI 折算回布局空间（与存档时在哪块屏无关）。
            // 旧格式（posIsRawPx=false）：存档时所在屏的 DIP，保持旧语义——重新停靠一次后升级为新格式。
            return ui.PosIsRawPx
                ? MonitorLayout.RawPointToDips(pos.X, pos.Y)
                : new WinPoint(pos.X, pos.Y);
        }

        var workArea = WorkArea();
        var estimate = new WinPoint(workArea.X, workArea.Y + 80);
        var scale = PanelLayout.ClampScale(
            PanelLayout.DefaultMainHeight(workArea.Height) / PanelLayout.MainBaseHeight,
            workArea,
            estimate);
        _storedScale = scale;
        return PanelLayout.FirstLaunchAnchor(workArea, scale, ui.QuickVisible);
    }

    /// <summary>窗口所在显示器的工作区（DIP）。旧实现用 SystemParameters.WorkArea 只取主屏，
    /// 拖到副屏/竖屏后仍按主屏高度钳制，表现为"屏幕中间一条界线"。</summary>
    private WinRect WorkArea() => MonitorLayout.WorkAreaDips(_main);

    /// <summary>
    /// 锚点对齐到整数物理像素后再派生三窗几何：
    /// 主窗左缘=锚点、快捷窗右缘=锚点-缝隙、已完成窗左缘=锚点+主宽+缝隙 都从同一取整值推出，
    /// 因此"钉住的边"与"缝隙宽度"逐帧都是稳定的整数像素（此前三条边各自取整会有 ±1px 波动）。
    /// </summary>
    private WinPoint SnappedAnchor()
    {
        var dpi = VisualTreeHelper.GetDpi(_main);
        return new WinPoint(
            Math.Round(_anchor.X * dpi.DpiScaleX) / dpi.DpiScaleX,
            Math.Round(_anchor.Y * dpi.DpiScaleY) / dpi.DpiScaleY);
    }

    /// <summary>
    /// 把设计尺寸按"窗口实际宽度"（已按物理像素取整）折算成内容缩放比：
    /// 与 WindowPlacement 用同一套取整，保证内容布局尺寸＝客户区尺寸。
    /// </summary>
    private double NormalizedScale(double rectWidth, double designWidth)
    {
        var dpi = VisualTreeHelper.GetDpi(_main);
        var physical = Math.Max(1, (int)Math.Round(rectWidth * dpi.DpiScaleX));
        return physical / dpi.DpiScaleX / designWidth;
    }

    /// <summary>
    /// 模板窗内容缩放：窗口物理宽（<b>按模板窗所在屏 DPI</b> 取整，ele 口径=保持物理大小）÷
    /// 模板窗自身 DPI = WPF 眼里的 DIP 客户宽 ÷ 设计宽。
    /// 同屏时模板窗 DPI == 主窗 DPI，结果与旧口径一致；
    /// 跨屏后物理尺寸按所在屏放大、WPF 的 DIP 客户宽回到布局宽 → 内容比例与主屏一致、像素更密。
    /// </summary>
    private void UpdateQuickContentScale(double frameWidth)
    {
        var quickDpi = VisualTreeHelper.GetDpi(_quick);
        _lastQuickDpiX = quickDpi.DpiScaleX;
        var physical = Math.Max(1, (int)Math.Round(frameWidth * quickDpi.DpiScaleX));
        _quick.SetContentScale(physical / quickDpi.DpiScaleX / PanelLayout.TemplateWidth);
    }

    private void OnQuickDpiChanged(object sender, System.Windows.DpiChangedEventArgs e)
    {
        // 模板窗跨屏（或松手瞬间才跨屏）时 WPF 会按系统建议矩形改写它的位置/尺寸（×新屏缩放），
        // 把主窗空间的精确落位打掉。拖动中不在这里重摆——下一帧 MouseMove 自会重摆（此处重摆反而跟逐帧路径打架）；
        // 非拖动态没有后续帧，必须立即按主窗空间重摆纠正（典型：松手瞬间才完成跨屏）。
        if (!_dragging)
        {
            ApplyLayout(saveState: false);
        }
    }

    /// <summary>白条设计尺寸（与 TuckBarWindow.xaml 的 Width/Height 一致）。</summary>
    private const double TuckBarDesignWidth = 160;

    private const double TuckBarDesignHeight = 6;

    /// <summary>
    /// 白条 WPF 属性定位（辅助）：把目标换算成物理像素，再按白条**当前** DPI 反推它的 WPF DIP。
    /// 注意这只是一级近似——白条隐藏期间自身 DPI 停在旧屏（1.0），Show 落到 175% 屏才更新，
    /// 两级换算的分子分母会不同代 → 位置飞出屏外（2026-11-07 竖屏实测：悬在屏上方 941px）。
    /// 因此真正的落位以 <see cref="NormalizeTuckBarPhysical"/> 的物理直设为准（IsVisibleChanged/DpiChanged 触发）。
    /// </summary>
    private void PlaceTuckBar(double mainCenterDipX, double topDipY)
    {
        _lastBarCenterDipX = mainCenterDipX;
        _lastBarTopDip = topDipY;
        _barHasTarget = true;
        var mainDpi = VisualTreeHelper.GetDpi(_main);
        var barDpi = VisualTreeHelper.GetDpi(_tuckBar);
        var physX = mainCenterDipX * mainDpi.DpiScaleX - _tuckBar.Width * barDpi.DpiScaleX / 2;
        var physY = topDipY * mainDpi.DpiScaleY;
        _tuckBar.Left = physX / barDpi.DpiScaleX;
        _tuckBar.Top = physY / barDpi.DpiScaleY;
    }

    /// <summary>
    /// 白条物理像素直设（最终口径）：内容是无装饰的 3px 细条，渲染密度无关紧要——
    /// 位置与尺寸直接按物理像素 SetWindowPos（尺寸按主窗 DPI = 整簇同一视觉比例），
    /// 完全绕开"白条自身 DPI 何时更新"的时序问题。
    /// </summary>
    private void NormalizeTuckBarPhysical()
    {
        if (!_barHasTarget)
        {
            return;
        }

        var hwnd = new System.Windows.Interop.WindowInteropHelper(_tuckBar).Handle;
        if (hwnd == System.IntPtr.Zero)
        {
            return;
        }

        var mainDpi = VisualTreeHelper.GetDpi(_main);
        int cx = (int)System.Math.Round(TuckBarDesignWidth * mainDpi.DpiScaleX);
        int cy = (int)System.Math.Round(TuckBarDesignHeight * mainDpi.DpiScaleY);
        int x = (int)System.Math.Round(_lastBarCenterDipX * mainDpi.DpiScaleX) - cx / 2;
        int y = (int)System.Math.Round(_lastBarTopDip * mainDpi.DpiScaleY);
        MonitorLayout.MovePhysicalSized(hwnd, x, y, cx, cy);
    }

    private void OnTuckBarDpiChanged(object sender, System.Windows.DpiChangedEventArgs e)
    {
        // 白条跨屏时 WPF 会按系统建议矩形改写它（实测 Width 被按倍率连翻：160→280 DIP、
        // 渲染 490 物理宽），把落位和尺寸一起打掉——先复位设计尺寸，再按物理像素直设归位。
        if (_barHasTarget)
        {
            _tuckBar.Width = TuckBarDesignWidth;
            _tuckBar.Height = TuckBarDesignHeight;
            PlaceTuckBar(_lastBarCenterDipX, _lastBarTopDip);
            NormalizeTuckBarPhysical();
        }
    }

    /// <summary>模板窗设计高度：标题 40 + 动作行 40 + 列表上下 12 + 行 × 44（ele 行高），夹在 160 与 448 之间。</summary>
    private double TemplateWindowHeight()
    {
        var rows = _data.Templates.Count;
        return Math.Clamp(rows * 44 + 92, 100, 448);
    }

    /// <summary>按锚点+缩放+开关重排三窗（隐藏窗同时就位但不出场）。</summary>
    public void ApplyLayout(bool saveState = true)
    {
        var ui = _data.UiState;
        var workArea = WorkArea();
        var scale = LayoutScale(workArea, SnappedAnchor());
        var frame = PanelLayout.Arrange(SnappedAnchor(), scale, ui.QuickVisible);

        // 模板窗用固定画布（零矩形变更）；主窗逐帧摆位。
        // （2026-11-07 删 KeepTemplateOnMainScreen 贴边钳制：它掩盖的是"各窗按自身 DPI 换算"的错误；
        //   换算统一到主窗基准后（SetBoundsBatch refScale），模板窗跨屏也能精确落位，钳制不再需要。）
        var tplRect = frame.Template.Rect with { Height = TemplateWindowHeight() * scale };
        PlaceBatch(
            (frame.Main.Rect, _main, true),
            (tplRect, _quick, frame.Template.Visible));

        // 内容缩放比不从 _scale 直接取，而是从"窗口矩形取整后的实际尺寸"反推：
        // 窗口宽度要落到整数物理像素（PlaceBatch 里 round），若内容仍按原始 _scale 排版，
        // 两者会差零点几像素——内容比窗口宽 ⇒ 右对齐的卡面被裁掉一条；比窗口窄 ⇒
        // 卡面右缘缩进窗内，肉眼就是"两窗缝隙在缩放中变宽/变窄"（用户视频慢放实证：缝隙 13→17→13）。
        // 反推后 内容布局尺寸 ≡ 窗口客户区尺寸，卡面边缘逐帧与窗口边缘重合。
        _main.SetContentScale(NormalizedScale(frame.Main.Rect.Width, PanelLayout.MainWidth));
        UpdateQuickContentScale(frame.Template.Rect.Width);
        _quick.SetContentHeight(TemplateWindowHeight());

        // 窗口矩形已在本帧落位；这里把内容布局也同步推完，避免"内容晚窗口一帧"
        // （内容若晚一帧，右对齐的快捷窗右缘与两窗缝隙就会被看见在动）。
        if (_resizing)
        {
            _main.UpdateLayout();
            _quick.UpdateLayout();
        }

        // 白条定位（用户 2026-11-04 裁定 D4）：**以待办主窗水平居中**、贴屏顶（不再按整组宽度居中）
        PlaceTuckBar(frame.Main.Rect.X + frame.Main.Rect.Width / 2, workArea.Y);

        if (saveState)
        {
            // 锚点改存**物理像素**（2026-11-07 恢复跨屏错位修复）：DIP 坐标绑定"存档时所在的屏"，
            // 重启后被按当次主窗 DPI 解释 → 2K 存档被 ×1.75 摆到 4K 屏。
            // 物理像素与屏无关；加载时按位置所在屏折算回布局空间（见 ResolveAnchor）。
            var dpi = VisualTreeHelper.GetDpi(_main);
            Mutate(d => d with
            {
                UiState = d.UiState with
                {
                    Pos = new Position { X = _anchor.X * dpi.DpiScaleX, Y = _anchor.Y * dpi.DpiScaleY },
                    PosIsRawPx = true,
                    ClusterScale = _storedScale,
                },
            });
        }
    }

    /// <summary>拖动快路径：只同步可见窗的坐标（不重设宽高、不切显隐、不落盘）。</summary>
    private void MoveCluster()
    {
        var ui = _data.UiState;
        var dragArea = WorkArea();
        var scale = LayoutScale(dragArea, SnappedAnchor());
        var frame = PanelLayout.Arrange(SnappedAnchor(), scale, ui.QuickVisible);

        // 拖动跨屏瞬间：模板窗自己的 DPI 会变（WPF 按它渲染内容密度）→ 内容缩放补一帧，
        // 否则模板窗在另一块屏上按旧密度渲染，内容裁切/错乱，直到松手才恢复。
        var quickDpiX = VisualTreeHelper.GetDpi(_quick).DpiScaleX;
        if (quickDpiX != _lastQuickDpiX)
        {
            UpdateQuickContentScale(frame.Template.Rect.Width);
        }

        // 快捷窗在移动路径上也必须用**画布矩形**（与 ApplyLayout 一致）：
        // 它是个固定设计尺寸 + 渲染层缩放的窗，卡面比目标档宽（310 设计宽）。
        // 这里若用目标矩形（缩放后的实际宽度）摆它，卡片就被塞进更窄的窗口里 ⇒ 移动过程中被裁；
        // 松手走 ApplyLayout 又换回画布矩形 ⇒ 于是"移动时被裁、松手恢复"（用户 2026-10-13 实测）。
        // 同 ApplyLayout：不再钳制，跨屏拖动中模板窗按布局矩形精确跟随；
        // 跨屏时尺寸按模板窗所在屏（保持物理大小，ele 口径），位置仍钉主窗空间。
        var tplRect = frame.Template.Rect with { Height = TemplateWindowHeight() * scale };
        PlaceBatch(
            (frame.Main.Rect, _main, true),
            (tplRect, _quick, frame.Template.Visible));

        if (_tuckBar.IsVisible)
        {
            PlaceTuckBar(frame.Main.Rect.X + frame.Main.Rect.Width / 2, dragArea.Y);
        }
    }

    /// <summary>
    /// 三窗几何同批提交（DeferWindowPos）：避免逐窗提交被系统在两窗之间插入呈现，
    /// 造成"同一帧里三窗位置/尺寸不同步"；显隐状态另行处理（仅变化时切换）。
    /// </summary>
    private void PlaceBatch(params (WinRect Rect, Window Window, bool Visible)[] panels)
    {
        var mainDpi = VisualTreeHelper.GetDpi(_main);
        var quickDpi = VisualTreeHelper.GetDpi(_quick);
        var atomic = new List<PlacementTarget>(panels.Length);
        var toShow = new List<Window>(panels.Length);
        var toHide = new List<Window>(panels.Length);

        foreach (var (rect, window, visible) in panels)
        {
            if (visible)
            {
                if (window.IsVisible)
                {
                    // 已显示：走原子批量。逐帧路径上**不**写 WPF 的 Left/Top/Width/Height——
                    // 那会额外发出 4 次非原子 SetWindowPos（先位置后尺寸），把"新位置 + 旧尺寸"
                    // 的中间态暴露给 DWM 合成器（叠层窗口按新矩形贴旧表面 ⇒ 右缘被拉左的假象）。
                    // WPF 自己会跟随 WM_SIZE 同步这些属性值。
                    atomic.Add(TargetFor(window, rect, mainDpi, quickDpi));
                }
                else
                {
                    // 首次显示：先把 WPF 属性同步到目标几何，Show() 由 WPF 按其属性一次落位。
                    // 若先 Show 再摆位，会以默认尺寸（1536×844）闪一帧——即"启动闪一下"。
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = rect.X;
                    window.Top = rect.Y;
                    window.Width = rect.Width;
                    window.Height = rect.Height;
                    toShow.Add(window);
                }
            }
            else if (window.IsVisible)
            {
                toHide.Add(window);
            }
        }

        WindowPlacement.SetBoundsBatch(atomic);

        foreach (var window in toShow)
        {
            window.Show();
        }

        foreach (var window in toHide)
        {
            window.Hide();
        }
    }

    /// <summary>
    /// 单窗换算基准：位置恒按主窗 DPI（簇内相邻关系不变）；
    /// 模板窗**跨屏**时尺寸按它自己所在屏的 DPI——保持物理大小（ele 口径，2026-11-07 用户裁定）：
    /// 内容同布局、像素更密，代价是与主窗的比例短暂错位（主窗过界重新布局后恢复一致）。
    /// 同屏时两 DPI 相等，退化为既有的全主窗基准（逐像素等价，零回归）。
    /// </summary>
    private PlacementTarget TargetFor(Window window, WinRect rect, DpiScale mainDpi, DpiScale quickDpi)
    {
        var ownSize = ReferenceEquals(window, _quick)
            && (Math.Abs(quickDpi.DpiScaleX - mainDpi.DpiScaleX) > 0.01
                || Math.Abs(quickDpi.DpiScaleY - mainDpi.DpiScaleY) > 0.01);
        return ownSize
            ? new PlacementTarget(window, rect, mainDpi.DpiScaleX, mainDpi.DpiScaleY, quickDpi.DpiScaleX, quickDpi.DpiScaleY)
            : new PlacementTarget(window, rect, mainDpi.DpiScaleX, mainDpi.DpiScaleY);
    }

    // ---------- 整簇拖动（手写：MouseCapture 下同步三窗，DragMove 无法联动） ----------

    private void OnTitlebarPressed(object? sender, MouseButtonEventArgs e)
    {
        if (_dragging || sender is not Window window)
        {
            return;
        }

        EndHover(); // 整组拖动时气泡悬空已失锚
        _dragging = true;
        _dragStartMonitor = MonitorLayout.MonitorOf(_main);
        _dragVeiled = false;
        _dragStartScale = LayoutScale(WorkArea(), _anchor);
        _dragWorkArea = WorkArea();
        _dragStartAnchor = _anchor;
        var startDpi = VisualTreeHelper.GetDpi(_main);
        _dragStartAnchorPhysical = new WinPoint(
            _anchor.X * startDpi.DpiScaleX, _anchor.Y * startDpi.DpiScaleY);
        _dragStartMousePhysical = window.PointToScreen(e.GetPosition(window));
        _tuck.SetDragging(true);
        _ = window.CaptureMouse();
        window.MouseMove += DragMove_MouseMove;
        window.MouseUp += DragEnd_MouseUp;
        window.LostMouseCapture += DragLostCapture;
    }

    private void DragMove_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Window window || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // 拖动位移在**物理像素**里累加，再按主窗**当前** DPI 折算回 DIP：
        // 跨屏瞬间主窗 DPI 会变（125% ↔ 175%），若用 DIP 记录的起点，换屏后同一数值代表的位置就变了
        // → 窗口跳变或跟不上鼠标（2026-11-09 修）。物理像素与屏无关，天然跨屏连续。
        var mousePhys = window.PointToScreen(e.GetPosition(window));
        var dpi = VisualTreeHelper.GetDpi(window);
        var anchorPhys = new WinPoint(
            _dragStartAnchorPhysical.X + mousePhys.X - _dragStartMousePhysical.X,
            _dragStartAnchorPhysical.Y + mousePhys.Y - _dragStartMousePhysical.Y);
        var anchor = new WinPoint(anchorPhys.X / dpi.DpiScaleX, anchorPhys.Y / dpi.DpiScaleY);

        // 拖动钳制（用户 2026-11-04 裁定 D2，照 ele clampAnchorForEdges）：
        // 主窗至少 90 DIP 可见（左右各留 90）、顶边不越工作区顶（可贴顶）、底部至少 50 DIP 可见。
        // **只保留上边缘吸附**（裁定 D1：左/右/下边缘贴边在 ele 与需求文档「暂不做」清单里，
        // 本批 ④-3 我越界加的四缘磁吸已移除——它也正是"拖不过屏幕"的根因：磁吸把整簇钳在屏内）。
        // 按**主窗所在屏**钳制（照 ele clampAnchorForEdges：主窗 ≥90px 可见、顶不越界、底部 ≥50px 可见）。
        // 注：主窗可以一直往右拖到只剩 90px 可见 → 其中心越过边界后 MonitorFromWindow 切到新屏 →
        // 继续往新屏移动 ⇒ **跨屏可移动**。
        // （2026-11-04 曾改为"按鼠标所在屏 + 整簇不跨屏"，结果主窗永远跨不过边界、完全无法跨屏，已回退。）
        var area = WorkArea();
        var dragScale = LayoutScale(area, anchor);
        var mainWidth = PanelLayout.MainWidth * dragScale;

        // 拖动钳制（用户 2026-11-04 裁定 D2）：默认保持"主窗 ≥90 DIP 可见、顶不越工作区顶、底部 ≥50 DIP 可见"。
        // **但该侧存在相邻屏时不钳制**（2026-11-09 修）：否则窗口最多只露 90 DIP，
        // 而换屏要求窗口大部分落到另一块屏——主屏 125% 时最小档宽 162、一半仅 81 < 90，
        // 中心永远差一点过不去，表现为"缩到最小也跨不了屏"（100% 时 101.25 > 90 才侥幸能过）。
        // 放宽后窗口跟随鼠标越过交界，切屏后按新屏工作区重新钳制。
        // 顶边仍钳制：贴顶吸附语义依赖"锚点不高于工作区顶"。
        var nb = MonitorLayout.NeighborEdges(_main);
        var minX = nb.Left ? double.NegativeInfinity : area.X + PanelLayout.DragVisibleWidth - mainWidth;
        var maxX = nb.Right ? double.PositiveInfinity : area.X + area.Width - PanelLayout.DragVisibleWidth;
        var maxY = nb.Bottom ? double.PositiveInfinity : area.Y + area.Height - PanelLayout.DragVisibleHeight;
        anchor = new WinPoint(
            Math.Min(Math.Max(anchor.X, minX), Math.Max(minX, maxX)),
            Math.Min(Math.Max(anchor.Y, area.Y), Math.Max(area.Y, maxY)));
        _dragWorkArea = area;

        _anchor = anchor;
        MoveCluster(); // 快路径：只改坐标，不重排、不落盘（抖动治理）

        // 跨屏白板（2026-11-07 用户裁定）：**只属于跨屏过交界线**，同屏边缘交互不触发。
        // 白板只开不关，恢复统走 EndDrag。两类触发：
        // ① 整簇任一窗越出起点屏边界（真跨屏）；
        // ② 被屏缘挤压缩小，且**被推的方向之外确实存在另一块屏**（交界线方向判定）——
        //    挤压本身可能发生在任何屏缘（右/下），只有推向"存在相邻屏"的方向才算跨屏过程；
        //    单屏右缘/贴底等普通边缘交互按之前行为（不白板，用户 2026-11-07 裁定）。
        if (!_dragVeiled)
        {
            var mainDpi = VisualTreeHelper.GetDpi(_main);
            var byWidth = (area.Right - anchor.X - PanelLayout.ViewportMargin) / PanelLayout.MainWidth;
            var byHeight = PanelLayout.MaxMainHeight(area.Height, anchor.Y) / PanelLayout.MainBaseHeight;
            var squeezed = (byWidth < _dragStartScale - 0.001
                                && MonitorLayout.HasMonitorBeyondRight(
                                    _dragStartMonitor, (int)(anchor.Y * mainDpi.DpiScaleY)))
                           || (byHeight < _dragStartScale - 0.001
                                && MonitorLayout.HasMonitorBeyondBottom(
                                    _dragStartMonitor, (int)(anchor.X * mainDpi.DpiScaleX)));
            if (squeezed || MonitorLayout.IsCrossingMonitor(
                    _dragStartMonitor,
                    new System.Windows.Interop.WindowInteropHelper(_main).Handle,
                    new System.Windows.Interop.WindowInteropHelper(_quick).Handle))
            {
                _dragVeiled = true;
                _main.SetDragVeil(true);
                _quick.SetDragVeil(true);
            }
        }
    }

    private void DragEnd_MouseUp(object sender, MouseButtonEventArgs e) => EndDrag(sender);

    private void DragLostCapture(object sender, MouseEventArgs e) => EndDrag(sender);

    private void EndDrag(object sender)
    {
        if (!_dragging || sender is not Window window)
        {
            return;
        }

        _dragging = false;
        if (_dragVeiled)
        {
            _dragVeiled = false;
            _main.SetDragVeil(false);
            _quick.SetDragVeil(false);
        }
        window.MouseMove -= DragMove_MouseMove;
        window.MouseUp -= DragEnd_MouseUp;
        window.LostMouseCapture -= DragLostCapture;
        if (window.IsMouseCaptured)
        {
            window.ReleaseMouseCapture();
        }

        _tuck.SetDragging(false);
        _tuck.SetSnapped(_anchor.Y <= WorkArea().Y);
        ApplyLayout(); // 松手：完整布局 + 落盘
    }

    /// <summary>
    /// 拖动采样：PointToScreen 返回物理像素，而 Window.Left/Top 是逻辑像素（DIP）。
    /// 高 DPI（如 125%）下若直接混用，窗口会比鼠标"跑得更快"、越拖越偏——
    /// 这里统一把物理像素换算回逻辑像素，保证鼠标与窗口严格同步。
    /// </summary>
    private static Point ToScreen(Window window, MouseEventArgs e)
    {
        var device = window.PointToScreen(e.GetPosition(window));
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Point(device.X / dpi.DpiScaleX, device.Y / dpi.DpiScaleY);
    }

    // ---------- 等比缩放 ----------

    /// <summary>
    /// 缩放（自绘鼠标捕获，与标题栏拖动同一套"屏幕逻辑像素"采样）：
    /// 以按下时的鼠标位置与主窗宽度为基准，横向增量决定等比缩放（原型口径：横向拖动）。
    /// 不用 Thumb.DragDelta——其在 Viewbox 缩放坐标空间下的增量会随缩放比失真。
    /// </summary>
    private void ScaleGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_resizing)
        {
            return;
        }

        _resizing = true;
        EndHover(); // 缩放期间窗口矩形在变，气泡失锚
        _resizeStartMouse = ToScreen(_main, e);
        _resizeStartMainWidth = _main.ActualWidth;
        _tuck.SetResizing(true);
        SetResizeVisual(resizing: true);
        _ = _main.CaptureMouse();
        _main.MouseMove += ScaleGrip_MouseMove;
        _main.MouseUp += ScaleGrip_MouseUp;
        e.Handled = true;
    }

    private void ScaleGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var mouse = ToScreen(_main, e);
        var workArea = WorkArea();
        var targetMainWidth = Math.Max(
            PanelLayout.MainWidth * PanelLayout.MinScaleFor(workArea.Height),
            _resizeStartMainWidth + (mouse.X - _resizeStartMouse.X));
        // 拖拽期间的边界按当前屏（用户主动调整时的自然限制）；写入的是用户档位，
        // 加载时不再按屏钳制（见 LayoutScale 注释）。
        _storedScale = PanelLayout.ClampScale(
            targetMainWidth / PanelLayout.MainWidth, workArea, SnappedAnchor());
        RequestResizeLayout();
    }

    private void ScaleGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing)
        {
            return;
        }

        _resizing = false;
        _main.MouseMove -= ScaleGrip_MouseMove;
        _main.MouseUp -= ScaleGrip_MouseUp;
        if (_main.IsMouseCaptured)
        {
            _main.ReleaseMouseCapture();
        }

        _tuck.SetResizing(false);
        SetResizeVisual(resizing: false);
        ApplyLayout(); // 松手：完整布局 + 落盘
    }

    /// <summary>
    /// 缩放期间是否隐藏两侧卡片。**当前为 false（两侧保持可见）——按用户裁定回退，进入缩放逻辑调整阶段。**
    /// 置 true = 最后备用方案：拖动期间把两侧透明度归零（松手恢复），彻底消除"缝隙呼吸"
    /// （放大时主窗右缘是"需重绘内容"的边、已完成窗左缘是"纯移动"的边，合成落在不同帧 → 缝宽瞬时变化）。
    /// 位移与尺寸无论开关如何都每帧照常计算，隐蔽期间不会影响松手后的归位。
    /// </summary>
    private const bool HideSideWindowsDuringResize = false;

    private void SetResizeVisual(bool resizing)
    {
        var opacity = HideSideWindowsDuringResize && resizing ? 0d : 1d;
        _quick.Opacity = opacity;
    }

    /// <summary>置 true 可在拖缩放柄时把每帧几何写盘（临时目录下的 resize-trace.log），用于定位“边缘没钉住”。</summary>

    /// <summary>缩放中的布局请求：合并到下一帧统一执行（CompositionTarget.Rendering 每帧一次）。</summary>
    private void RequestResizeLayout()
    {
        if (_resizeLayoutQueued)
        {
            return;
        }

        _resizeLayoutQueued = true;
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            CompositionTarget.Rendering -= handler;
            _resizeLayoutQueued = false;
            ApplyLayout(saveState: false);

        };
        CompositionTarget.Rendering += handler;
    }



    // ---------- 面板开关 ----------

    public void ToggleQuick()
    {
        ExitTuckBeforeLayout();
        Mutate(d => d with { UiState = d.UiState with { QuickVisible = !d.UiState.QuickVisible } });
        _main.SetPanelToggles(_data.UiState.QuickVisible, _data.UiState.DoneVisible);
        ApplyLayout();
    }


    /// <summary>
    /// 缩进动画用 BeginAnimation 持有窗口 Top 属性；显隐模板/已完成窗前若不清除，
    /// 随后的布局设置会被动画覆盖，出现两窗错位。这里先结束动画并复位缩进状态。
    /// </summary>
    private void ExitTuckBeforeLayout()
    {
        _tuck.NotifyUntuckCompleted();
        _tuck.SetSnapped(false);
        _main.BeginAnimation(Window.TopProperty, null);
        _quick.BeginAnimation(Window.TopProperty, null);
        _main.Top = _anchor.Y;
        _quick.Top = _anchor.Y;
        _tuck.SetSnapped(_anchor.Y <= WorkArea().Y);
    }

    // ---------- 置顶 / 缩进锁定 / 主题 ----------

    public void ToggleTopMost()
    {
        Mutate(d => d with { UiState = d.UiState with { TopMost = !d.UiState.TopMost } });
        ApplyTopMost(_data.UiState.TopMost);
        _main.SetTopMost(_data.UiState.TopMost);
    }

    private void ApplyTopMost(bool on)
    {
        _main.Topmost = on;
        _quick.Topmost = on;
        _tuckBar.Topmost = on;
    }

    public void ToggleTuckLock()
    {
        Mutate(d => d with { UiState = d.UiState with { TuckLock = !d.UiState.TuckLock } });
        _tuck.SetLocked(_data.UiState.TuckLock);
        _main.SetTuckLock(_data.UiState.TuckLock);
    }

    /// <summary>切换已完成排序（主窗「已完成」标题行右端按钮）。</summary>
    public void ToggleDoneSort()
    {
        Mutate(d => d with
        {
            Settings = d.Settings with { DoneSort = d.Settings.DoneSort == "asc" ? "desc" : "asc" },
        });
        RefreshAllViews();
    }

    public void ToggleTheme()
    {
        App.ApplyTheme(!App.IsNight);
        // 主题落盘（此前只在内存切换，重启丢失——批次05③ 修复）
        Mutate(d => d with { UiState = d.UiState with { Theme = App.IsNight ? "night" : "day" } });
        _main.ThemeBtn.Tag = (System.Windows.Media.Geometry)_main.FindResource(App.IsNight ? "Icon.Sun" : "Icon.Moon");
    }

    private void ApplyThemeState()
    {
        if (_data.UiState.Theme == "night")
        {
            App.ApplyTheme(true);
            _main.ThemeBtn.Tag = (System.Windows.Media.Geometry)_main.FindResource("Icon.Sun");
        }
    }

    // ---------- 缩进执行（R1：整组上滑 → 白条 → 真实命中滑回） ----------

    private void OnPointerEnter()
    {
        _tuck.NotifyPointerEntered(DateTimeOffset.Now);
        _tuck.SetPointerInside(true);
    }

    private void TuckTick()
    {
        var now = DateTimeOffset.Now;
        _tuck.Tick(now);

        foreach (var command in _tuck.DrainCommands())
        {
            switch (command)
            {
                case TuckCommand.Tuck:
                    AnimateCluster(slideOut: true);
                    break;
                case TuckCommand.Untuck:
                    ShowWindowsForUntuck();
                    AnimateCluster(slideOut: false);
                    break;
                case TuckCommand.StartHold:
                case TuckCommand.EndHold:
                    // 会话时长与判定在状态机；执行层无需计时
                    break;
            }
        }
    }

    private void AnimateCluster(bool slideOut)
    {
        var ui = _data.UiState;
        var workArea = WorkArea();
        var scale = LayoutScale(workArea, SnappedAnchor());
        var frame = PanelLayout.Arrange(SnappedAnchor(), scale, ui.QuickVisible);

        var targets = new List<(Window Window, double DockedTop, double HiddenTop)>();
        if (frame.Main.Visible)
        {
            targets.Add((_main, frame.Main.Rect.Y, workArea.Y - frame.Main.Rect.Height - 8));
        }

        if (frame.Template.Visible)
        {
            var tplH = TemplateWindowHeight() * scale;
            targets.Add((_quick, frame.Template.Rect.Y, workArea.Y - tplH - 8));
        }

        if (slideOut)
        {
            _tuckBar.Hide();
        }

        var completed = 0;
        var total = targets.Count;
        if (total == 0)
        {
                if (slideOut)
                {
                    _tuck.NotifyTuckCompleted();
                    _tuckBar.Show();
                }
                else
                {
                    // 弹出完成即收白条（此前只在下次缩进开始时隐藏，导致"弹出后白条仍挂着"）
                    _tuck.NotifyUntuckCompleted();
                    _tuckBar.Hide();
                }

                return;
        }

        foreach (var (window, dockedTop, hiddenTop) in targets)
        {
            var toTop = slideOut ? hiddenTop : dockedTop;
            AnimateTop(window, toTop, () =>
            {
                completed++;
                if (completed < total)
                {
                    return;
                }

                if (slideOut)
                {
                    _tuck.NotifyTuckCompleted();
                    _tuckBar.Show();
                }
                else
                {
                    _tuck.NotifyUntuckCompleted();
                    _tuckBar.Hide();
                }
            });
        }
    }

    private void ShowWindowsForUntuck()
    {
        // 缩进态托盘恢复等场景：从白条弹出前先把可见窗 Show 出来（仍离屏）
        if (_clusterHidden)
        {
            return;
        }

        var ui = _data.UiState;
        _main.Show();
        if (ui.QuickVisible)
        {
            _quick.Show();
        }

        if (ui.DoneVisible)
        {
            // 已完成窗已不参与布局（批次04 收尾），不再显示
        }
    }

    private static void AnimateTop(Window window, double toTop, Action completed)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
        };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(180),
            Value = toTop,
            KeySpline = new KeySpline(0.22, 0.61, 0.36, 1),
        });
        animation.Completed += (_, _) => completed();

        window.BeginAnimation(Window.TopProperty, animation);
    }

    // ---------- 时钟 ----------

    private void UpdateClock()
    {
        var now = DateTimeOffset.Now;

        // 跨天结算（需求 §2.9，全程静默）：日期变化（含挂起唤醒跨天）时自动归档昨日已完成
        // + 重算未完成逾期标；启动补结算由 AppBootstrap 负责，这里管运行期。
        var todayKey = LocalDate.Key(now);
        if (_data.UiState.LastSettleDate != todayKey)
        {
            var settle = SettlementService.Settle(_data, now);
            if (settle.Changed)
            {
                _data = settle.Data;
                RefreshAllViews();
                _ = _repository.SaveAsync(_data);
            }
        }

        _main.SetClock(
            MainViewService.ClockText(now, _data.Settings.Hour12),
            MainViewService.CnDateText(now));
    }

    // ---------- 批次04：待办 CRUD（二级窗 + 原子数据操作） ----------

    /// <summary>有二级窗打开（组件保持弹出；缩进门槛由 TuckStateMachine 承担）。
    /// 模态可嵌套（设置→历史→详情），按计数开关。</summary>
    public bool IsModalOpen { get; private set; }

    private int _modalDepth;

    private void BeginModal()
    {
        _modalDepth++;
        IsModalOpen = true;
        _tuck.SetChildOpen(true);
    }

    private void EndModal()
    {
        _modalDepth = Math.Max(0, _modalDepth - 1);
        IsModalOpen = _modalDepth > 0;
        if (!IsModalOpen)
        {
            _tuck.SetChildOpen(false);
        }
    }

    /// <summary>CRUD 成功后统一走这里：替换数据 → 刷新三窗 → 防抖落盘。</summary>
    private void ApplyOps(TodoOpsResult result)
    {
        if (!result.Ok)
        {
            return;
        }

        _data = result.Data;
        RefreshAllViews();
        // 下一帧再改窗口高度：让列表先完成追加/删除，避免窗口 resize 与列表更新同帧造成闪烁。
        _main.Dispatcher.BeginInvoke(
            new Action(() => ApplyLayout(saveState: false)),
            DispatcherPriority.Loaded);
        _ = _repository.SaveAsync(_data);
    }

    // ---------- 悬停气泡（批次05①） ----------

    /// <summary>行文本悬停进入：300ms 后若内容确实被截断则显示全文气泡。</summary>
    public void HoverText(TextBlock textElement, FrameworkElement row)
    {
        _hoverTextEl = textElement;
        _hoverRow = row;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    /// <summary>
    /// 数据格式兼容检测（C4，用户 2026-11-09 裁定「提示 + 打开数据目录」）：
    /// 读 data.json 里的格式版本 `v`，与 <see cref="TodoWpf.Core.Persistence.DataCompatibility"/> 判定比对；
    /// 不兼容（过旧/过新）时提示用户并可直接打开数据目录备份。同一应用版本只提示一次（避免每次启动打扰）。
    /// 判定为纯逻辑（Core 已 TDD）；此处只做文件读取与提示。
    /// </summary>
    private void CheckDataCompatibilityOnce()
    {
        try
        {
            var verdict = TodoWpf.Core.Persistence.DataCompatibility.Check(_fileSchemaVersion);
            if (verdict == TodoWpf.Core.Persistence.DataCompatibilityResult.Compatible)
            {
                return;
            }

            var path = TodoWpf.Core.Persistence.DataPaths.NewDataFile();
            var current = UpdateService.CurrentVersion();
            if (_data.UiState.LastCompatWarnVersion == current)
            {
                return; // 本版本已提示过
            }

            var shown = _fileSchemaVersion is null ? "无版本标记" : $"v{_fileSchemaVersion}";
            var reason = verdict == TodoWpf.Core.Persistence.DataCompatibilityResult.TooNew
                ? $"已有数据的格式（{shown}）比本版本（支持 v{TodoWpf.Core.Models.AppData.CurrentSchemaVersion}）新——" +
                  "通常是安装回了较旧的版本。本版本会尽量读取，但新版本新增的内容可能读不到；继续使用会覆盖这些内容。"
                : $"已有数据的格式（{shown}）低于本版本可读下限（v{TodoWpf.Core.Models.AppData.MinReadableSchemaVersion}）。";
            var outcome = ConfirmDialog.Ask(
                "数据格式不兼容",
                reason + "\n\n建议先备份本地数据目录，再决定是否继续。",
                okText: "打开数据目录",
                cancelText: "继续使用",
                showCloseButton: false);
            if (outcome == ConfirmDialog.Outcome.Ok)
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                }
            }

            Mutate(d => d with { UiState = d.UiState with { LastCompatWarnVersion = current } });
        }
        catch
        {
            // 兼容检测属提示性功能：任何异常都不得影响正常启动
        }
    }

    /// <summary>读 data.json 里的原始格式版本（失败/不存在 → null）。</summary>
    private static int? ReadFileSchemaVersionOnce()
    {
        try
        {
            var path = TodoWpf.Core.Persistence.DataPaths.NewDataFile();
            return System.IO.File.Exists(path)
                ? TodoWpf.Core.Persistence.JsonCodec.ReadSchemaVersion(System.IO.File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>手动检查发现的版本也走这里登记（标题栏提示与设置页弹窗共用）。</summary>
    public void SetPendingUpdate(UpdateService.UpdateAvailable available)
    {
        _pendingUpdate = available;
        RefreshUpdateBadge();
    }

    /// <summary>
    /// 标题栏「有新版本！」刷新（C4 定稿）：有发现的新版本**且**提示开关开才显示；
    /// 开关关 → 立即隐藏（哪怕有新版本）。
    /// </summary>
    public void RefreshUpdateBadge()
    {
        var show = _pendingUpdate is not null && _data.UiState.UpdateNotifyEnabled;
        _main.SetUpdateBadge(show);
    }

    /// <summary>「新版本提示」开关切换（设置页）：落盘并即时刷新标题栏；开 → 立刻补一次检查。</summary>
    public void SetUpdateNotify(bool enabled)
    {
        Mutate(d => d with { UiState = d.UiState with { UpdateNotifyEnabled = enabled } });
        if (!enabled)
        {
            RefreshUpdateBadge();
            return;
        }

        // 从关切到开：立刻补一次检查（方案A 体验说明书口径），有新版本马上亮标。
        if (_pendingUpdate is not null)
        {
            RefreshUpdateBadge();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var available = await _updates.CheckAsync();
                if (available is not null)
                {
                    await _main.Dispatcher.InvokeAsync(() =>
                    {
                        _pendingUpdate = available;
                        RefreshUpdateBadge();
                    });
                }
            }
            catch
            {
                // 同启动检查：静默
            }
        });
    }

    /// <summary>
    /// 标题栏「有新版本！」点击 → 更新提示窗（C4 定稿：新版本号+更新内容+两按钮；
    /// 「重启完成更新」=下载并应用更新后由 Velopack 重启；「暂不更新」=关窗）。
    /// </summary>
    public void ShowUpdateBadgePrompt()
    {
        if (_pendingUpdate is not { } update)
        {
            return;
        }

        ShowUpdatePrompt(update, AppWindows.Main());
    }

    /// <summary>更新提示窗（anchor=触发它的窗口：标题栏→主窗、设置页→设置窗）。</summary>
    public void ShowUpdatePrompt(UpdateService.UpdateAvailable update, Window? anchor)
    {
        var body = string.IsNullOrWhiteSpace(update.Notes)
            ? $"新版本 {update.Version} 已就绪。"
            : $"新版本 {update.Version} 已就绪。\n\n更新内容：\n{update.Notes}";
        var outcome = ConfirmDialog.Ask(
            $"发现新版本 {update.Version}", body, "重启完成更新",
            anchor: anchor, cancelText: "暂不更新", showCloseButton: true);
        if (outcome == ConfirmDialog.Outcome.Ok)
        {
            _ = UpdateAndRestartAsync();
        }
    }

    /// <summary>下载并应用更新后由 Velopack 重启；失败（含开发副本未安装）明确提示。</summary>
    private async Task UpdateAndRestartAsync()
    {
        try
        {
            ShowToast("正在更新，应用将自动重启…");
            await _updates.UpdateAndRestartAsync();
        }
        catch (Exception ex)
        {
            ShowToast("更新失败：" + ex.Message);
        }
    }

    /// <summary>悬停结束（移出/滚动/拖动/刷新/隐藏窗）。瞬时隐藏，不淡出（原型口径）。</summary>
    public void EndHover()
    {
        _hoverTimer.Stop();
        _hoverTextEl = null;
        _hoverRow = null;
        _bubble.Hide();
    }

    /// <summary>行元素的完整内容（气泡用）：三种行投影都带 FullContent；取不到就回退行内文本。</summary>
    private static string? FullContentOf(FrameworkElement? row) => row?.DataContext switch
    {
        TodoRowView t => t.FullContent,
        DoneRowView d => d.FullContent,
        TemplateRowView t => t.FullContent,
        _ => null,
    };

    private void ShowHoverBubble()
    {
        _hoverTimer.Stop();
        if (_hoverTextEl is not { } tb || _hoverRow is null || _clusterHidden || _batch.IsActive)
        {
            return;
        }

        // 弹气泡的两种情形（用户 2026-11-06）：
        // ① 行内文本被截断（宽度放不下）；② 行只显示第一行、而换行后还有文字（行里看不全）。
        var full = FullContentOf(_hoverRow) ?? tb.Text;
        if (!BubbleWindow.IsTruncated(tb, tb.Text) && !MainViewService.HasContentAfterFirstLine(full))
        {
            return; // 全文已在行内看全，不弹
        }

        // 缩放比跟所属窗：模板窗行用模板窗缩放，其余按主窗（原型 --bubble-scale 口径）
        var scale = ReferenceEquals(Window.GetWindow(tb), _quick)
            ? _quick.ContentScaleFactor
            : _main.ContentScaleFactor;
        _bubble.Show(_hoverRow, full, scale, MonitorLayout.WorkAreaDips(_main));   // 气泡显示全文（可含换行）
    }

    /// <summary>勾选完成（主窗行）。</summary>
    public void CompleteRow(string id)
    {
        var r = TodoOpsService.CompleteTodo(_data, id, DateTimeOffset.Now);
        if (r.Ok)
        {
            ApplyOps(r);
        }
    }

    /// <summary>拖拽排序落位（批次05①；松手归位动画结束后调用）。</summary>
    public void MoveTodo(string id, int newIndex)
    {
        var r = TodoOpsService.MoveTodo(_data, id, newIndex);
        if (r.Ok)
        {
            ApplyOps(r);
        }
    }

    // ---------- 设置窗（批次05③） ----------

    /// <summary>打开设置窗（标题栏齿轮；模态）。</summary>
    public void OpenSettings()
    {
        BeginModal();
        SettingsDialog.Show(this);
        EndModal();
    }

    /// <summary>待办上限 1~10 步进；不可低于当前待办数量（需求 §2.8）。</summary>
    public void SetMaxItems(int value)
    {
        var floor = Math.Max(1, _data.Todos.Count);
        var clamped = Math.Clamp(value, floor, AppSettings.MaxMaxItems);
        if (clamped == _data.Settings.MaxItems)
        {
            return;
        }

        Mutate(d => d with { Settings = d.Settings with { MaxItems = clamped } });
        RefreshAllViews();
    }

    /// <summary>时制切换（时钟即时变化并落盘）。</summary>
    public void SetHour12(bool hour12)
    {
        if (_data.Settings.Hour12 == hour12)
        {
            return;
        }

        Mutate(d => d with { Settings = d.Settings with { Hour12 = hour12 } });
        UpdateClock();
    }

    /// <summary>主题切换（设置窗入口；即时生效并落盘）。</summary>

    /// <summary>恢复默认尺寸：按当前屏重算（屏高 1/3）并落盘（体验说明书 ③.3）。</summary>
    public void ResetWindowSize()
    {
        var workArea = WorkArea();
        _storedScale = PanelLayout.ClampScale(
            PanelLayout.DefaultMainHeight(workArea.Height) / PanelLayout.MainBaseHeight,
            workArea, SnappedAnchor());
        ApplyLayout();
        ShowToast("已按当前屏恢复默认尺寸");
    }

    /// <summary>开机自启（注册表 Run；开启时清除 StartupApproved 禁用标记加固；需求 §2.8）。
    /// 注册表被拦截/拒绝时只提示，不绕过。</summary>
    public void SetAutoStart(bool on)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string approvedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const string appName = "桌面待办-WPF";

        try
        {
            using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (run is null)
            {
                ShowToast("注册表不可写，无法修改自启");
                return;
            }

            if (on)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    ShowToast("无法获取程序路径");
                    return;
                }

                run.SetValue(appName, $"\"{exe}\"");
                using var approved = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(approvedKey, writable: true);
                approved?.DeleteValue(appName, throwOnMissingValue: false);
            }
            else
            {
                run.DeleteValue(appName, throwOnMissingValue: false);
            }

            Mutate(d => d with { Settings = d.Settings with { AutoStart = on } });
            ShowToast(on ? "已开启开机自启" : "已关闭开机自启");
        }
        catch (System.Security.SecurityException)
        {
            ShowToast("注册表写入被拒绝");
        }
        catch (System.UnauthorizedAccessException)
        {
            ShowToast("注册表写入被拒绝");
        }
        catch (System.IO.IOException)
        {
            ShowToast("注册表写入失败");
        }
    }

    /// <summary>导入历史 CSV（覆盖/合并；文件框 + 校验提示；需求 §2.8）。</summary>
    public void ImportHistoryCsv(string mode, System.Windows.Window anchor)
    {
        if (mode == CsvService.ModeCover &&
            !ConfirmDialog.Confirm("覆盖导入历史记录？", "会清空现有全部历史记录（含今天已完成的），无法恢复。",
                "选择文件", danger: true, anchor: anchor))
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
        };
        if (dialog.ShowDialog(_main) != true)
        {
            return;
        }

        string text;
        try
        {
            text = System.IO.File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);
        }
        catch (System.IO.IOException)
        {
            ShowToast("文件读取失败");
            return;
        }
        catch (System.Security.SecurityException)
        {
            ShowToast("没有读取权限");
            return;
        }

        var (next, ok, imported) = CsvService.Import(
            _data, text, mode, LocalDate.Key(DateTimeOffset.Now), DateTimeOffset.Now);
        if (!ok)
        {
            ShowToast("文件格式不正确，只支持本软件导出的 CSV");
            return;
        }

        if (imported == 0)
        {
            ShowToast("没有新记录（内容已存在）");
            return;
        }

        _data = next;
        RefreshAllViews();
        _ = _repository.SaveAsync(_data);
        ShowToast($"已导入 {imported} 条");
    }

    // ---------- 历史窗（批次05③） ----------

    /// <summary>当前数据快照（历史窗每次渲染时读取）。</summary>
    public AppData Data => _data;

    /// <summary>二级窗分组缩放存档（0.7~1.2；用户 2026-11-04 裁定：二级窗只做等比缩放）。</summary>
    public void SetChildScale(string group, double scale)
    {
        var dict = new System.Collections.Generic.Dictionary<string, double>(_data.UiState.ChildScales);
        dict[group] = Math.Clamp(scale, 0.7, 1.2);
        Mutate(d => d with { UiState = d.UiState with { ChildScales = dict } });
    }

    /// <summary>打开历史记录（设置页「打开」；模态嵌套于设置窗）。</summary>
    public void OpenHistory()
    {
        BeginModal();
        HistoryDialog.Show(this);
        EndModal();
    }

    /// <summary>历史窗按日批量删除（确认框由历史窗负责）。</summary>
    public bool DeleteArchivedMany(string dateKey, IReadOnlyCollection<string> ids)
    {
        var r = TodoOpsService.DeleteArchivedMany(_data, dateKey, ids);
        if (r.Ok)
        {
            ApplyOps(r);
        }

        return r.Ok;
    }

    /// <summary>导出历史 CSV（系统另存为；UTF-8 BOM；需求 §2.7）。</summary>
    public void ExportHistoryCsv(string from, string to)
    {
        if (string.CompareOrdinal(from, to) > 0)
        {
            ShowToast("起始日期不能晚于结束日期");
            return;
        }

        var csv = CsvService.Export(_data, from, to);
        if (csv is null)
        {
            ShowToast("该区间暂无记录");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = CsvService.ExportFileName(from, to),
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
        };
        if (dialog.ShowDialog(_main) != true)
        {
            return;
        }

        try
        {
            System.IO.File.WriteAllText(dialog.FileName, csv, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            ShowToast("已导出 CSV");
        }
        catch (System.IO.IOException)
        {
            ShowToast("文件被占用，请关闭后重试");
        }
        catch (System.UnauthorizedAccessException)
        {
            ShowToast("没有写入权限，请换一个位置");
        }
    }

    /// <summary>＋ 新建模板（快捷窗；上限 10、同内容查重）。</summary>
    public void OpenNewTemplate()
    {
        if (_data.Templates.Count >= TemplateLimit.Max)
        {
            ShowOpsError(TodoOpsService.ReasonTemplateLimitReached);
            return;
        }

        // 需求 §2.4：返回时"无输入直接退、有输入询问"（2026-11-06 查漏：此前只有编辑路径有询问）
        var saved = false;
        var latestText = "";

        BeginModal();
        TextEditDialog.Open(b =>
        {
            b.Title("新建模板").Placeholder("模板内容（≤300 字）")
             .Action("save", "新建模板", primary: true, enabled: false, autoClose: false, onClick: d =>
             {
                 var r = TodoOpsService.AddTemplate(_data, d.Editor.Text, DateTimeOffset.Now);
                 if (r.Ok)
                 {
                     saved = true;
                     ApplyOps(r);
                     ShowToast("模板已新建");
                     d.Close();
                 }
                 else
                 {
                     ShowOpsError(r.Reason); // 查重/超长：提示且不关闭
                 }
             })
             .OnTextChanged(d =>
             {
                 latestText = d.Editor.Text;
                 var ok = ContentRules.Validate(d.Editor.Text).Ok;
                 ((System.Windows.Controls.Button)((System.Windows.Controls.Panel)d.ActionsPanel).Children[0]).IsEnabled = ok;
             })
             .OnCloseRequested(d =>
             {
                 if (saved || d.Editor.Text.Trim().Length == 0)
                 {
                     return true;   // 已保存 或 空输入 → 直接关
                 }

                // 有未保存输入 → 弹在编辑窗**之上**（模态），编辑窗不先消失（用户 2026-11-06 裁定）
                var outcome = ConfirmDialog.Ask("未保存的输入", "输入的内容还没保存，确认退出吗？",
                                                "保存", anchor: d, cancelText: "确认", showCloseButton: true);
                if (outcome == ConfirmDialog.Outcome.Closed)
                {
                    return false;   // 右上角 X → 关掉弹窗、回到编辑窗继续编辑
                }

                if (outcome == ConfirmDialog.Outcome.Ok)
                {
                    var r = TodoOpsService.AddTemplate(_data, latestText, DateTimeOffset.Now);
                    if (!r.Ok)
                    {
                        ShowOpsError(r.Reason);   // 保存失败 → 不关闭（与"保存失败不关窗"一致）
                        return false;
                    }

                    saved = true;
                    ApplyOps(r);
                    ShowToast("模板已新建");
                }

                return true;   // 「确认」= 确认放弃 → 关闭编辑窗
            });
        }, this);
        EndModal();
    }

    /// <summary>编辑模板（双击模板行；返回时已改询问）。</summary>
    public void OpenEditTemplate(string id)
    {
        var item = _data.Templates.Find(t => t.Id == id);
        if (item is null)
        {
            return;
        }

        var saved = false;
        var dirty = false;
        var latestText = item.Content;

        BeginModal();
        TextEditDialog.Open(b =>
        {
            b.Title("编辑模板").Text(item.Content).Placeholder("模板内容（≤300 字）")
             .LeftAction("delete", "删除", danger: true, autoClose: false, onClick: d =>
             {
                 if (ConfirmDialog.Confirm("删除模板", "确定删除这个模板吗？", "删除", danger: true, anchor: d))
                 {
                     saved = true;
                     ApplyOps(TodoOpsService.DeleteTemplate(_data, id));
                     ShowToast("已删除模板");
                     d.Close();
                 }
             })
             .Action("save", "保存", primary: true, autoClose: false, onClick: d =>
             {
                 var r = TodoOpsService.EditTemplate(_data, id, d.Editor.Text);
                 if (!r.Ok)
                 {
                     ShowOpsError(r.Reason); // 保存失败（查重/空内容等）：提示且不关闭
                     return;
                 }

                 saved = true;
                 ApplyOps(r);
                 ShowToast("模板已保存");
                 d.Close();
             })
             .OnTextChanged(d =>
             {
                 latestText = d.Editor.Text;
                 dirty = latestText.Trim() != item.Content;
             })
             .OnCloseRequested(d =>
             {
                 if (saved || !dirty)
                 {
                     return true;   // 未改过 → 直接关
                 }

                // 有未保存输入 → 弹在编辑窗**之上**（模态），编辑窗不先消失（用户 2026-11-06 裁定）
                var outcome = ConfirmDialog.Ask("未保存的修改", "输入的内容还没保存，确认退出吗？",
                                                "保存", anchor: d, cancelText: "确认", showCloseButton: true);
                if (outcome == ConfirmDialog.Outcome.Closed)
                {
                    return false;   // 右上角 X → 关掉弹窗、回到编辑窗继续编辑
                }

                if (outcome == ConfirmDialog.Outcome.Ok)
                {
                    var r = TodoOpsService.EditTemplate(_data, id, latestText);
                    if (!r.Ok)
                    {
                        ShowOpsError(r.Reason);   // 保存失败 → 不关闭（与"保存失败不关窗"一致）
                        return false;
                    }

                    saved = true;
                    ApplyOps(r);
                    ShowToast("模板已保存");
                }

                return true;   // 「确认」= 确认放弃 → 关闭编辑窗
            });
        }, this);
        EndModal();
    }

    // ---------- 批次04：批量删除（主窗待办；跨窗冻结由缩进门槛+模态承担） ----------

    /// <summary>批量删除状态机（目标=待办；选择集与置灰口径）。</summary>
    private readonly BatchState _batch = new();

    /// <summary>进入/退出 批量删除（主窗「批量删除」按钮）。
    /// 空列表不进入（原型：toast「没有可批量删除的待办」）。</summary>
    public void ToggleBatch()
    {
        if (_batch.IsActive)
        {
            _batch.Exit();
            PushBatchBar();
            return;
        }

        if (_data.Todos.Count == 0)
        {
            ShowToast("没有可批量删除的待办");
            return;
        }

        _batch.Enter("todos");
        PushBatchBar();
    }

    /// <summary>批量中：切换某行选中态（轻量推送：目标窗计数条 + 该行视觉，不整表重建）。</summary>
    public void ToggleBatchSelection(string id)
    {
        _batch.Toggle(id);
        var selected = _batch.IsSelected(id);
        if (_batch.Target == "templates")
        {
            _quick.UpdateRowSelection(id, selected);
            _quick.SetTemplateBatch(true, _batch.SelectedCount, _batch.CanDelete, _data.Templates.Count);
        }
        else
        {
            _main.UpdateRowSelection(id, selected);
            _main.SetBatchBar(_batch.IsActive, _batch.SelectedCount, _batch.CanDelete, _data.Todos.Count);
        }
    }

    /// <summary>「全选 / 取消全选」切换（原型 batchAll 口径）。</summary>
    public void ToggleBatchAll()
    {
        _batch.ToggleAll(_batch.Target == "templates"
            ? _data.Templates.Select(t => t.Id)
            : _data.Todos.Select(t => t.Id));
        PushBatchBar();
    }

    /// <summary>进入/退出 模板批量（模板窗「批量删除」按钮）。
    /// 空列表不进入（原型 toast「还没有模板」）。</summary>
    public void ToggleTemplateBatch()
    {
        if (_batch.IsActive && _batch.Target == "templates")
        {
            _batch.Exit();
            PushBatchBar();
            return;
        }

        if (_data.Templates.Count == 0)
        {
            ShowToast("还没有模板");
            return;
        }

        _batch.Enter("templates");
        PushBatchBar();
    }

    /// <summary>当前是否模板批量态（模板窗行点击/双击分支用）。</summary>
    public bool IsTemplateBatch => _batch.Target == "templates";

    /// <summary>当前是否任一批量态（Esc 出口用）。</summary>
    public bool IsBatchActive => _batch.IsActive;

    /// <summary>「删除所选」：确认框 → 永久删除 → 退出批量（按目标分支：待办 / 模板）。</summary>
    public void DeleteSelected()
    {
        if (!_batch.CanDelete)
        {
            return;
        }

        var isTemplates = _batch.Target == "templates";
        if (!ConfirmDialog.Confirm(
                isTemplates ? "删除所选模板" : "删除所选待办",
                isTemplates
                    ? $"确定删除所选的 {_batch.SelectedCount} 个模板吗？删除后无法恢复。"
                    : $"确定删除所选的 {_batch.SelectedCount} 项吗？删除后无法恢复。",
                "删除", danger: true, anchor: _main))
        {
            return;
        }

        var r = isTemplates
            ? TodoOpsService.DeleteTemplates(_data, _batch.SelectedIds)
            : TodoOpsService.DeleteTodos(_data, _batch.SelectedIds);
        if (r.Ok)
        {
            ApplyOps(r);
        }

        _batch.Exit();
        PushBatchBar();
    }

    /// <summary>退出批量（批量条「返回」）。</summary>
    public void ExitBatch()
    {
        _batch.Exit();
        PushBatchBar();
    }

    /// <summary>
    /// 批量态视觉推送（唯一入口，进入/切换/退出都走这里）。单目标互斥（原型口径）：
    /// 主窗批量 ⇒ 模板窗整窗冻结；模板批量 ⇒ 主窗整窗冻结（＋新建冻结、设置钮随整窗冻结）。
    /// </summary>
    private void PushBatchBar()
    {
        var mainBatch = _batch.IsActive && _batch.Target == "todos";
        var tplBatch = _batch.IsActive && _batch.Target == "templates";

        _main.SetBatchBar(mainBatch, _batch.SelectedCount, _batch.CanDelete, _data.Todos.Count);
        _main.SetRows(MainViewService.TodoRows(_data, LocalDate.Key(DateTimeOffset.Now), mainBatch ? _batch : null));
        _main.SetFrozen(tplBatch);

        _quick.SetTemplateBatch(tplBatch, _batch.SelectedCount, _batch.CanDelete, _data.Templates.Count);
        // ⚠ 必须把 tplBatch 传进去：漏传时默认 false，SetTemplateRows 会以为"批量态没变"而走只追加快路径，
        //   旧行的 Selected/BatchMode 不会被清 → 退出批量后仍显示选中样式（用户 2026-11-04 二次反馈）。
        _quick.SetTemplateRows(
            MainViewService.TemplateRows(_data, tplBatch ? _batch : null),
            Core.Models.TemplateLimit.Max, tplBatch);
        _quick.SetFrozen(mainBatch);

        _tuck.SetBatching(_batch.IsActive);
    }

    /// <summary>模板一键加入今日待办（快捷窗行；上限已满时提示；模板保留）。</summary>
    public void ApplyTemplateRow(string templateId)
    {
        var r = TodoOpsService.ApplyTemplate(_data, templateId, DateTimeOffset.Now);
        if (r.Ok)
        {
            ApplyOps(r);
        }
        else
        {
            ShowOpsError(r.Reason);
        }
    }

    /// <summary>已完成退回（已完成行勾选框反选；上限已满时提示）。</summary>
    public void ReturnRow(string id)
    {
        var r = TodoOpsService.ReturnTodo(_data, id, LocalDate.Key(DateTimeOffset.Now), DateTimeOffset.Now);
        if (r.Ok)
        {
            ApplyOps(r);
            return;
        }

        // 退回场景的文案要跟"添加"区分开：共用 ReasonLimitReached，但说"无法添加"会看不懂
        if (r.Reason == TodoOpsService.ReasonLimitReached)
        {
            ShowToast($"待办数量已达上限（{_data.Settings.MaxItems} 条），无法退回");
        }
        else
        {
            ShowOpsError(r.Reason);
        }
    }

    /// <summary>
    /// toast 提示（主窗底部深色胶囊；批次05①）。
    /// **画在"当前活动窗口"上**（用户 2026-11-05 裁定）：此前写死发主窗，设置窗/历史窗
    /// 触发的提示全被模态二级窗盖住；现在谁在用就显示在谁身上。
    /// </summary>
    public void ShowToast(string message) => ToastPresenter.Show(ToastTarget(), message);

    /// <summary>提示宿主 = 本应用当前活动的那个窗口（气泡/白条是 ShowActivated=False，天然不会中选）；没有则回落主窗。</summary>
    private Window ToastTarget() =>
        System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive && w.IsVisible)
        ?? _main;

    /// <summary>操作失败提示（机器可读 Reason → 用户文案；批次05：原型 toast）。</summary>
    private void ShowOpsError(string? reason) =>
        _main.ShowToast(reason switch
        {
            // 待办上限是**用户可设**的（设置页 1~10），必须用当前设置值；
            // 此前写死 TemplateLimit.Max(=10)，用户把上限调成 5 时提示会骗人（2026-11-06 查漏发现）
            TodoOpsService.ReasonLimitReached => $"待办数量已达上限（{_data.Settings.MaxItems} 条），无法添加",
            TodoOpsService.ReasonTemplateLimitReached => $"模板数量已达上限（{TemplateLimit.Max} 个）",
            TodoOpsService.ReasonDuplicateTemplate => "已存在相同内容的模板",
            TodoOpsService.ReasonEmptyContent => "内容不能为空",
            TodoOpsService.ReasonTooLong => "内容超过 300 字",
            _ => "操作失败",
        });

    /// <summary>＋ 新建待办（主窗右上角 ＋；空内容时新建置灰）。</summary>
    public void OpenNewTodo()
    {
        if (_data.Todos.Count >= _data.Settings.MaxItems)
        {
            ShowOpsError(TodoOpsService.ReasonLimitReached);
            return;
        }

        // 需求 §2.4：返回时"无输入直接退、有输入询问"（2026-11-06 查漏：此前只有编辑路径有询问）
        var saved = false;
        var latestText = "";

        BeginModal();
        TextEditDialog.Open(b =>
        {
            b.Title("新建待办").Placeholder("写点什么（≤300 字）")
             .LeftAction("clear", "清空", onClick: d => d.Editor.Clear())
             .Action("save", "新建", primary: true, enabled: false, autoClose: false, onClick: d =>
             {
                 var r = TodoOpsService.AddTodo(_data, d.Editor.Text, DateTimeOffset.Now);
                 if (r.Ok)
                 {
                     saved = true;
                     ApplyOps(r);
                     ShowToast("已新建");
                     d.Close();
                 }
                 else
                 {
                     ShowOpsError(r.Reason); // 达上限等：提示且不关闭
                 }
             })
             .OnTextChanged(d =>
             {
                 latestText = d.Editor.Text;
                 var ok = ContentRules.Validate(d.Editor.Text).Ok;
                 ((System.Windows.Controls.Button)((System.Windows.Controls.Panel)d.ActionsPanel).Children[0]).IsEnabled = ok;
             })
             .OnCloseRequested(d =>
             {
                 if (saved || d.Editor.Text.Trim().Length == 0)
                 {
                     return true;   // 已保存 或 空输入 → 直接关（需求：无输入直接退）
                 }

                // 有未保存输入 → 弹在编辑窗**之上**（模态），编辑窗不先消失（用户 2026-11-06 裁定）
                var outcome = ConfirmDialog.Ask("未保存的输入", "输入的内容还没保存，确认退出吗？",
                                                "保存", anchor: d, cancelText: "确认", showCloseButton: true);
                if (outcome == ConfirmDialog.Outcome.Closed)
                {
                    return false;   // 右上角 X → 关掉弹窗、回到编辑窗继续编辑
                }

                if (outcome == ConfirmDialog.Outcome.Ok)
                {
                    var r = TodoOpsService.AddTodo(_data, latestText, DateTimeOffset.Now);
                    if (!r.Ok)
                    {
                        ShowOpsError(r.Reason);   // 保存失败 → 不关闭（与"保存失败不关窗"一致）
                        return false;
                    }

                    saved = true;
                    ApplyOps(r);
                    ShowToast("已新建");
                }

                return true;   // 「确认」= 确认放弃 → 关闭编辑窗
            });
        }, this);
        EndModal();
    }

    /// <summary>编辑待办（双击行；返回时已改询问；删除有确认框）。</summary>
    public void OpenEditTodo(string id)
    {
        var item = _data.Todos.Find(t => t.Id == id);
        if (item is null)
        {
            return;
        }

        var saved = false;
        var dirty = false;
        var latestText = item.Content;

        BeginModal();
        TextEditDialog.Open(b =>
        {
            b.Title("编辑待办")
             .StateBadge(item.Overdue ? "已逾期" : "进行中",
                         $"创建于 {CreatedTimeText(item.CreatedAt)}", item.Overdue)
             .RestoreButton()
             .Text(item.Content).Placeholder("写点什么（≤300 字）")
             .LeftAction("delete", "删除", danger: true, autoClose: false, onClick: d =>
             {
                 if (ConfirmDialog.Confirm("删除待办", "确定删除这条待办吗？删除后无法恢复。", "删除", danger: true, anchor: d))
                 {
                     saved = true;
                     ApplyOps(TodoOpsService.DeleteTodo(_data, id));
                     ShowToast("已删除");
                     d.Close();
                 }
             })
             .Action("toTpl", "添加到模板", onClick: d => AddToTemplateFromEditor(d.Editor.Text, d))
             .Action("save", "保存", primary: true, autoClose: false, onClick: d =>
             {
                 var r = TodoOpsService.EditTodo(_data, id, d.Editor.Text);
                 if (!r.Ok)
                 {
                     ShowOpsError(r.Reason); // 保存失败（空内容等）：提示且不关闭
                     return;
                 }

                 saved = true;
                 ApplyOps(r);
                 ShowToast("已保存");
                 d.Close();
             })
             .OnTextChanged(d =>
             {
                 latestText = d.Editor.Text;
                 dirty = latestText.Trim() != item.Content;
             })
             .OnCloseRequested(d =>
             {
                 if (saved || !dirty)
                 {
                     return true;   // 未改过 → 直接关
                 }

                // 有未保存输入 → 弹在编辑窗**之上**（模态），编辑窗不先消失（用户 2026-11-06 裁定）
                var outcome = ConfirmDialog.Ask("未保存的修改", "输入的内容还没保存，确认退出吗？",
                                                "保存", anchor: d, cancelText: "确认", showCloseButton: true);
                if (outcome == ConfirmDialog.Outcome.Closed)
                {
                    return false;   // 右上角 X → 关掉弹窗、回到编辑窗继续编辑
                }

                if (outcome == ConfirmDialog.Outcome.Ok)
                {
                    var r = TodoOpsService.EditTodo(_data, id, latestText);
                    if (!r.Ok)
                    {
                        ShowOpsError(r.Reason);   // 保存失败 → 不关闭（与"保存失败不关窗"一致）
                        return false;
                    }

                    saved = true;
                    ApplyOps(r);
                    ShowToast("已保存");
                }

                return true;   // 「确认」= 确认放弃 → 关闭编辑窗
            });
        }, this);
        EndModal();
    }

    /// <summary>编辑待办「添加到模板」（原型 dToTpl：空内容/上限/查重提示 + 确认框）。</summary>
    private void AddToTemplateFromEditor(string text, System.Windows.Window anchor)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            ShowToast("内容为空，先写点什么");
            return;
        }

        if (_data.Templates.Count >= TemplateLimit.Max)
        {
            ShowToast($"模板已达上限（{TemplateLimit.Max} 条）");
            return;
        }

        if (_data.Templates.Any(t => t.Content == trimmed))
        {
            ShowToast("已存在相同内容");
            return;
        }

        if (!ConfirmDialog.Confirm("添加到模板？", "当前内容会保存为一条模板。", "添加", anchor: anchor))
        {
            return;
        }

        var r = TodoOpsService.AddTemplate(_data, trimmed, DateTimeOffset.Now);
        if (r.Ok)
        {
            ApplyOps(r);
            ShowToast("已添加到模板");
        }
        else
        {
            ShowOpsError(r.Reason);
        }
    }

    /// <summary>创建时间展示（原型 fmtDate + fmtHM：x月x日 星期x HH:mm）。</summary>
    private static string CreatedTimeText(string createdAtIso)
    {
        var local = IsoTime.ParseLocal(createdAtIso);
        return $"{MainViewService.CnDateShortText(local)} {local:HH:mm}";
    }

    /// <summary>点已完成行 → 只读详情窗（方案 H 420×438；删除走确认框后关闭）。</summary>
    public void OpenDoneDetail(string id)
    {
        var item = _data.Completed.Find(t => t.Id == id);
        var archived = false;
        if (item is null)
        {
            foreach (var kv in _data.Archive)
            {
                item = kv.Value.Find(t => t.Id == id);
                if (item is not null)
                {
                    archived = true;
                    break;
                }
            }
        }

        if (item is null)
        {
            return;
        }

        BeginModal();
        DetailDialog.Show(
            archived ? "历史事项" : "已完成事项",
            CreatedTimeText(item.CreatedAt),
            CreatedTimeText(item.CompletedAt ?? item.CreatedAt),
            OverdueService.IsCompletedOverdue(item),
            item.Content,
            d =>
            {
                var r = archived
                    ? TodoOpsService.DeleteArchived(_data, id)
                    : TodoOpsService.DeleteCompleted(_data, id);
                if (r.Ok)
                {
                    ApplyOps(r);
                    ShowToast("已删除");
                }

                d.Close();
            }, this);
        EndModal();
    }

    // ---------- 托盘显隐 / 退出 ----------

    public void HideClusterToTray()
    {
        EndHover();
        // 隐藏前先退出批量态（原型 minimizeToTray 口径：恢复即一般态；贴边缩进不在此列）
        if (_batch.IsActive)
        {
            _batch.Exit();
            PushBatchBar();
        }

        // 有未保存的新建/编辑内容 → 先提醒（与托盘退出同一套口径）；点取消则不隐藏
        // （用户 2026-11-06：隐藏指令也要做这个判别）
        var editor = TextEditDialog.Current;
        if (editor?.HasUnsavedInput == true
            && !ConfirmDialog.Confirm("隐藏到托盘", "有未保存的输入，隐藏后会丢失。确定要隐藏吗？",
                                      "隐藏", anchor: editor))
        {
            return;
        }

        _clusterHidden = true;
        // 隐藏**全部**应用窗口（含二级窗）。此前只隐藏 _main/_quick/_tuckBar，
        // 二级窗会留在屏幕上（用户 2026-11-06 反馈：打开二级窗双击托盘，主窗没了、其它窗还在）。
        foreach (var window in System.Windows.Application.Current.Windows
                     .OfType<System.Windows.Window>()
                     .ToList())
        {
            window.Hide();
        }
    }

    public void ShowClusterFromTray()
    {
        _clusterHidden = false;

        // 用户 2026-11-06 裁定：恢复时"一级窗之外的其他窗口直接关闭"——二级窗不再跟着回来。
        // 模板窗是一级窗（有自己的开关状态），仍按 ui.QuickVisible 恢复；这里只关对话框。
        foreach (var dialog in AppWindows.Dialogs().ToList())
        {
            dialog.Close();
        }

        ApplyLayout();
        _main.Show();
        var ui = _data.UiState;
        if (ui.QuickVisible)
        {
            _quick.Show();
        }

        if (ui.DoneVisible)
        {
            // 已完成窗已不参与布局（批次04 收尾），不再显示
        }

        // 缩进态托盘恢复：自动弹出停留 3 秒（状态机 hold 会话）
        _tuck.TrayRestoreRequested(DateTimeOffset.Now);
        ApplyTopMost(_data.UiState.TopMost);
    }

    public bool IsClusterHidden => _clusterHidden;

    public async Task ExitAsync()
    {
        _tuckTick.Stop();
        _clock.Stop();
        await _repository.FlushAsync();
        Application.Current.Shutdown();
    }

    // ---------- 多屏 ----------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var workArea = WorkArea();
        var clamped = ScreenClamp.ClampToWorkArea(
            new WinRect(_anchor.X, _anchor.Y, 0, 0), workArea);
        _anchor = new WinPoint(clamped.X, clamped.Y);
        // 只钳生效值；用户档位不动
        if (!_clusterHidden)
        {
            ApplyLayout();
        }
    }

    // ---------- 会话状态替换 + 记忆落盘（防抖由仓储层处理） ----------

    private void Mutate(Func<AppData, AppData> transform)
    {
        _data = transform(_data);
        _ = _repository.SaveAsync(_data);
    }

    public void Dispose()
    {
        _tuckTick.Stop();
        _clock.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
