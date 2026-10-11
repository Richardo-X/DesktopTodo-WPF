namespace TodoWpf.Core.Utils;

/// <summary>全局唯一 id：小写 UUID v4（与旧版 crypto.randomUUID() 同形态）。</summary>
public static class Ids
{
    public static string NewId() => Guid.NewGuid().ToString("D");
}
