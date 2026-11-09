namespace TodoWpf;

/// <summary>
/// 应用显示名（窗口标题 / 标题栏品牌文字 / 托盘提示共用一处）。
/// 注意与安装包显示名区分：安装包用「桌面待办WPF」以避免与旧 Electron 版在
/// 「应用和功能」与快捷方式上撞名（2026-11-08 事故），应用内品牌仍是「桌面待办」。
/// </summary>
public static class AppInfo
{
    /// <summary>应用显示名。</summary>
    public const string DisplayName = "桌面待办";
}
