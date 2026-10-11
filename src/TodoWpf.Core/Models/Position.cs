namespace TodoWpf.Core.Models;

/// <summary>组件簇位置（界面记忆项之一；几何语义由窗口行为批次定义）。</summary>
public sealed record Position
{
    public double X { get; init; }

    public double Y { get; init; }
}
