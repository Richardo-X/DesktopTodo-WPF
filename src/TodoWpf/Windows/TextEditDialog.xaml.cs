using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 文本编辑二级窗（原型 openEditor；新建/编辑待办、新建/编辑模板共用）：
/// TextEditDialog.Open(configure) 注入按钮与回调。
/// 底部操作分左右槽：左=删除/清空，右=添加到模板/保存（原型 ed-foot 口径）；
/// Enter=提交（Shift+Enter 换行）、Esc=返回；保存失败提示不关闭（AutoClose=false）。
/// </summary>
public partial class TextEditDialog : Window
{
    /// <summary>按钮档位（原型三档）：次操作 / 主操作 / 危险。</summary>
    public enum ActionTone { Normal, Primary, Danger }

    /// <summary>按钮定义。</summary>
    public sealed record ActionDef(
        string Key, string Text, ActionTone Tone = ActionTone.Normal, bool Enabled = true,
        bool AutoClose = true, Action<TextEditDialog>? OnClick = null);

    private readonly List<ActionDef> _pendingActions = new();
    private string? _result;
    private Action<TextEditDialog>? _submitAction;
    private string _initialText = "";
    private string _placeholder = "";

    private TextEditDialog()
    {
        InitializeComponent();
        // 2026-11-05 裁定：二级窗弃 WPF 投影，改 DWM 系统圆角（与主窗同壳）
        WindowShell.ApplyFixed(this);
        UpdatePlaceholder();
    }

    public System.Windows.Controls.TextBox Editor => InputBox;

    public Panel ActionsPanel => RightActions; // 兼容既有回调取按钮（空内容置灰）

    public string? Result => _result;

    /// <summary>编辑态：显示状态徽标（进行中/已逾期胶囊）与创建时间文字。</summary>
    public TextEditDialog SetStateBadge(string badge, string createdAtText, bool overdue)
    {
        StatePanel.Visibility = Visibility.Visible;
        StateBadge.Text = badge;
        StateBadge.SetResourceReference(TextBlock.ForegroundProperty,
            overdue ? "Brush.Overdue" : "Brush.Accent");
        StateBadgeBorder.SetResourceReference(Border.BackgroundProperty,
            overdue ? "Brush.SoftOverdue" : "Brush.SoftAccent");
        CreatedAtText.Text = createdAtText;
        return this;
    }

    /// <summary>编辑态顶行「还原」按钮：缺省还原为初始文本（XAML Click），可注入自定义回调。</summary>
    public TextEditDialog SetRestoreButton(Action<TextEditDialog>? onClick)
    {
        RestoreBtn.Visibility = Visibility.Visible;
        if (onClick is not null)
        {
            RestoreBtn.Click += (_, _) => onClick(this);
        }

        return this;
    }

    public TextEditDialog SetText(string initial)
    {
        _initialText = initial;
        InputBox.Text = initial;
        InputBox.CaretIndex = InputBox.Text.Length;
        return this;
    }

    public TextEditDialog SetPlaceholder(string placeholder)
    {
        _placeholder = placeholder;
        UpdatePlaceholder();
        return this;
    }

    public TextEditDialog SetCounterTotal(int total) => this; // 字数上限恒 300（ContentRules.MaxLength）

    /// <summary>左槽按钮（删除/清空）。</summary>
    public TextEditDialog AddLeftAction(ActionDef def)
    {
        _pendingActions.Add(def);
        var btn = BuildAction(def, left: true);
        LeftActions.Children.Add(btn);
        return this;
    }

    /// <summary>右槽按钮（添加到模板/保存）。</summary>
    public TextEditDialog AddRightAction(ActionDef def)
    {
        _pendingActions.Add(def);
        var btn = BuildAction(def, left: false);
        RightActions.Children.Add(btn);
        if (def.Tone == ActionTone.Primary)
        {
            _submitAction = d => def.OnClick?.Invoke(d); // Enter 触发主操作
        }

        return this;
    }

    /// <summary>字数计数联动（输入变化时回调，用于保存键的空内容置灰）。</summary>
    public TextEditDialog SetOnTextChanged(Action<TextEditDialog> onChanged)
    {
        _onTextChanged = onChanged;
        return this;
    }

    public TextEditDialog SetTitle(string title)
    {
        TitleText.Text = title;
        return this;
    }

