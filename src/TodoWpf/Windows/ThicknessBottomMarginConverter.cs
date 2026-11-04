using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TodoWpf.Windows;

/// <summary>
/// Thickness → 只保留 Bottom 的外边距。供覆盖式滚动条模板用：
/// 滚动条下端要收到"内容下截断位置"（= ScrollViewer.Padding.Bottom），
/// 让滑块拖到底也不越过内容区、不压到右下缩放角标（用户 2026-11-05 反馈）。
/// 水平方向不随 Padding 走——覆盖式滚动条保持贴窗口右缘。
/// </summary>
public sealed class ThicknessBottomMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var thickness = value is Thickness t ? t : default;
        return new Thickness(0, 0, 0, thickness.Bottom);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
