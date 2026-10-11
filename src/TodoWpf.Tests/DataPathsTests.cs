using TodoWpf.Core.Persistence;
using IOPath = System.IO.Path;

namespace TodoWpf.Tests;

/// <summary>数据落点（修订记录 R3）：新路径=桌面待办-WPF、旧路径=桌面待办。</summary>
public class DataPathsTests
{
    [Fact]
    public void NewDataFile_UsesWpfFolder()
    {
        Assert.Equal(
            IOPath.Combine("R:\\root", "桌面待办-WPF", "data.json"),
            DataPaths.NewDataFile("R:\\root"));
    }

    [Fact]
    public void LegacyDataFile_UsesElectronFolder()
    {
        Assert.Equal(
            IOPath.Combine("R:\\root", "桌面待办", "data.json"),
            DataPaths.LegacyDataFile("R:\\root"));
    }

    [Fact]
    public void DefaultPaths_UseRealAppDataRoot_AndEndWithDataJson()
    {
        var root = DataPaths.AppDataRoot();

        Assert.False(string.IsNullOrWhiteSpace(root));
        Assert.EndsWith("data.json", DataPaths.NewDataFile());
        Assert.EndsWith("data.json", DataPaths.LegacyDataFile());
        Assert.StartsWith(root, DataPaths.NewDataFile());
        Assert.StartsWith(root, DataPaths.LegacyDataFile());
    }

    [Fact]
    public void FolderNames_MatchRequirement()
    {
        Assert.Equal("桌面待办-WPF", DataPaths.AppFolderName);
        Assert.Equal("桌面待办", DataPaths.LegacyFolderName);
        Assert.Equal("data.json", DataPaths.FileName);
    }
}
