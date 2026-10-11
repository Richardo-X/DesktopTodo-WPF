namespace TodoWpf.Core.Persistence;

/// <summary>文件 IO 适配（生产注入 <see cref="SystemFileIo"/>，测试注入内存实现）。</summary>
public interface IFileIo
{
    Task<bool> ExistsAsync(string path);

    Task<string> ReadAllTextAsync(string path);

    Task WriteAllTextAsync(string path, string text);

    Task MoveAsync(string source, string destination, bool overwrite);

    Task CreateDirectoryAsync(string path);
}
