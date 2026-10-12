using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 键盘焦点描边口径（L1 第 8 项）：**仅键盘导航（Tab）时显示，鼠标点击不出现**——对应 CSS 的 :focus-visible。
/// WPF 的 FocusVisualStyle 默认在鼠标点击后同样显示，视频抽帧可见"点过按钮留蓝框"；
/// 这里按最后一次输入设备切换各窗口内控件的 FocusVisualStyle。
/// </summary>
internal static class FocusVisualScope
{
    public static void Attach(Window window)
    {
        window.PreviewMouseDown += (_, _) => Apply(window, keyboard: false);
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Tab)
            {
                Apply(window, keyboard: true);
            }
        };
    }

    private static void Apply(Window window, bool keyboard)
    {
        var ring = keyboard ? (Style)window.FindResource("FocusRing") : null;
        foreach (var element in Descendants(window).OfType<FrameworkElement>())
        {
            if (element.FocusVisualStyle is not null || ring is not null)
            {
                element.FocusVisualStyle = ring;
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var grand in Descendants(child))
            {
                yield return grand;
            }
        }
    }
}
