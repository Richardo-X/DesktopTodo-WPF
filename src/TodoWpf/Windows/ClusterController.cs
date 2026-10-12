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

        _main.Show();
        _quick.Show();
        _done.Show();
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

    /// <summary>按锚点+缩放+开关重排三窗（隐藏窗同时就位但不出场）。</summary>
    public void ApplyLayout(bool saveState = true)
    {
        var ui = _data.UiState;
        var workArea = WorkArea();
        var frame = ClusterLayout.Arrange(workArea, _anchor, _scale, ui.QuickVisible, ui.DoneVisible);

        PlaceBatch(
            (frame.Main.Rect, _main, true, PinEdge.Left),
            (frame.Quick.Rect, _quick, frame.Quick.Visible, PinEdge.Right),
            (frame.Done.Rect, _done, frame.Done.Visible, PinEdge.Left));

        _main.SetContentScale(_scale);
        _quick.SetContentScale(_scale);
        _done.SetContentScale(_scale);

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
        var frame = ClusterLayout.Arrange(WorkArea(), _anchor, _scale, ui.QuickVisible, ui.DoneVisible);

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
        var batch = new List<(Window Window, WinRect Rect, PinEdge Pin)>(panels.Length);
        foreach (var (rect, window, visible, pin) in panels)
        {
            if (visible)
            {
                batch.Add((window, rect, pin));
                if (!window.IsVisible)
                {
                    window.Show();
                }
            }
            else if (window.IsVisible)
            {
                window.Hide();
            }
        }

        WindowPlacement.SetBoundsBatch(batch);
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
        SetResizeVisual(frozen: true);
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
        SetResizeVisual(frozen: false);
        ApplyLayout(); // 松手：完整布局 + 落盘
    }

    /// <summary>
    /// 缩放期间的视觉降级（对齐成品口径"按下冻结两侧窗内容、只看窗壳"，并抑制透明窗逐帧合成拖影）：
    /// ① 三窗卡面切为不透明底；② 两侧窗内容冻结（待办主窗内容保留，便于看缩放结果）。
    /// </summary>
    private void SetResizeVisual(bool frozen)
    {
        var key = frozen ? "Brush.WinBgSolid" : "Brush.WinBg";
        var background = (Brush)Application.Current.Resources[key];
        _main.Card.Background = background;
        _quick.Card.Background = background;
        _done.Card.Background = background;
        _quick.CardBody.Visibility = frozen ? Visibility.Hidden : Visibility.Visible;
        _done.CardBody.Visibility = frozen ? Visibility.Hidden : Visibility.Visible;
    }

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
        var frame = ClusterLayout.Arrange(workArea, _anchor, _scale, ui.QuickVisible, ui.DoneVisible);

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
