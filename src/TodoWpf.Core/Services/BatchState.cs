namespace TodoWpf.Core.Services;

/// <summary>
/// 批量删除模式状态（纯 C# 可单测；需求 §2.3）：
/// 目标窗口 = todos 或 templates（单目标）；进行中其余窗口冻结（跨窗冻结由 UI 层执行）；
/// 未选中时「删除所选」置灰；完成后退出批量。
/// </summary>
public sealed class BatchState
{
    public string? Target { get; private set; }

    private readonly HashSet<string> _selected = new();

    public bool IsActive => Target is not null;

    public bool IsSelected(string id) => _selected.Contains(id);

    public int SelectedCount => _selected.Count;

    /// <summary>
    /// 进入批量；**重复进入 = 清空旧选择回到首态**（Electron enterBatch 同款：sel.clear()+target=…）。
    /// </summary>
    public void Enter(string target)
    {
        Target = target;
        _selected.Clear();
    }

    /// <summary>切换某条选中态；未进行中时忽略。</summary>
    public void Toggle(string id)
    {
        if (!IsActive)
        {
            return;
        }

        if (_selected.Contains(id))
        {
            _selected.Remove(id);
        }
        else
        {
            _selected.Add(id);
        }
    }

    /// <summary>
    /// 「全选」切换（原型 batchAll 口径）：传入集合**全部已选中** → 清空（按钮此时文案=取消全选）；
    /// 否则全部选中。未进行中时忽略。
    /// </summary>
    public void ToggleAll(IEnumerable<string> ids)
    {
        if (!IsActive)
        {
            return;
        }

        var list = ids as IReadOnlyCollection<string> ?? ids.ToList();
        if (list.Count > 0 && list.All(IsSelected))
        {
            _selected.Clear();
            return;
        }

        foreach (var id in list)
        {
            _selected.Add(id);
        }
    }

    /// <summary>当前选中的 id 快照（删除用）。</summary>
    public IReadOnlyCollection<string> SelectedIds => _selected.ToList();

    /// <summary>「删除所选」是否可用（≥1 条选中）。</summary>
    public bool CanDelete => _selected.Count > 0;

    /// <summary>退出批量（清空）。</summary>
    public void Exit()
    {
        Target = null;
        _selected.Clear();
    }
}
