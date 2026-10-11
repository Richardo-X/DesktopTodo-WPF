using System.Windows;

namespace TodoWpf.Windows;

/// <summary>令牌化确认框：ConfirmAsync 返回是否确认（用于托盘退出等）。</summary>
public partial class ConfirmDialog : Window
{
    private bool _confirmed;

    private ConfirmDialog(string title, string body, string okText)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        OkBtn.Content = okText;
    }

    /// <summary>显示确认框；返回 true=用户点了确定。</summary>
    public static bool Confirm(string title, string body, string okText = "确定")
    {
        var dialog = new ConfirmDialog(title, body, okText);
        dialog.ShowDialog();
        return dialog._confirmed;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
