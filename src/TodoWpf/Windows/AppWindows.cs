using System.Windows;

namespace TodoWpf.Windows;

/// <summary>
/// 本应用窗口的查找助手（2026-11-06 查漏统一）：
/// 此前 7 处各写一遍"按标题找主窗"（<c>w.Title == "桌面待办"</c>），而二级窗的标题**也是**"桌面待办"，
/// 于是可能取到某个对话框而不是主窗。改为**按类型**找，语义确定。
/// </summary>
internal static class AppWindows
{
    /// <summary>主窗（唯一可独立存在的一级窗）。</summary>
    public static Window? Main() =>
        Application.Current?.Windows.OfType<MainTodoWindow>().FirstOrDefault();

    /// <summary>
    /// 当前打开的**二级窗**（对话框）：除主窗、模板窗、白条、悬停气泡之外的全部。
    /// 托盘隐藏/恢复时按这个集合处理（隐藏时一并隐藏、恢复时直接关闭）。
    /// </summary>
    public static IEnumerable<Window> Dialogs() =>
        (Application.Current?.Windows ?? new WindowCollection())
        .OfType<Window>()
        .Where(w => w is not MainTodoWindow and not QuickWindow and not TuckBarWindow and not BubbleWindow);
}
