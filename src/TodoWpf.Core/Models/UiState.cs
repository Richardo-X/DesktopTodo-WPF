namespace TodoWpf.Core.Models;

/// <summary>界面记忆项（窗口位置/缩放/开关/主题/置顶/缩进锁定/最近结算日）。</summary>
public sealed record UiState
{
    /// <summary>组件簇位置；null = 首次启动默认位置（屏幕右侧偏上）。</summary>
    public Position? Pos { get; init; }

    /// <summary>
    /// Electron 版遗留的 pos 语义迁移标记：旧档中 true = pos 已是"待办左上角锚点"。
    /// WPF 版不使用 Electron 的几何迁移（几何模型由窗口批次另定），读取时原样保留，缺省为 false。
    /// </summary>
    public bool PosIsTodoAnchor { get; init; }

    /// <summary>
    /// pos 的坐标语义标记（2026-11-07 恢复跨屏错位修复）：true = pos 存的是**物理像素**，
    /// 重启后按位置所在屏的 DPI 折算回布局空间，与存档时在哪块屏无关；
    /// false/缺省 = 旧档，pos 是存档时所在屏的 DIP（保持旧读取语义，重新停靠一次后升级为新格式）。
    /// </summary>
    public bool PosIsRawPx { get; init; }

    /// <summary>「新版本提示」开关（C4，默认开）：开 = 每次启动自动检查更新并显示标题栏提示；关 = 不检查。</summary>
    public bool UpdateNotifyEnabled { get; init; } = true;

    /// <summary>上次运行本应用的应用版本（C4 降级检测：当前版本 &lt; 它 → 数据可能不完全兼容，提醒备份）。</summary>
    public string? LastRunVersion { get; init; }

    /// <summary>三面板簇整体等比缩放。</summary>
    public double ClusterScale { get; init; } = 1;

    /// <summary>子窗口按类别分组缩放：事项类/模板类/设置/历史。</summary>
    public Dictionary<string, double> ChildScales { get; init; } = new();

    /// <summary>快捷添加面板开关（记忆）。</summary>
    public bool QuickVisible { get; init; } = true;

    /// <summary>已完成面板开关（记忆）。</summary>
    public bool DoneVisible { get; init; } = true;

    /// <summary>主题："day" 或 "night"。</summary>
    public string Theme { get; init; } = "day";

    /// <summary>窗口置顶（记忆）。</summary>
    public bool TopMost { get; init; }

    /// <summary>贴边缩进锁定。</summary>
    public bool TuckLock { get; init; }

    /// <summary>最近一次跨天结算的本地日期键（YYYY-MM-DD）；null = 从未结算。</summary>
    public string? LastSettleDate { get; init; }
}
