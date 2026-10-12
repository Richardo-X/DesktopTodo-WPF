using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
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
    private static readonly string[] Weekdays = ["日", "一", "二", "三", "四", "五", "六"];

    private AppData _data;
    private readonly JsonFileRepository _repository;

    private readonly MainTodoWindow _main;
    private readonly QuickWindow _quick;
    private readonly DoneWindow _done;
    private readonly TuckBarWindow _tuckBar;
    private readonly TuckStateMachine _tuck = new();

    private readonly DispatcherTimer _tuckTick;
    private readonly DispatcherTimer _clock;

    private WinPoint _anchor;
    private double _scale = 1;
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
    private Point _dragStartMouse;

    public ClusterController(AppData data, JsonFileRepository repository)
    {
        _data = data;
        _repository = repository;

        _main = new MainTodoWindow();
        _quick = new QuickWindow();
        _done = new DoneWindow();
        _tuckBar = new TuckBarWindow();

        // 不进任务栏/Alt+Tab（需求 §2.2）：ShowInTaskbar=false 只管任务栏，Alt+Tab 需 WS_EX_TOOLWINDOW
        foreach (var window in (Window[])[_main, _quick, _done, _tuckBar])
        {
            ToolWindowChrome.Apply(window);
        }


        _scale = _data.UiState.ClusterScale;
        _anchor = ResolveAnchor();

        _main.TitlebarPressed += OnTitlebarPressed;
        _quick.TitlebarPressed += OnTitlebarPressed;
        _done.TitlebarPressed += OnTitlebarPressed;
        _main.ScaleGripHandle.MouseLeftButtonDown += ScaleGrip_MouseDown;
        _tuckBar.BarHit += (_, _) => OnPointerEnter();

        // 指针进出 → 状态机（窗口级，被遮挡时系统不派发 = 真实命中语义）
        foreach (var window in (Window[])[_main, _quick, _done])
        {
            window.MouseLeave += (_, _) => _tuck.NotifyPointerLeft(DateTimeOffset.Now);
            window.MouseEnter += (_, _) => OnPointerEnter();
        }

        // Alt+F4 = 强制退出整个应用（退出前数据落盘；×=托盘隐藏用 Hide，不触发 Closing）
        foreach (var window in (Window[])[_main, _quick, _done, _tuckBar])
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

    public DoneWindow Done => _done;

    /// <summary>初始化：应用记忆、接互锁、显示窗口。</summary>
    public void Start()
    {
        ApplyThemeState();
        _main.Controller = this;
        _quick.Controller = this;
        _done.Controller = this;
        _tuckBar.Controller = this;

        // 焦点描边仅键盘导航显示（L1 明细；鼠标点击不留蓝框）
        foreach (var window in (Window[])[_main, _quick, _done])
        {
            FocusVisualScope.Attach(window);
        }

        _tuck.SetLocked(_data.UiState.TuckLock);
        _main.SetTuckLock(_data.UiState.TuckLock);
        _main.SetTopMost(_data.UiState.TopMost);

        ApplyTopMost(_data.UiState.TopMost);
        UpdateClock();
        ApplyLayout();

        // 显隐交给 ApplyLayout（按存档的面板开关决定）——此前三个窗口无条件 Show()，
        // 会把"已关闭"的面板以 WPF 默认尺寸（1536×844）显示出来（Viewbox 又把内容放大填满）= 启动巨窗 bug。
        _main.SetPanelToggles(_data.UiState.QuickVisible, _data.UiState.DoneVisible);

    }

    // ---------- 布局 ----------

    private WinPoint ResolveAnchor()
    {
        var ui = _data.UiState;
        if (ui.Pos is { } pos)
        {
            return new WinPoint(pos.X, pos.Y);
        }

        var scale = ClusterLayout.ClampScale(_scale, WorkArea(), ui.QuickVisible, ui.DoneVisible);
        _scale = scale;
        return ClusterLayout.FirstLaunchAnchor(WorkArea(), scale, ui.QuickVisible, ui.DoneVisible);
    }

    private static WinRect WorkArea()
    {
        var wa = SystemParameters.WorkArea;
        return new WinRect(wa.Left, wa.Top, wa.Width, wa.Height);
    }

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
    /// 快捷窗的"画布矩形"：窗口矩形恒定不变的那块地方（右缘钉主窗左缘减缝隙、上缘钉锚点）。
    /// 尺寸取 max(设计尺寸, 设计尺寸×缩放上限) —— 保证卡面（固定设计尺寸 310×640，靠渲染层缩放表现大小）
    /// 在窗口内**不被约束**：否则卡面被父级宽度夹小，渲染层缩放的锚点算错，卡会缩到左下角去。
    /// 窗口矩形永不改变 ⇒ 合成器不会重贴旧表面（无拖影、无按下/松手闪动）。
    /// </summary>
    private WinRect QuickCanvasRect()
    {
        var ui = _data.UiState;
        var workArea = WorkArea();
        var maxScale = ClusterLayout.MaxScale(workArea, ui.QuickVisible, ui.DoneVisible);
        var w = ClusterLayout.SideWidth * Math.Max(1, maxScale);
        var h = ClusterLayout.SideHeight * Math.Max(1, maxScale);
        var right = SnappedAnchor().X - ClusterLayout.Gap;
        return new WinRect(right - w, SnappedAnchor().Y, w, h);
    }

    /// <summary>按锚点+缩放+开关重排三窗（隐藏窗同时就位但不出场）。</summary>
    public void ApplyLayout(bool saveState = true)
    {
        var ui = _data.UiState;
        var workArea = WorkArea();
        var frame = ClusterLayout.Arrange(workArea, SnappedAnchor(), _scale, ui.QuickVisible, ui.DoneVisible);

        // 快捷窗恒用"画布矩形"（= 上限档的尺寸，右缘与目标档同一条物理边）：窗口矩形全程不变，
        // 只靠内容缩放表现大小变化 → 合成器无需重贴旧表面（无拖影、按下/松手也无闪动）。
        PlaceBatch(
            (frame.Main.Rect, _main, true, PinEdge.Left),
            (QuickCanvasRect(), _quick, frame.Quick.Visible, PinEdge.Right),
            (frame.Done.Rect, _done, frame.Done.Visible, PinEdge.Left));

        // 内容缩放比不从 _scale 直接取，而是从"窗口矩形取整后的实际尺寸"反推：
        // 窗口宽度要落到整数物理像素（PlaceBatch 里 round），若内容仍按原始 _scale 排版，
        // 两者会差零点几像素——内容比窗口宽 ⇒ 右对齐的卡面被裁掉一条；比窗口窄 ⇒
        // 卡面右缘缩进窗内，肉眼就是"两窗缝隙在缩放中变宽/变窄"（用户视频慢放实证：缝隙 13→17→13）。
        // 反推后 内容布局尺寸 ≡ 窗口客户区尺寸，卡面边缘逐帧与窗口边缘重合。
        // 拖动期间快捷窗内容不参与重绘（基准「缩放呈现 v3.3」口径：只看窗壳缩放，松手恢复内容）。
        // 目的：把每帧要重绘+推送的像素量压到最小，让它的尺寸变化跟上主窗的节拍。
        _quick.SetContentVisible(!HideQuickContentForTest && !_resizing);
        _main.SetContentScale(NormalizedScale(frame.Main.Rect.Width, ClusterLayout.MainWidth));
        _quick.SetContentScale(NormalizedScale(frame.Quick.Rect.Width, ClusterLayout.SideWidth));
        _done.SetContentScale(NormalizedScale(frame.Done.Rect.Width, ClusterLayout.SideWidth));

        // 窗口矩形已在本帧落位；这里把内容布局也同步推完，避免"内容晚窗口一帧"
        // （内容若晚一帧，右对齐的快捷窗右缘与两窗缝隙就会被看见在动）。
        if (_resizing)
        {
            _main.UpdateLayout();
            _quick.UpdateLayout();
            _done.UpdateLayout();
        }

        // 白条定位：可见组水平中心、贴屏顶
        var left = frame.Quick.Visible ? frame.Quick.Rect.X : frame.Main.Rect.X;
        var right = frame.Done.Visible ? frame.Done.Rect.Right : frame.Main.Rect.Right;
        _tuckBar.Left = (left + right) / 2 - _tuckBar.Width / 2;
        _tuckBar.Top = workArea.Y;

        if (saveState)
        {
            Mutate(d => d with
            {
                UiState = d.UiState with
                {
                    Pos = new Position { X = _anchor.X, Y = _anchor.Y },
                    ClusterScale = _scale,
                },
            });
        }
    }

    /// <summary>拖动快路径：只同步可见窗的坐标（不重设宽高、不切显隐、不落盘）。</summary>
    private void MoveCluster()
    {
        var ui = _data.UiState;
        var frame = ClusterLayout.Arrange(WorkArea(), SnappedAnchor(), _scale, ui.QuickVisible, ui.DoneVisible);

        PlaceBatch(
            (frame.Main.Rect, _main, true, PinEdge.Left),
            (frame.Quick.Rect, _quick, frame.Quick.Visible, PinEdge.Right),
            (frame.Done.Rect, _done, frame.Done.Visible, PinEdge.Left));

        var left = frame.Quick.Visible ? frame.Quick.Rect.X : frame.Main.Rect.X;
        var right = frame.Done.Visible ? frame.Done.Rect.Right : frame.Main.Rect.Right;
        var barLeft = (left + right) / 2 - _tuckBar.Width / 2;
        if (_tuckBar.IsVisible && _tuckBar.Left != barLeft) { _tuckBar.Left = barLeft; }
    }

    /// <summary>
    /// 三窗几何同批提交（DeferWindowPos）：避免逐窗提交被系统在两窗之间插入呈现，
    /// 造成"同一帧里三窗位置/尺寸不同步"；显隐状态另行处理（仅变化时切换）。
    /// </summary>
    private static void PlaceBatch(params (WinRect Rect, Window Window, bool Visible, PinEdge Pin)[] panels)
    {
        var atomic = new List<(Window Window, WinRect Rect, PinEdge Pin)>(panels.Length);
        var toShow = new List<Window>(panels.Length);
        var toHide = new List<Window>(panels.Length);

        foreach (var (rect, window, visible, pin) in panels)
        {
            if (visible)
            {
                if (window.IsVisible)
                {
                    // 已显示：走原子批量。逐帧路径上**不**写 WPF 的 Left/Top/Width/Height——
                    // 那会额外发出 4 次非原子 SetWindowPos（先位置后尺寸），把"新位置 + 旧尺寸"
                    // 的中间态暴露给 DWM 合成器（叠层窗口按新矩形贴旧表面 ⇒ 右缘被拉左的假象）。
                    // WPF 自己会跟随 WM_SIZE 同步这些属性值。
                    atomic.Add((window, rect, pin));
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

    // ---------- 整簇拖动（手写：MouseCapture 下同步三窗，DragMove 无法联动） ----------

    private void OnTitlebarPressed(object? sender, MouseButtonEventArgs e)
    {
        if (_dragging || sender is not Window window)
        {
            return;
        }

        _dragging = true;
        _dragStartAnchor = _anchor;
        _dragStartMouse = ToScreen(window, e);
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

        var mouse = ToScreen(window, e);
        var anchor = new WinPoint(
            _dragStartAnchor.X + mouse.X - _dragStartMouse.X,
            _dragStartAnchor.Y + mouse.Y - _dragStartMouse.Y);

        // 上边缘吸附：顶边真正触碰屏幕上缘 → 贴齐 y=工作区顶
        if (anchor.Y <= WorkArea().Y)
        {
            anchor = anchor with { Y = WorkArea().Y };
        }

        _anchor = anchor;
        MoveCluster(); // 快路径：只改坐标，不重排、不落盘（抖动治理）
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
        var targetMainWidth = Math.Max(
            ClusterLayout.MainWidth * ClusterLayout.MinScale,
            _resizeStartMainWidth + (mouse.X - _resizeStartMouse.X));
        var ui = _data.UiState;
        _scale = ClusterLayout.ClampScale(targetMainWidth / ClusterLayout.MainWidth, WorkArea(), ui.QuickVisible, ui.DoneVisible);
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

    /// <summary>
    /// 【临时测试开关】屏蔽快捷添加窗的内容层——只留卡面底色 + 1px 描边，用来对照
    /// "内容重绘"对拖缩放时观感的影响。测完改回 false（或改为"仅拖动期间"）。
    /// </summary>
    private const bool HideQuickContentForTest = false;

    private void SetResizeVisual(bool resizing)
    {
        var opacity = HideSideWindowsDuringResize && resizing ? 0d : 1d;
        _quick.Opacity = opacity;
        _done.Opacity = opacity;
    }

    /// <summary>置 true 可在拖缩放柄时把每帧几何写盘（临时目录下的 resize-trace.log），用于定位“边缘没钉住”。</summary>
    public static bool TraceResize;

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

            if (TraceResize)
            {
                var winRight = WindowPlacement.PhysicalRight(_quick);
                var cardRight = _quick.CardRightInWindowDip();
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "resize-trace.log"),
                        $"scale={_scale:F4} "
                        + $"quickWinRightPhysical={winRight} quickCardRightDip={cardRight:F2} quickCardW={_quick.Card.Width:F1}"
                        + Environment.NewLine);
                }
                catch { }
            }
        };
        CompositionTarget.Rendering += handler;
    }



    // ---------- 面板开关 ----------

    public void ToggleQuick()
    {
        Mutate(d => d with { UiState = d.UiState with { QuickVisible = !d.UiState.QuickVisible } });
        _main.SetPanelToggles(_data.UiState.QuickVisible, _data.UiState.DoneVisible);
        ApplyLayout();
    }

    public void ToggleDone()
    {
        Mutate(d => d with { UiState = d.UiState with { DoneVisible = !d.UiState.DoneVisible } });
        _main.SetPanelToggles(_data.UiState.QuickVisible, _data.UiState.DoneVisible);
        ApplyLayout();
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
        _done.Topmost = on;
        _tuckBar.Topmost = on;
    }

    public void ToggleTuckLock()
    {
        Mutate(d => d with { UiState = d.UiState with { TuckLock = !d.UiState.TuckLock } });
        _tuck.SetLocked(_data.UiState.TuckLock);
        _main.SetTuckLock(_data.UiState.TuckLock);
    }

    public void ToggleTheme()
    {
        App.ApplyTheme(!App.IsNight);
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
        var frame = ClusterLayout.Arrange(workArea, SnappedAnchor(), _scale, ui.QuickVisible, ui.DoneVisible);

        var targets = new List<(Window Window, double DockedTop, double HiddenTop)>();
        if (frame.Main.Visible)
        {
            targets.Add((_main, frame.Main.Rect.Y, workArea.Y - frame.Main.Rect.Height - 8));
        }

        if (frame.Quick.Visible)
        {
            targets.Add((_quick, frame.Quick.Rect.Y, workArea.Y - frame.Quick.Rect.Height - 8));
        }

        if (frame.Done.Visible)
        {
            targets.Add((_done, frame.Done.Rect.Y, workArea.Y - frame.Done.Rect.Height - 8));
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
                _tuck.NotifyUntuckCompleted();
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
            _done.Show();
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
        var now = DateTime.Now;
        _main.SetClock(now.ToString(App.TimeFormat), $"{now.Year}年{now.Month}月{now.Day}日 星期{Weekdays[(int)now.DayOfWeek]}");
    }

    // ---------- 托盘显隐 / 退出 ----------

    public void HideClusterToTray()
    {
        _clusterHidden = true;
        _main.Hide();
        _quick.Hide();
        _done.Hide();
        _tuckBar.Hide();
    }

    public void ShowClusterFromTray()
    {
        _clusterHidden = false;
        ApplyLayout();
        _main.Show();
        var ui = _data.UiState;
        if (ui.QuickVisible)
        {
            _quick.Show();
        }

        if (ui.DoneVisible)
        {
            _done.Show();
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
        _scale = ClusterLayout.ClampScale(_scale, workArea, _data.UiState.QuickVisible, _data.UiState.DoneVisible);
        var clamped = ScreenClamp.ClampToWorkArea(
            new WinRect(_anchor.X, _anchor.Y, 0, 0), workArea);
        _anchor = new WinPoint(clamped.X, clamped.Y);
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
