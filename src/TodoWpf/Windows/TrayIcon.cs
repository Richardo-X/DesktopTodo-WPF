using System.IO;
using TodoWpf.Core.Persistence;

namespace TodoWpf.Windows;

/// <summary>系统托盘图标（WinForms NotifyIcon）：显隐/外观预览/退出；图标=蓝底白勾（程序合成）。</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly ClusterController _controller;

    public TrayIcon(ClusterController controller)
    {
        _controller = controller;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "桌面待办",
            Visible = true,
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, _) => ToggleVisible());
        menu.Items.Add("外观预览（工具）", null, (_, _) => OpenPreview());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _ = ExitAsync());
        _notifyIcon.ContextMenuStrip = menu;

        // 单击恢复（体验说明书 ④.1）：隐藏态下单击弹回。250ms 延迟区分双击
        // （双击会先触发一次 Click，若立即恢复则双击切换会被抖成两次）。
        _clickTimer.Interval = 250;
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (_controller.IsClusterHidden)
            {
                _controller.ShowClusterFromTray();
            }
        };
        _notifyIcon.Click += (_, _) => _clickTimer.Start();
        _notifyIcon.DoubleClick += (_, _) =>
        {
            _clickTimer.Stop(); // 双击=切换（需求 §2.2），吞掉刚才那次单击
            ToggleVisible();
        };
    }

    private readonly System.Windows.Forms.Timer _clickTimer = new();

    private void ToggleVisible()
    {
        if (_controller.IsClusterHidden)
        {
            _controller.ShowClusterFromTray();
        }
        else
        {
            _controller.HideClusterToTray();
        }
    }

    private static void OpenPreview()
    {
        var preview = new TodoWpf.Preview.MainPreviewWindow();
        preview.Show();
    }

    private async Task ExitAsync()
    {
        var ok = ConfirmDialog.Confirm(
            "退出桌面待办",
            "确定要退出桌面待办吗？",
            "退出");
        if (ok)
        {
            await _controller.ExitAsync();
        }
    }

    /// <summary>合成托盘图标：主题色圆角方块 + 白色对勾（无外部资源依赖）。</summary>
    private static System.Drawing.Icon BuildIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var background = new System.Drawing.Drawing2D.GraphicsPath();
            background.AddArc(1, 1, 30, 30, 180, 360);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x0A, 0x63, 0xB8));
            g.FillPath(brush, background);

            using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 3.4f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            g.DrawLines(pen,
            [
                new System.Drawing.PointF(9f, 16.5f),
                new System.Drawing.PointF(14f, 21.5f),
                new System.Drawing.PointF(23f, 9.5f),
            ]);
        }

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
