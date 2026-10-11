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
