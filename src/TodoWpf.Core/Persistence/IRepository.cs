namespace TodoWpf.Core.Persistence;

/// <summary>仓储抽象：实现方需保证写入原子性。</summary>
public interface IRepository<TData>
{
    /// <summary>读取持久化数据；无数据文件（首次启动）返回 null。</summary>
    Task<TData?> LoadAsync();

    /// <summary>写入持久化数据（防抖合并；"即时保存"语义由 Flush 兜底）。</summary>
    Task SaveAsync(TData data);

    /// <summary>强制把防抖中未落盘的变更刷盘（退出前调用）。</summary>
    Task FlushAsync();
}
