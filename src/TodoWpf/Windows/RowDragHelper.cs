using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TodoWpf.Core.Services;

namespace TodoWpf.Windows;

/// <summary>
/// 列表拖拽排序（批次05①，原型拖拽口径）：
/// 只从行首点阵发起；位移超过 4 视觉 DIP 才算拖动（防手抖误触）；被拖行跟手浮起（阴影+描边），
/// 其余行 0.18s 让位滑动；被拖行钳在待办段内（不能拖进已完成区）；列表上/下缘 26 内容 px 内自动滚动；
/// Esc 取消；松手 0.16s 归位后再提交（重排后布局与松手瞬间画面重合，看不到跳变）。
/// 坐标口径：屏幕 DIP → ÷ContentScale → 内容坐标；行位置快照取未加让位变换时的布局坐标
/// （拖动中兄弟行在位移，实时量 rect 会把让位也算进去，目标槽位来回抖——原型同款处理）。
/// </summary>
internal sealed class RowDragHelper
{
    private const double LiftThreshold = 4;
    private const double ShiftMs = 180;   // 让位/归位动画（原型 .18s / .16s）
    private const double SettleMs = 170;  // 松手到提交的等待（覆盖 0.16s 归位）
    private const double AutoScrollMargin = 26;
    private const double AutoScrollStep = 7;

    private readonly MainTodoWindow _window;
    private readonly DispatcherTimer _scrollTimer;

