using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TodoWpf.Core.Utils;

namespace TodoWpf.Preview;

/// <summary>外观预览主窗：待办主窗静态外观 + 规格检查陈列切换（预览工具，非产品界面）。</summary>
public partial class MainPreviewWindow : Window
{
    private bool _specShown;
    private bool _isNight;

    public MainPreviewWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateReducedMotionText();
        ClockTime.Text = DateTime.Now.ToString(App.TimeFormat);
        ClockDate.Text = App.FormatCnDate(DateTime.Now);
        SetShadowMode(ShadowMode.Blue);
    }

    private void Titlebar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        _isNight = !_isNight;
        App.ApplyTheme(_isNight);
        ThemeBtn.Tag = (Geometry)FindResource(_isNight ? "Icon.Sun" : "Icon.Moon");
    }

    private void SpecToggle_Click(object sender, RoutedEventArgs e)
    {
        _specShown = !_specShown;
        MainView.Visibility = _specShown ? Visibility.Collapsed : Visibility.Visible;
        SpecView.Visibility = _specShown ? Visibility.Visible : Visibility.Collapsed;
        SpecToggle.Content = _specShown ? "返回外观" : "规格检查";
        if (_specShown)
        {
            UpdateReducedMotionText();
        }
    }

    private void UpdateReducedMotionText() =>
        ReducedMotionText.Text = SystemParameters.ClientAreaAnimation
            ? "减少动画：当前系统动画开启（关闭系统动画后，本应用全部过渡自动禁用）"
            : "减少动画：已检测到系统关闭动画，本应用全部过渡已自动禁用";

    // ---------- 投影三档对比（验收反馈：双层蓝黑 / 单层蓝黑 / 单层纯黑对照） ----------

    private enum ShadowMode
    {
        Both,
        Blue,
        Black,
    }

    private void ShadowBoth_Click(object sender, RoutedEventArgs e) => SetShadowMode(ShadowMode.Both);

    private void ShadowBlue_Click(object sender, RoutedEventArgs e) => SetShadowMode(ShadowMode.Blue);

    private void ShadowBlack_Click(object sender, RoutedEventArgs e) => SetShadowMode(ShadowMode.Black);

    private void SetShadowMode(ShadowMode mode)
    {
        switch (mode)
        {
            case ShadowMode.Both:
                ShadowLayerOuter.Effect = (Effect)FindResource("Effect.DialogShadowSoft");
                ShadowLayerInner.Effect = (Effect)FindResource("Effect.DialogShadowMain");
                break;
            case ShadowMode.Blue:
                ShadowLayerOuter.Effect = null;
                ShadowLayerInner.Effect = (Effect)FindResource("Effect.DialogShadowMain");
                break;
            case ShadowMode.Black:
                ShadowLayerOuter.Effect = null;
                ShadowLayerInner.Effect = (Effect)FindResource("Effect.DialogShadowBlack");
                break;
        }

        Highlight(ShadowBoth, mode == ShadowMode.Both);
        Highlight(ShadowBlue, mode == ShadowMode.Blue);
        Highlight(ShadowBlack, mode == ShadowMode.Black);
    }

    private static void Highlight(Button button, bool selected) =>
        button.Background = (Brush)FindAppResource(selected ? "Brush.Accent" : "Brush.Btn");

    private static object FindAppResource(string key) =>
        Application.Current.Resources[key]
            ?? Application.Current.TryFindResource(key)
            ?? throw new InvalidOperationException($"缺少资源 {key}");
}
