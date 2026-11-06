using System.Text;
using TodoWpf.Core.Persistence;

namespace TodoWpf.Tests;

/// <summary>
/// 生产文件 IO（SystemFileIo）——2026-11-06 查漏补测：此前只有 MemoryFileIo 被测，
/// 生产适配器零覆盖。重点钉住两条与旧版（node:fs utf8）兼容的口径：
/// ① 写出 **UTF-8 无 BOM**（带 BOM 会让旧版/Excel 读到 \uFEFF）；
/// ② 读回文本与写入一致（中文不乱码）。
/// </summary>
public sealed class SystemFileIoTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "todowpf-io-" + Guid.NewGuid().ToString("N"));

    public SystemFileIoTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // 清理失败不影响用例结论（临时目录）
        }
    }

    private string P(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task 写出为UTF8无BOM()
    {
        var io = new SystemFileIo();
        var path = P("data.json");

        await io.WriteAllTextAsync(path, "{\"content\":\"中文内容\"}");

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "不应带 UTF-8 BOM");
        Assert.Equal("{\"content\":\"中文内容\"}", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task 读回与写入一致_中文不乱码()
    {
        var io = new SystemFileIo();
        var path = P("round.txt");
        const string text = "第一行\n第二行 · 中文\n";

        await io.WriteAllTextAsync(path, text);

        Assert.Equal(text, await io.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task 存在性判断()
    {
        var io = new SystemFileIo();
        var path = P("exists.txt");

        Assert.False(await io.ExistsAsync(path));
        await io.WriteAllTextAsync(path, "x");
        Assert.True(await io.ExistsAsync(path));
    }

    [Fact]
    public async Task 移动_覆盖与不覆盖()
    {
        var io = new SystemFileIo();
        var src = P("a.txt");
        var dst = P("b.txt");

        await io.WriteAllTextAsync(src, "A");
        await io.MoveAsync(src, dst, overwrite: false);
        Assert.False(await io.ExistsAsync(src));
        Assert.Equal("A", await io.ReadAllTextAsync(dst));

        // 覆盖：目标已存在时也应成功
        await io.WriteAllTextAsync(src, "B");
        await io.MoveAsync(src, dst, overwrite: true);
        Assert.Equal("B", await io.ReadAllTextAsync(dst));
    }

    [Fact]
    public async Task 建目录_已存在时不抛()
    {
        var io = new SystemFileIo();
        var sub = Path.Combine(_dir, "nested", "deeper");

        await io.CreateDirectoryAsync(sub);
        await io.CreateDirectoryAsync(sub);   // 幂等

        Assert.True(Directory.Exists(sub));
    }
}
