using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TodoWpf.Windows;

/// <summary>
/// 提示（toast）公共实现：**画在目标窗口自己身上**，底部居中深色胶囊。
///
/// 为什么要有这个类（2026-11-05 用户反馈）：此前 toast 只做在主窗 XAML 里，
/// 控制器又写死 `_main.ShowToast(...)` —— 于是设置窗/历史窗/编辑窗触发的提示
/// 全被画到主窗底部；设置窗是模态且比主窗大，提示直接被盖住（用户看不到）。
///
/// 口径（原型 .toasts，1:1 不变）：底部居中、离底 28、0.18s 淡入上移、2.2s 消失、重复调用重置计时。
/// 目标窗口由调用方决定（见 ClusterController.ShowToast：跟随当前活动窗口）。
/// </summary>
internal static class ToastPresenter
{
    private static readonly Dictionary<Window, Host> Hosts = new();

    /// <summary>在目标窗口上显示一条提示（同一窗口重复调用 = 重置计时并换文案）。</summary>
    public static void Show(Window? target, string message)
    {
        if (target is null)
        {
            return;
        }

        if (!Hosts.TryGetValue(target, out var host))
        {
            host = Create(target);
            Hosts[target] = host;
            target.Closed += (_, _) => Hosts.Remove(target);
        }

        host.Text.Text = message;
        host.Pill.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        var riseIn = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(180));
        host.Pill.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        host.Shift.BeginAnimation(TranslateTransform.YProperty, riseIn);

        host.Timer.Stop();
        host.Timer.Start();
    }

    private sealed record Host(Border Pill, TextBlock Text, TranslateTransform Shift, DispatcherTimer Timer);

    private static Host Create(Window window)
    {
        var pill = new Border
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 28),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 9, 14, 9),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        Panel.SetZIndex(pill, 30);
        var root = HostRoot(window);
        // 跨满所有行列：二级窗的根 Grid 带行定义（标题栏 40 + 内容 *），
        // 不跨行时提示会落进第 0 行（标题栏行）——实测跑到窗口顶部去了。
        if (root is Grid grid)
        {
            Grid.SetRowSpan(pill, Math.Max(1, grid.RowDefinitions.Count));
            Grid.SetColumnSpan(pill, Math.Max(1, grid.ColumnDefinitions.Count));
        }

        // 主题色一律走动态资源引用：切日/夜主题时跟随（与其它控件同一纪律）
        pill.SetResourceReference(Border.BackgroundProperty, "Brush.Toast");

        var shift = new TranslateTransform(0, 0);
        pill.RenderTransform = shift;

        var text = new TextBlock { FontSize = 12, LineHeight = 18 };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ToastFg");
        pill.Child = text;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            pill.Visibility = Visibility.Collapsed;
        };

        root.Children.Add(pill);
        return new Host(pill, text, shift, timer);    }

    /// <summary>
    /// 找承载 toast 的面板：窗口内容往下找第一个 Panel（各窗口结构都是 Border→Grid）。
    /// 找不到就把原内容包进一个新 Grid（保持原实例，命名引用不受影响）。
    /// </summary>
    private static Panel HostRoot(Window window)
    {
        if (window.Content is Panel direct)
        {
            return direct;
        }

        if (window.Content is FrameworkElement element)
        {
            var found = FindPanel(element);
            if (found is not null)
            {
                return found;
            }

            // 兜底：包一层 Grid（原元素实例不变）
            window.Content = null;
            var wrapper = new Grid();
            wrapper.Children.Add(element);
            window.Content = wrapper;
            return wrapper;
        }

        var empty = new Grid();
        window.Content = empty;
        return empty;
    }

    private static Panel? FindPanel(DependencyObject node)
    {
        if (node is Panel panel)
        {
            return panel;
        }

        if (node is Decorator decorator && decorator.Child is not null)
        {
            return FindPanel(decorator.Child);
        }

        return null;
    }
}
