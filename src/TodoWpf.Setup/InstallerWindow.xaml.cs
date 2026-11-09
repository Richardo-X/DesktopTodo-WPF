using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace TodoWpf.Setup;

/// <summary>
/// 安装器主窗（C4）：让用户选择安装位置 → 调用内嵌的 Velopack Setup 完成安装。
///
/// 为什么需要它：Velopack 自带的 Setup.exe **没有目录选择界面**（实测全新安装只弹一个
/// 「正在安装…请稍候」进度窗，无任何可选项），只支持命令行 `--installto`。
/// 故本安装器只做两件事：给一个位置选择界面 + 把选择透传给 Setup。
/// </summary>
public partial class InstallerWindow : Window
{
    /// <summary>Velopack 打包时的 packId（安装注册表项与默认目录名都基于它）。</summary>
    private const string PackId = "TodoWpf";

    private bool _installing;

    /// <summary>已安装位置（null = 未安装）。已安装时**不提供位置选择**，只问是否覆盖安装（用户 2026-11-09 裁定）。</summary>
    private string? _existingDir;

    /// <summary>已安装版本（仅用于提示文案）。</summary>
    private string? _existingVersion;

    public InstallerWindow()
    {
        InitializeComponent();
        Chrome.Apply(this);

        var existing = ExistingInstall();
        _existingDir = existing?.Dir;
        _existingVersion = existing?.Version;

        if (_existingDir is { } dir)
        {
            // 已安装：隐藏位置选择，改为「是否覆盖安装」（成熟安装器的升级/修复流程）
            InstallPanel.Visibility = Visibility.Collapsed;
            OverwritePanel.Visibility = Visibility.Visible;
            ExistingText.Text = string.IsNullOrEmpty(_existingVersion)
                ? $"检测到已安装 桌面待办WPF：\n{dir}"
                : $"检测到已安装 桌面待办WPF {_existingVersion}：\n{dir}";
            InstallBtn.Content = "覆盖安装";
            CancelBtn.Content = "退出";
        }
        else
        {
            PathBox.Text = DefaultInstallDir();
        }
    }

    /// <summary>默认安装位置：用户目录（免管理员，用户 2026-11-09 裁定）。</summary>
    private static string DefaultInstallDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PackId);

    private void Title_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择安装位置",
            InitialDirectory = Directory.Exists(PathBox.Text)
                ? PathBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FolderName;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            return;
        }

        // 已安装 → 用检测到的原位置（_existingDir），忽略界面上没有的路径输入
        var target = _existingDir ?? (PathBox.Text ?? "").Trim().Trim('"');
        if (target.Length == 0)
        {
            Warn("请先选择安装位置。");
            return;
        }

        try
        {
            target = Path.GetFullPath(target);
        }
        catch (Exception ex)
        {
            Warn("安装位置无效：" + ex.Message);
            return;
        }

        // 已安装时位置在上面的构造函数里已锁定为原位置（PathBox 只读），此处无需再询问

        _installing = true;
        InstallBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        SetStatus("正在安装，请稍候…");

        var setup = ExtractSetup();
        if (setup is null)
        {
            SetStatus("");
            Warn("安装器损坏：未找到内嵌的安装组件。请重新获取安装包。");
            _installing = false;
            InstallBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(setup)
            {
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--installto");
            psi.ArgumentList.Add(target);
            psi.ArgumentList.Add("--silent");

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                throw new InvalidOperationException("无法启动安装组件。");
            }

            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0)
            {
                SetStatus("");
                Warn($"安装未完成（退出码 {proc.ExitCode}）。\n若选择了需要管理员权限的位置，请改用用户目录或右键以管理员身份运行。");
                _installing = false;
                InstallBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                return;
            }

            SetStatus("安装完成。");
            // Setup 通常会自行启动应用；没起来就补一次（用户点完就应看到程序）
            var exe = Path.Combine(target, "current", "TodoWpf.exe");
            if (File.Exists(exe) && Process.GetProcessesByName("TodoWpf").Length == 0)
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }

            MessageBox.Show(this, "桌面待办WPF 安装完成。", "安装完成",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            SetStatus("");
            Warn("安装失败：" + ex.Message);
            _installing = false;
            InstallBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
        }
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "安装 桌面待办WPF", MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>
    /// 检测已安装的 桌面待办WPF（位置 + 版本）；未安装返回 null。
    ///
    /// 注意（2026-11-09 修的 bug）：**不要**拿 UninstallString 直接喂 <c>Path.GetDirectoryName</c>——
    /// 它是 <c>"C:\...\Update.exe" --uninstall</c> 这种"带引号 + 参数"的命令行，
    /// GetDirectoryName 解析出坏路径（抛异常被吞 → 恒判为未安装，安装器就错误地显示了位置选择）。
    /// 注册表项里有现成的 <c>InstallLocation</c>，优先用它；缺失时才从命令行里**取引号内**的 exe 再取目录。
    /// </summary>
    private static (string Dir, string? Version)? ExistingInstall()
    {
        try
        {
            const string uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            foreach (var sub in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var root = sub.OpenSubKey(uninstallKey);
                if (root is null)
                {
                    continue;
                }

                foreach (var name in root.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey(name);
                    if (entry is null)
                    {
                        continue;
                    }

                    var location = entry.GetValue("InstallLocation") as string;
                    var uninstall = entry.GetValue("UninstallString") as string;
                    var display = entry.GetValue("DisplayName") as string;
                    var version = entry.GetValue("DisplayVersion") as string;

                    // 认自己的条目：安装位置/卸载命令里带 packId（回退看显示名）
                    var ours = (location?.Contains(PackId, StringComparison.OrdinalIgnoreCase) ?? false)
                               || (uninstall?.Contains(PackId, StringComparison.OrdinalIgnoreCase) ?? false)
                               || (display?.Contains("待办WPF", StringComparison.Ordinal) ?? false);
                    if (!ours)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
                    {
                        return (location.TrimEnd('\\'), version);
                    }

                    var exe = ExtractExePath(uninstall);
                    var dir = exe is null ? null : Path.GetDirectoryName(exe);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return (dir, version);
                    }
                }
            }
        }
        catch
        {
            // 注册表读取失败按"未安装"处理：不阻断安装
        }

        return null;
    }

    /// <summary>从命令行里取出可执行文件路径：带引号取引号内，否则取到第一个空格。</summary>
    private static string? ExtractExePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = commandLine.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    /// <summary>取出内嵌的 Velopack Setup 到临时文件（单文件交付）；开发期回退为查找同目录。</summary>
    private static string? ExtractSetup()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SetupPayload.exe");
        if (stream is not null)
        {
            var path = Path.Combine(Path.GetTempPath(), $"TodoWpfSetup-{Guid.NewGuid():N}.exe");
            using var file = File.Create(path);
            stream.CopyTo(file);
            return path;
        }

        var beside = Path.Combine(AppContext.BaseDirectory, "TodoWpf-win-Setup.exe");
        return File.Exists(beside) ? beside : null;
    }
}
