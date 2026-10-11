using System.Text;

namespace TodoWpf.Core.Persistence;

/// <summary>生产文件 IO：UTF-8 无 BOM（对齐旧版 node:fs utf8 写出）。</summary>
public sealed class SystemFileIo : IFileIo
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public Task<bool> ExistsAsync(string path) => Task.FromResult(File.Exists(path));

    public async Task<string> ReadAllTextAsync(string path) =>
        await File.ReadAllTextAsync(path).ConfigureAwait(false);

    public async Task WriteAllTextAsync(string path, string text) =>
        await File.WriteAllTextAsync(path, text, Utf8NoBom).ConfigureAwait(false);

    public Task MoveAsync(string source, string destination, bool overwrite)
    {
        File.Move(source, destination, overwrite);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }
}
