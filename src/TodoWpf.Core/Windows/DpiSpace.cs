namespace TodoWpf.Core.Windows;

/// <summary>
/// 物理像素 ↔ DIP 折算（纯函数，同一屏内线性换算；屏的查找与 DPI 获取在 UI 层 MonitorLayout）。
/// 存档锚点改存物理像素后，加载时按"位置所在屏"的 DPI 折算回布局空间（2026-11-07 恢复跨屏错位修复）。
/// </summary>
public static class DpiSpace
{
    public static (double X, double Y) RawToDips(double rawX, double rawY, double scale) => (rawX / scale, rawY / scale);

    public static (double X, double Y) DipsToRaw(double dipX, double dipY, double scale) => (dipX * scale, dipY * scale);
}
