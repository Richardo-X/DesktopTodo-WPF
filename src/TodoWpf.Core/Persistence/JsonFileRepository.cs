using System.Globalization;
using System.Text.Json;
using TodoWpf.Core.Models;

namespace TodoWpf.Core.Persistence;

/// <summary>
/// 文件仓储（生产实现）：
/// 原子写（写 .tmp 后替换，中断不损原文件）；防抖合并写 + Flush 强制落盘（对用户仍是"即时保存"语义）；
/// 损坏文件改名 .corrupt-&lt;时间戳&gt; 留档 → 返回 null（按首次启动处理）；写失败保留待写数据、下次自动重试。
/// </summary>
public sealed class JsonFileRepository : IRepository<AppData>
{
    private readonly IFileIo _io;
    private readonly int _debounceMs;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _gate = new();

    private AppData? _pending;
    private Timer? _timer;

    public JsonFileRepository(string filePath, IFileIo io, int debounceMs = 100)
    {
        FilePath = filePath;
        _io = io;
        _debounceMs = debounceMs;
    }

    public string FilePath { get; }

    /// <summary>最近一次后台写盘失败（诊断用；失败的数据保留，下次 Save/Flush 自动重试）。</summary>
    public Exception? LastWriteError { get; private set; }

    public async Task<AppData?> LoadAsync()
    {
        if (!await _io.ExistsAsync(FilePath))
        {
            return null;
        }

        string text;
        try
        {
            text = await _io.ReadAllTextAsync(FilePath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            return JsonCodec.Read(text);
        }
        catch (JsonException)
        {
            await ArchiveCorruptAsync();
            return null;
        }
    }

    public async Task SaveAsync(AppData data)
    {
        var snapshot = data.DeepCopy();
        bool writeNow;

        lock (_gate)
        {
            _pending = snapshot;
            if (_debounceMs <= 0)
            {
                writeNow = true;
            }
            else
            {
                _timer?.Dispose();
                _timer = new Timer(
                    _ => _ = WritePendingSafelyAsync(),
                    state: null,
                    dueTime: _debounceMs,
                    period: Timeout.Infinite);
                writeNow = false;
            }
        }

        if (writeNow)
        {
            await WritePendingAsync();
        }
    }

    public async Task FlushAsync()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        if (HasPending())
        {
            await WritePendingAsync();
        }
    }

    private bool HasPending()
    {
        lock (_gate)
        {
            return _pending is not null;
        }
    }

    private async Task WritePendingSafelyAsync()
    {
        try
        {
            await WritePendingAsync();
        }
        catch (Exception ex)
        {
            // 后台写失败：数据保留在 _pending，等下一次 Save/Flush 重试
            LastWriteError = ex;
        }
    }

    private async Task WritePendingAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            AppData? data;
            lock (_gate)
            {
                data = _pending;
            }

            if (data is null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                await _io.CreateDirectoryAsync(directory);
            }

            var tempPath = FilePath + ".tmp";
            await _io.WriteAllTextAsync(tempPath, JsonCodec.Write(data));
            await _io.MoveAsync(tempPath, FilePath, overwrite: true);

            lock (_gate)
            {
                // 仅成功落盘后清空待写数据（失败保留 → 自动重试）
                _pending = null;
                _timer?.Dispose();
                _timer = null;
            }

            LastWriteError = null;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ArchiveCorruptAsync()
    {
        var stamp = DateTimeOffset.UtcNow.ToString(
            "yyyy-MM-dd'T'HH-mm-ss-fff'Z'",
            CultureInfo.InvariantCulture);

        try
        {
            await _io.MoveAsync(FilePath, $"{FilePath}.corrupt-{stamp}", overwrite: false);
        }
        catch
        {
            // 留档失败不阻塞"按首次启动处理"的兜底
        }
    }
}
