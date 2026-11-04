$ErrorActionPreference='Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class Mon {
  [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern bool GetMonitorInfoW(IntPtr m, ref MONITORINFO mi);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
  public static string Info(IntPtr h) {
    var sb = new StringBuilder();
    uint dpi = GetDpiForWindow(h);
    sb.Append("DPI=").Append(dpi).Append(" (").Append(dpi*100/96).Append("%)");
    IntPtr mon = MonitorFromWindow(h, 2);
    var mi = new MONITORINFO(); mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
    if (GetMonitorInfoW(mon, ref mi)) {
      sb.Append(" | 显示器物理 ").Append(mi.rcMonitor.R-mi.rcMonitor.L).Append("x").Append(mi.rcMonitor.B-mi.rcMonitor.T);
      sb.Append(" | 工作区物理 ").Append(mi.rcWork.R-mi.rcWork.L).Append("x").Append(mi.rcWork.B-mi.rcWork.T);
      sb.Append(" @(").Append(mi.rcWork.L).Append(",").Append(mi.rcWork.T).Append(")");
    }
    RECT r; GetWindowRect(h, out r);
    sb.Append(" | 窗口物理 ").Append(r.R-r.L).Append("x").Append(r.B-r.T).Append(" @(").Append(r.L).Append(",").Append(r.T).Append(")");
    return sb.ToString();
  }
  public static string Virtual() {
    return "虚拟屏 " + GetSystemMetrics(78) + "x" + GetSystemMetrics(79) + " 显示器数 " + GetSystemMetrics(80);
  }
}
"@
$AE=[System.Windows.Automation.AutomationElement]; $root=$AE::RootElement
$proc = Get-Process -Name TodoWpf -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output '应用未运行'; exit }
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
Write-Output ("[" + [Mon]::Virtual() + "]")
foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) {
  $h = [IntPtr]$w.Current.NativeWindowHandle
  if ($h -eq [IntPtr]::Zero) { continue }
  Write-Output ("窗口 '" + $w.Current.Name + "' → " + [Mon]::Info($h))
}