    private System.Windows.Controls.Button BuildAction(ActionDef def, bool left)
    {
        var btn = new System.Windows.Controls.Button
        {
            Content = def.Text,
            IsEnabled = def.Enabled,
            Margin = left ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0),
        };
        var styleKey = def.Tone switch
        {
            ActionTone.Primary => "EdBtnPrimary",
            ActionTone.Danger => "EdBtnDanger",
            _ => "EdBtn",
        };
        btn.SetResourceReference(StyleProperty, styleKey);
        btn.Click += (_, _) =>
        {
            _result = def.Key;
            def.OnClick?.Invoke(this);
            if (def.AutoClose)
            {
                Close();
            }
        };
        return btn;
    }

    private void UpdatePlaceholder()
    {
        PlaceholderText.Text = _placeholder;
        PlaceholderText.Visibility = InputBox.Text.Length == 0 && _placeholder.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ---------- Builder（保持 Open(configure) 入口习惯） ----------

    public sealed class Builder
    {
        private readonly TextEditDialog _dialog = new();
        internal Builder() { }

        public Builder Title(string title) { _dialog.SetTitle(title); return this; }

        public Builder StateBadge(string badge, string createdAtText, bool overdue)
        {
            _dialog.SetStateBadge(badge, createdAtText, overdue);
            return this;
        }

        public Builder RestoreButton(Action<TextEditDialog>? onClick = null)
        {
            _dialog.SetRestoreButton(onClick);
            return this;
        }

        public Builder Text(string initial) { _dialog.SetText(initial); return this; }

        public Builder Placeholder(string placeholder) { _dialog.SetPlaceholder(placeholder); return this; }

        public Builder CounterTotal(int total) { _dialog.SetCounterTotal(total); return this; }

        public Builder LeftAction(string key, string text, bool danger = false, bool enabled = true,
                                  Action<TextEditDialog>? onClick = null)
        {
            _dialog.AddLeftAction(new ActionDef(
                key, text, danger ? ActionTone.Danger : ActionTone.Normal, enabled, true, onClick));
            return this;
        }

        public Builder Action(string key, string text, bool primary = false, bool enabled = true,
                              Action<TextEditDialog>? onClick = null, bool autoClose = true)
        {
            _dialog.AddRightAction(new ActionDef(
                key, text, primary ? ActionTone.Primary : ActionTone.Normal, enabled, autoClose, onClick));
            return this;
        }

        public Builder OnTextChanged(Action<TextEditDialog> onChanged)
        {
            _dialog.SetOnTextChanged(onChanged);
            return this;
        }

        public TextEditDialog Build(ClusterController? controller = null)
        {
            _dialog._controller = controller;
            _dialog.Owner = System.Windows.Application.Current.Windows
                .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办");
            _dialog.AttachScale();
            _dialog.CenterOnOwner();
            return _dialog;
        }
    }

    private int _counterTotal = 300;
    private Action<TextEditDialog>? _onTextChanged;
    private ClusterController? _controller;

    /// <summary>二级窗等比缩放（编辑组 420×560；右下手柄，按组存档）。</summary>
    internal void AttachScale() =>
        DialogScale.Attach(this, CardRoot, ScaleGrip, "edit", 420, 560, _controller);

    public static string? Open(Action<TextEditDialog.Builder> configure, ClusterController? controller = null)
    {
        var builder = new Builder();
        configure(builder);
        var dialog = builder.Build(controller);
        dialog.ShowDialog();
        return dialog._result;
    }

    /// <summary>与主窗当前视觉矩形两轴居中（越界钳回所在屏工作区，需求 R2）。</summary>
    private void CenterOnOwner()
    {
        var anchor = Owner ?? System.Windows.Application.Current.Windows
            .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办");
        if (anchor is null)
        {
            return;
        }

        // 同设置窗：物理像素定位必须在可见前（SourceInitialized）
        SourceInitialized += (_, _) => MonitorLayout.CenterOnOwnerPhysical(this, anchor, edgeDips: 8);
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateCounter();
        UpdatePlaceholder();
    }

    private void UpdateCounter()
    {
        var len = InputBox.Text.Length;
        CounterText.Text = $"{len}/{_counterTotal}";
        // 将满变红（原型 .near：len > max-30）
        CounterText.SetResourceReference(TextBlock.ForegroundProperty,
            len > _counterTotal - 30 ? "Brush.Overdue" : "Brush.Faint");
        _onTextChanged?.Invoke(this);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        // 还原=回到初始文本（SetRestoreButton 未注入自定义回调时走此默认）
        InputBox.Text = _initialText;
        InputBox.CaretIndex = InputBox.Text.Length;
        _onTextChanged?.Invoke(this);
    }

    /// <summary>标题栏拖动：走 WindowShell 的物理像素实现（拖动中持续钳屏，需求「限屏内」）。
    /// 不能用 WPF 的 DragMove()——它没有范围限制，窗口能被拖出屏幕抓不回来（2026-11-06 查漏）。</summary>
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => WindowShell.BeginDialogDrag(this, e);

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            _submitAction?.Invoke(this); // Enter=提交（Shift+Enter 换行）
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Back_Click(sender, e); // Esc=返回（由调用方 dirty 询问逻辑接管——见 OpenEditTodo 约定）
        }
    }
}
