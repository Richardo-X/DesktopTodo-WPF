using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoWpf.Windows;

/// <summary>
/// 文本编辑二级窗（新建/编辑待办、模板编辑共用）：
/// TextEditDialog.Open(configure) 注入按钮与回调，返回用户点过的动作 key（null=直接关闭）。
/// 屏蔽 Esc/Alt+F4 直接关闭（走"返回"逻辑，由回调决定是否询问）。
/// </summary>
public partial class TextEditDialog : Window
{
    /// <summary>按钮定义。</summary>
    public sealed record ActionDef(string Key, string Text, bool Primary = false, bool Enabled = true);

    private readonly Dictionary<string, Action<TextEditDialog>> _actions = new();
    private string? _result;

    private TextEditDialog()
    {
        InitializeComponent();
    }

    public System.Windows.Controls.TextBox Editor => InputBox;

    public string? Result => _result;

    public sealed class Builder
    {
        private readonly TextEditDialog _dialog = new();
        internal Builder() { }

        public Builder Title(string title) { _dialog.TitleText.Text = title; return this; }

        /// <summary>编辑态：显示状态徽标与创建时间文字。</summary>
        public Builder StateBadge(string badge, string createdAtText, bool overdue)
        {
            _dialog.StatePanel.Visibility = Visibility.Visible;
            _dialog.StateBadge.Text = badge;
            _dialog.StateBadge.SetResourceReference(TextBlock.ForegroundProperty,
                overdue ? "Brush.Overdue" : "Brush.Muted");
            _dialog.CreatedAtText.Text = createdAtText;
            return this;
        }

        public Builder Text(string initial) { _dialog.InputBox.Text = initial; return this; }

        /// <summary>编辑区水印（WPF 原生无 placeholder，用背景画法略；此处空串即可）。</summary>
        public Builder CounterTotal(int total)
        {
            _dialog._counterTotal = total;
            _dialog.UpdateCounter();
            return this;
        }

        public Builder Action(string key, string text, bool primary = false, bool enabled = true,
                              Action<TextEditDialog>? onClick = null)
        {
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                IsEnabled = enabled,
            };
            btn.SetResourceReference(StyleProperty, primary ? "PrimaryButton" : "BlockButton");
            btn.Click += (_, _) =>
            {
                _dialog._result = key;
                onClick?.Invoke(_dialog);
                _dialog.Close();
            };
            _dialog.ActionsPanel.Children.Add(btn);
            _dialog._actions[key] = d => { };
            return this;
        }

        /// <summary>字数计数联动（输入变化时回调，用于"新建"按钮的空内容置灰）。</summary>
        public Builder OnTextChanged(Action<TextEditDialog> onChanged)
        {
            _dialog._onTextChanged = onChanged;
            return this;
        }

        public TextEditDialog Build()
        {
            _dialog.Owner = System.Windows.Application.Current.Windows
                .OfType<Window>().FirstOrDefault(w => w.Title == "桌面待办");
            return _dialog;
        }
    }

    private int _counterTotal = ContentRulesMaxLength;
    private Action<TextEditDialog>? _onTextChanged;

    private const int ContentRulesMaxLength = 300;

    public static string? Open(Action<TextEditDialog.Builder> configure)
    {
        var builder = new Builder();
        configure(builder);
        var dialog = builder.Build();
        dialog.ShowDialog();
        return dialog._result;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateCounter();

    private void UpdateCounter()
    {
        var len = InputBox.Text.Length;
        CounterText.Text = $"{len}/{_counterTotal}";
        CounterText.SetResourceReference(TextBlock.ForegroundProperty,
            len >= _counterTotal ? "Brush.Overdue" : "Brush.Muted");
        _onTextChanged?.Invoke(this);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) =>
        DragMove();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }
}
