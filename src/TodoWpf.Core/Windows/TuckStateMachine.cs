namespace TodoWpf.Core.Windows;

/// <summary>状态机向执行层（UI）发出的指令；动画由执行层实施并回报完成。</summary>
public enum TuckCommand
{
    /// <summary>整组上滑缩进（执行层播动画，完成后回报 NotifyTuckCompleted）。</summary>
    Tuck,

    /// <summary>整组滑回弹出（执行层播动画，完成后回报 NotifyUntuckCompleted）。</summary>
    Untuck,

    /// <summary>开始 3 秒弹出停留会话（托盘恢复触发）。</summary>
    StartHold,

    /// <summary>结束弹出停留会话。</summary>
    EndHold,
}

/// <summary>
/// 贴边缩进状态机（R1 语义，纯逻辑无 UI 依赖）：
/// 吸附才缩；指针离开约半秒（默认 500ms）缩进；碰到白条（指针进入）弹出；
/// 锁定/二级窗/拖动/缩放期间不缩进；锁定开启立即弹出；
/// 脱离吸附立即弹出；托盘恢复自动弹出并停留 3 秒（到期指针不在组件上则缩回，二级窗打开则取消会话）；
/// 动画防重入：指令发出后到执行层回报完成之前不再发新指令。
/// </summary>
public sealed class TuckStateMachine
{
    /// <summary>指针离开到触发缩进的延时（"约半秒"）。</summary>
    public static readonly TimeSpan TuckDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>托盘恢复后的弹出停留时长。</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(3);

    private readonly Queue<TuckCommand> _commands = new();
    private readonly object _gate = new();

    private bool _snapped;
    private bool _locked;
    private bool _childOpen;
    private bool _dragging;
    private bool _resizing;
    private bool _pointerInside;
    private bool _animating;
    private bool _tucked;
    private DateTimeOffset? _pendingTuckAt;
    private DateTimeOffset? _holdUntil;

    // ---------- 输入（门槛开关） ----------

    public void SetSnapped(bool snapped)
    {
        lock (_gate)
        {
            if (_snapped == snapped)
            {
                return;
            }

            _snapped = snapped;
            if (snapped)
            {
                return;
            }

            // 脱离吸附：取消待缩；已缩进/会话中 → 立即弹回
            _pendingTuckAt = null;
            _holdUntil = null;
            if (_tucked && !_animating)
            {
                Issue(TuckCommand.Untuck);
            }
        }
    }

    public void SetLocked(bool locked)
    {
        lock (_gate)
        {
            _locked = locked;
            if (locked)
            {
                _pendingTuckAt = null;
                var hadHold = _holdUntil is not null;
                _holdUntil = null;
                if (_tucked && !_animating)
                {
                    Issue(TuckCommand.Untuck);
                }

                if (hadHold)
                {
                    Issue(TuckCommand.EndHold);
                }
            }
        }
    }

    public void SetChildOpen(bool open)
    {
        lock (_gate)
        {
            _childOpen = open;
            if (open)
            {
                _pendingTuckAt = null;
                if (_holdUntil is not null)
                {
                    _holdUntil = null;
                    Issue(TuckCommand.EndHold);
                }
            }
        }
    }

    public void SetDragging(bool dragging)
    {
        lock (_gate)
        {
            _dragging = dragging;
            if (dragging)
            {
                _pendingTuckAt = null;
            }
        }
    }

    public void SetResizing(bool resizing)
    {
        lock (_gate)
        {
            _resizing = resizing;
            if (resizing)
            {
                _pendingTuckAt = null;
            }
        }
    }

    public void SetPointerInside(bool inside)
    {
        lock (_gate)
        {
            _pointerInside = inside;
            if (inside)
            {
                _pendingTuckAt = null;
            }
        }
    }

    // ---------- 事件 ----------

    /// <summary>指针离开组件（执行层在窗口 MouseLeave 时调用）。</summary>
    public void NotifyPointerLeft(DateTimeOffset at)
    {
        lock (_gate)
        {
            if (CanTuck())
            {
                _pendingTuckAt = at + TuckDelay;
            }
        }
    }

    /// <summary>指针进入组件/白条（真实命中）。</summary>
    public void NotifyPointerEntered(DateTimeOffset at)
    {
        lock (_gate)
        {
            _pendingTuckAt = null;
            if (_tucked && !_animating)
            {
                Issue(TuckCommand.Untuck);
            }
        }
    }

    /// <summary>执行层回报：缩进动画完成。</summary>
    public void NotifyTuckCompleted()
    {
        lock (_gate)
        {
            _animating = false;
            _tucked = true;
        }
    }

    /// <summary>执行层回报：弹出动画完成。</summary>
    public void NotifyUntuckCompleted()
    {
        lock (_gate)
        {
            _animating = false;
            _tucked = false;
        }
    }

    /// <summary>托盘恢复显示（缩进态下自动弹出并停留 3 秒）。</summary>
    public void TrayRestoreRequested(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_tucked && !_animating)
            {
                _holdUntil = now + HoldDuration;
                Issue(TuckCommand.Untuck);
                Issue(TuckCommand.StartHold);
            }
        }
    }

    /// <summary>周期心跳（UI 定时器 ~100ms 调用；时间由调用方注入）。</summary>
    public void Tick(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_animating)
            {
                return;
            }

            // 缩进判定
            if (_pendingTuckAt is { } tuckAt && now >= tuckAt)
            {
                _pendingTuckAt = null;
                if (CanTuck() && !_tucked)
                {
                    Issue(TuckCommand.Tuck);
                    return;
                }
            }

            // 3 秒会话到期收尾
            if (_holdUntil is { } holdUntil && now >= holdUntil && !_pointerInside && !_tucked)
            {
                _holdUntil = null;
                if (CanTuck())
                {
                    Issue(TuckCommand.Tuck);
                    Issue(TuckCommand.EndHold);
                }
            }
        }
    }

    /// <summary>取走全部待执行指令（执行层每轮 Drain）。</summary>
    public TuckCommand[] DrainCommands()
    {
        lock (_gate)
        {
            var list = _commands.ToArray();
            _commands.Clear();
            return list;
        }
    }

    // ---------- 内部 ----------

    private bool CanTuck() => _snapped && !_locked && !_childOpen && !_dragging && !_resizing;

    private void Issue(TuckCommand command)
    {
        if (command is TuckCommand.Tuck or TuckCommand.Untuck)
        {
            _animating = true;
        }

        _commands.Enqueue(command);
    }
}