    public RowDragHelper(MainTodoWindow window)
    {
        _window = window;
        _scrollTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(15),
        };
        _scrollTimer.Tick += (_, _) => AutoScrollTick();
    }

    // —— 拖动会话状态（End 即清空，归位提交走局部快照） ——
    private FrameworkElement? _captureElement;
    private Border? _dragCard;
    private TodoRowView? _dragRow;
    private List<Border>? _cards;
    private List<double>? _tops;
    private double _pitch;
    private double _grab;
    private double _rootTopScreen;
    private double _startMouseY;
    private double _lastMouseY;
    private int _from;
    private int _to;
    private bool _armed;

    /// <summary>行首点阵按下：快照布局位置、捕获鼠标。批量中/无数据/已在拖动时忽略。</summary>
    public void Begin(FrameworkElement grip, MouseButtonEventArgs e)
    {
        if (_captureElement is not null || _armed || _window.InBatch || _window.Controller is null)
        {
            return;
        }

        if (grip.DataContext is not TodoRowView row || _window.Rows.Count == 0)
        {
            return;
        }

        var generator = _window.TodoList.ItemContainerGenerator;
        var presenters = new List<FrameworkElement>(_window.Rows.Count);
        var cards = new List<Border>(_window.Rows.Count);
        foreach (var vm in _window.Rows)
        {
            if (generator.ContainerFromItem(vm) is not ContentPresenter presenter
                || FindRowCard(presenter) is not { } card)
            {
                return;
            }

            presenters.Add(presenter);
            cards.Add(card);
        }

        var from = -1;
        for (var i = 0; i < _window.Rows.Count; i++)
        {
            if (_window.Rows[i].Id == row.Id)
            {
                from = i;
                break;
            }
        }

        if (from < 0 || VisualTreeHelper.GetParent(presenters[0]) is not Panel host)
        {
            return;
        }

        // 位置快照（未加让位变换的布局坐标）；行距按 pitch 走，否则让位会留出/叠掉行间距
        var tops = presenters.Select(p => p.TranslatePoint(new Point(0, 0), host).Y).ToList();
        var dpi = VisualTreeHelper.GetDpi(_window);
        var scale = _window.ContentScaleFactor;
        var offset = _window.ListScroll.VerticalOffset;

        // 内容坐标系原点基准：把"host 当前在屏幕上的位置"折回当前滚动偏移，
        // 得到与滚动无关的基准（此后指针内容坐标 = (屏幕Y − 基准) ÷ scale + offset）
        var hostTopDevice = host.PointToScreen(new Point(0, 0));
        _rootTopScreen = hostTopDevice.Y / dpi.DpiScaleY + offset * scale;

        var mouseScreenY = ScreenY(e) ?? 0;
        _cards = cards;
        _tops = tops;
        _dragRow = row;
        _dragCard = cards[from];
        _from = from;
        _to = from;
        _pitch = _window.Rows.Count > 1 ? tops[1] - tops[0] : _dragCard.ActualHeight + 4;
        _grab = Math.Max(0, (mouseScreenY - _rootTopScreen) / scale + offset - tops[from]);
        _startMouseY = _lastMouseY = mouseScreenY;

        _captureElement = grip;
        grip.CaptureMouse();
        grip.MouseMove += Grip_MouseMove;
        grip.MouseUp += Grip_MouseUp;
        grip.LostMouseCapture += Grip_LostCapture;
        _window.PreviewKeyDown += Window_PreviewKeyDown;

        e.Handled = true;
    }

    // ---------- 指针/键盘事件 ----------

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _lastMouseY = ScreenY(e) ?? _lastMouseY;
        Paint();
    }

    private void Grip_MouseUp(object sender, MouseButtonEventArgs e) => End(commit: true);

    private void Grip_LostCapture(object sender, MouseEventArgs e) => End(commit: false);

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _armed)
        {
            e.Handled = true;
            End(commit: false);
        }
    }

    // ---------- 拖动画面 ----------

    private void Paint()
    {
        if (!_armed)
        {
            if (Math.Abs(_lastMouseY - _startMouseY) < LiftThreshold)
            {
                return;
            }

            _armed = true;
            ArmVisual();
            _scrollTimer.Start();
        }

        var scale = _window.ContentScaleFactor;
        var rowH = _dragCard!.ActualHeight;
        var cy = (_lastMouseY - _rootTopScreen) / scale + _window.ListScroll.VerticalOffset;

        // 被拖行的虚拟顶边钳在待办段内
        var want = Math.Min(Math.Max(cy - _grab, _tops![0]), _tops[^1]);
        _dragCard!.RenderTransform = new TranslateTransform(want - _tops[_from], 0);

        // 中心线换位（含钳到首/末槽）
        var vc = want + rowH / 2;
        var t = _from;
        for (var i = 0; i < _cards!.Count; i++)
        {
            var c = _tops[i] + rowH / 2;
            if (i < _from && vc <= c)
            {
                t = Math.Min(t, i);
            }
            else if (i > _from && vc >= c)
            {
                t = Math.Max(t, i);
            }
        }

        if (t == _to)
        {
            return;
        }

        _to = t;
        ShiftSiblings();
    }

    private void ArmVisual()
    {
        Panel.SetZIndex(_dragCard!, 6);
        _dragCard!.Effect = new DropShadowEffect
        {
            BlurRadius = 22,
            ShadowDepth = 8,
            Direction = 270,
            Opacity = 0.35,
        };
        _dragCard.BorderBrush = (Brush)_window.FindResource("Brush.CardBorder");
    }

    private void ShiftSiblings()
    {
        for (var i = 0; i < _cards!.Count; i++)
        {
            if (i == _from)
            {
                continue;
            }

            double s = 0;
            if (_to > _from && i > _from && i <= _to)
            {
                s = -_pitch;
            }
            else if (_to < _from && i >= _to && i < _from)
            {
                s = _pitch;
            }

            AnimateShift(EnsureTranslate(_cards[i]), s);
        }
    }

    private static void AnimateShift(TranslateTransform translate, double target)
    {
        var anim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ShiftMs) };
        anim.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(ShiftMs),
            Value = target,
            KeySpline = new KeySpline(0.22, 0.61, 0.36, 1),
        });
        translate.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private static TranslateTransform EnsureTranslate(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform t)
        {
            return t;
        }

        t = new TranslateTransform();
        element.RenderTransform = t;
        return t;
    }

    // ---------- 自动滚动 ----------

    private void AutoScrollTick()
    {
        if (!_armed || _dragCard is null)
        {
            _scrollTimer.Stop();
            return;
        }

        var scroll = _window.ListScroll;
        var before = scroll.VerticalOffset;
        var scale = _window.ContentScaleFactor;
        var cy = (_lastMouseY - _rootTopScreen) / scale + before;

        double next;
        if (cy < before + AutoScrollMargin)
        {
            next = before - AutoScrollStep;
        }
        else if (cy > before + scroll.ViewportHeight - AutoScrollMargin)
        {
            next = before + AutoScrollStep;
        }
        else
        {
            return;
        }

        scroll.ScrollToVerticalOffset(next); // ScrollViewer 自行钳回 [0, ScrollableHeight]
        Paint(); // 滚动改变指针内容坐标，重算一次
    }

    // ---------- 收尾 ----------

    private void End(bool commit)
    {
        if (_captureElement is null)
        {
            return;
        }

        var grip = _captureElement;
        grip.MouseMove -= Grip_MouseMove;
        grip.MouseUp -= Grip_MouseUp;
        grip.LostMouseCapture -= Grip_LostCapture;
        _window.PreviewKeyDown -= Window_PreviewKeyDown;
        if (grip.IsMouseCaptured)
        {
            grip.ReleaseMouseCapture();
        }

        _captureElement = null;
        _scrollTimer.Stop();

        // 会话状态取局部快照（归位计时器期间可再起新拖动）
        var card = _dragCard;
        var row = _dragRow;
        var cards = _cards;
        var tops = _tops;
        var from = _from;
        var target = commit ? _to : _from;
        var armed = _armed;
        _dragCard = null;
        _dragRow = null;
        _cards = null;
        _tops = null;
        _armed = false;
        _from = 0;
        _to = 0;

        if (!armed || card is null || cards is null || tops is null || row is null)
        {
            return; // 只点了一下点阵：什么都没发生
        }

        // 被拖行 0.16s 滑到目标槽（取消则滑回原位）；取消/原地放下时让位行也滑回
        var translate = EnsureTranslate(card);
        var settle = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(160) };
        settle.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(160),
            Value = tops[target] - tops[from],
            KeySpline = new KeySpline(0.22, 0.61, 0.36, 1),
        });
        translate.BeginAnimation(TranslateTransform.YProperty, settle);

        var landed = commit && target != from;
        if (!landed)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (i != from)
                {
                    AnimateShift(EnsureTranslate(cards[i]), 0);
                }
            }
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SettleMs),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Panel.SetZIndex(card, 0);
            card.Effect = null;
            card.BorderBrush = (Brush)_window.FindResource("Brush.RowCardBorder");
            if (landed)
            {
                _window.Controller?.MoveTodo(row.Id, target);
            }
            else
            {
                card.RenderTransform = Transform.Identity;
                foreach (var c in cards)
                {
                    c.RenderTransform = Transform.Identity;
                }
            }
        };
        timer.Start();
    }

    // ---------- 工具 ----------

    /// <summary>指针屏幕 Y（DIP）。PointToScreen 是物理像素，按窗口 DPI 换算。</summary>
    private double? ScreenY(MouseEventArgs e)
    {
        try
        {
            var device = _window.PointToScreen(e.GetPosition(_window));
            var dpi = VisualTreeHelper.GetDpi(_window);
            return device.Y / dpi.DpiScaleY;
        }
        catch (InvalidOperationException)
        {
            return null; // 窗口句柄不可用（极端时序）
        }
    }

    private static Border? FindRowCard(DependencyObject container)
    {
        // 行模板：ContentPresenter → Border（RowCard）——首个 Border 即行卡
        var node = container;
        while (node is not null)
        {
            if (node is Border border)
            {
                return border;
            }

            node = VisualTreeHelper.GetChildrenCount(node) > 0 ? VisualTreeHelper.GetChild(node, 0) : null;
        }

        return null;
    }
}
