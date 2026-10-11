namespace TodoWpf.Core.Persistence;

/// <summary>数据落点（修订记录 R3）：WPF 版独立路径，旧版路径仅用于首启自动迁移。</summary>
public static class DataPaths
{
    public const string AppFolderName = "桌面待办-WPF";

    public const string LegacyFolderName = "桌面待办";

    public const string FileName = "data.json";

    /// <summary>%APPDATA%（漫游）根目录。</summary>
    public static string AppDataRoot() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string NewDataFile() => NewDataFile(AppDataRoot());

    public static string NewDataFile(string appDataRoot) =>
        Path.Combine(appDataRoot, AppFolderName, FileName);

    public static string LegacyDataFile() => LegacyDataFile(AppDataRoot());

    public static string LegacyDataFile(string appDataRoot) =>
        Path.Combine(appDataRoot, LegacyFolderName, FileName);
}
