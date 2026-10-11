using TodoWpf.Core.Persistence;

namespace TodoWpf.Tests.TestSupport;

/// <summary>内存文件 IO（测试替身）：记录写/移动次数与顺序，支持注入读写失败。</summary>
public sealed class MemoryFileIo : IFileIo
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>成功写入次数（不含被注入失败的写）。</summary>
    public int WriteCount { get; private set; }

    public int MoveCount { get; private set; }

    public int CreateDirectoryCount { get; private set; }

    /// <summary>接下来 N 次写入抛 IOException（用完自动恢复）。</summary>
    public int FailWritesRemaining { get; set; }

    /// <summary>接下来 N 次读取抛 IOException。</summary>
    public int FailReadsRemaining { get; set; }

    /// <summary>成功操作的顺序日志（"write:路径" / "move:源->目标"）。</summary>
    public List<string> Operations { get; } = [];

    public void Put(string path, string content) => _files[path] = content;

    public bool Has(string path) => _files.ContainsKey(path);

    public string? ContentOf(string path) => _files.TryGetValue(path, out var text) ? text : null;

    public IEnumerable<string> Paths => _files.Keys;

    public string? FindPathStartingWith(string prefix) =>
        _files.Keys.FirstOrDefault(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public Task<bool> ExistsAsync(string path) => Task.FromResult(_files.ContainsKey(path));

    public Task<string> ReadAllTextAsync(string path)
    {
        if (FailReadsRemaining > 0)
        {
            FailReadsRemaining--;
            throw new IOException("simulated read failure");
        }

        return _files.TryGetValue(path, out var text)
            ? Task.FromResult(text)
            : throw new FileNotFoundException(path);
    }

    public Task WriteAllTextAsync(string path, string text)
    {
        if (FailWritesRemaining > 0)
        {
            FailWritesRemaining--;
            throw new IOException("simulated write failure");
        }

        WriteCount++;
        Operations.Add($"write:{path}");
        _files[path] = text;
        return Task.CompletedTask;
    }

    public Task MoveAsync(string source, string destination, bool overwrite)
    {
        MoveCount++;
        Operations.Add($"move:{source}->{destination}");
        if (!_files.TryGetValue(source, out var text))
        {
            throw new FileNotFoundException(source);
        }

        if (_files.ContainsKey(destination) && !overwrite)
        {
            throw new IOException("destination exists");
        }

        _files[destination] = text;
        _files.Remove(source);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path)
    {
        CreateDirectoryCount++;
        return Task.CompletedTask;
    }
}
