using TodoWpf.Core.Windows;

namespace TodoWpf.Tests;

/// <summary>
/// 贴边缩进状态机（批次 03，R1 语义）：
/// 吸附才缩；离开约半秒缩进；碰到白条弹出；锁定/二级窗/拖动/缩放不缩进；
/// 托盘恢复自动弹出停 3 秒；动画由 UI 执行并回报完成（状态机防重入）。
/// </summary>
public class TuckStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private static TuckStateMachine NewMachine() => new();

    // ---------- 门槛 ----------

    [Fact]
    public void PointerLeave_BelowDelay_NoCommand()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(300));

        Assert.Empty(m.DrainCommands());
    }

    [Fact]
    public void PointerLeave_AfterHalfSecond_CommandsTuck()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));

        Assert.Equal([TuckCommand.Tuck], m.DrainCommands());
    }

    [Fact]
    public void CannotTuck_WhenLocked_Dragging_Resizing_OrChildOpen()
    {
        foreach (var block in new[]
                 {
                     new Action<TuckStateMachine>(s => s.SetLocked(true)),
                     new Action<TuckStateMachine>(s => s.SetDragging(true)),
                     new Action<TuckStateMachine>(s => s.SetResizing(true)),
                     new Action<TuckStateMachine>(s => s.SetChildOpen(true)),
                 })
        {
            var m = NewMachine();
            m.SetSnapped(true);
            block(m);
            m.NotifyPointerLeft(T0);
            m.Tick(T0.AddSeconds(5));

            Assert.Empty(m.DrainCommands());
        }
    }

    [Fact]
    public void CannotTuck_WhenNotSnapped()
    {
        var m = NewMachine();
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddSeconds(5));

        Assert.Empty(m.DrainCommands());
    }

    [Fact]
    public void PointerBackInside_CancelsPendingTuck()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.SetPointerInside(true);
        m.Tick(T0.AddSeconds(5));

        Assert.Empty(m.DrainCommands());
    }

    // ---------- 动画门与完成回报 ----------

    [Fact]
    public void Tick_DoesNotRetriggerWhileAnimating()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));
        Assert.Equal([TuckCommand.Tuck], m.DrainCommands());

        // 动画进行中（未回报完成）：继续离开也不重复触发
        m.Tick(T0.AddSeconds(2));
        Assert.Empty(m.DrainCommands());

        // 完成回报后进入 Tucked；再次离开不再触发（已缩进）
        m.NotifyTuckCompleted();
        m.Tick(T0.AddSeconds(3));
        Assert.Empty(m.DrainCommands());
    }

    [Fact]
    public void PointerEntered_WhileTucked_CommandsUntuck()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));
        m.DrainCommands();
        m.NotifyTuckCompleted();

        m.NotifyPointerEntered(T0.AddSeconds(1));

        Assert.Equal([TuckCommand.Untuck], m.DrainCommands());
    }

    [Fact]
    public void PointerEntered_WhileAnimating_IsIgnored()
    {
        var m = NewMachine();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));
        m.DrainCommands(); // 动画开始（未回报完成）

        m.NotifyPointerEntered(T0.AddSeconds(1));

        Assert.Empty(m.DrainCommands());
    }

    // ---------- 锁定 ----------

    [Fact]
    public void LockOn_WhileTucked_CommandsUntuck_AndCancelsHold()
    {
        var m = NewMachine();
        ForceTucked(m);

        m.SetLocked(true);

        // 无托盘会话：只发弹回
        Assert.Equal([TuckCommand.Untuck], m.DrainCommands());

        // 有托盘会话：弹回 + 结束会话
        m.SetLocked(false);
        m.DrainCommands();
        m.NotifyUntuckCompleted();
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));
        Assert.Equal([TuckCommand.Tuck], m.DrainCommands());
        m.NotifyTuckCompleted();
        m.TrayRestoreRequested(T0);
        Assert.Equal([TuckCommand.Untuck, TuckCommand.StartHold], m.DrainCommands());
        m.NotifyUntuckCompleted();

        // 弹出已完成（未缩进）：锁定只结束会话
        m.SetLocked(true);
        Assert.Equal([TuckCommand.EndHold], m.DrainCommands());
    }

    // ---------- 脱离吸附 ----------

    [Fact]
    public void SnapReleased_WhileTucked_CommandsUntuck()
    {
        var m = NewMachine();
        ForceTucked(m);

        m.SetSnapped(false);

        Assert.Equal([TuckCommand.Untuck], m.DrainCommands());
    }

    // ---------- 托盘恢复 3 秒会话 ----------

    [Fact]
    public void TrayRestore_WhileTucked_PopsAndStartsHold()
    {
        var m = NewMachine();
        ForceTucked(m);

        m.TrayRestoreRequested(T0);

        Assert.Equal([TuckCommand.Untuck, TuckCommand.StartHold], m.DrainCommands());
    }

    [Fact]
    public void HoldExpires_PointerOutside_CommandsTuck()
    {
        var m = NewMachine();
        ForceTucked(m);
        m.TrayRestoreRequested(T0);
        m.DrainCommands();
        m.NotifyUntuckCompleted();
        m.SetPointerInside(false);

        m.Tick(T0.AddSeconds(3).AddMilliseconds(1));

        Assert.Equal([TuckCommand.Tuck, TuckCommand.EndHold], m.DrainCommands());
    }

    [Fact]
    public void Hold_PointerInside_Stays()
    {
        var m = NewMachine();
        ForceTucked(m);
        m.TrayRestoreRequested(T0);
        m.DrainCommands();
        m.NotifyUntuckCompleted();
        m.SetPointerInside(true);

        m.Tick(T0.AddSeconds(10));

        Assert.Empty(m.DrainCommands());
    }

    [Fact]
    public void Hold_CanceledByChildOpen()
    {
        var m = NewMachine();
        ForceTucked(m);
        m.TrayRestoreRequested(T0);
        m.DrainCommands();
        m.NotifyUntuckCompleted();

        m.SetChildOpen(true);

        Assert.Equal([TuckCommand.EndHold], m.DrainCommands());
    }

    [Fact]
    public void Hold_ExpiredWhileAnimating_NoReentry()
    {
        var m = NewMachine();
        ForceTucked(m);
        m.TrayRestoreRequested(T0);
        m.DrainCommands();
        m.SetPointerInside(false);

        m.Tick(T0.AddSeconds(3).AddMilliseconds(1));
        // Untuck 已发出但 UI 未回报完成（动画中）：Tick 不得再发 Tuck
        m.Tick(T0.AddSeconds(4));

        Assert.Empty(m.DrainCommands());
    }

    // ---------- 辅助：直接推进到 Tucked 稳态 ----------

    private static void ForceTucked(TuckStateMachine m)
    {
        m.SetSnapped(true);
        m.NotifyPointerLeft(T0);
        m.Tick(T0.AddMilliseconds(500));
        m.DrainCommands();
        m.NotifyTuckCompleted();
    }
}
