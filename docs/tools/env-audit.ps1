# 环境审计：列出所有显示器的物理分辨率 / 缩放(DPI) / 工作区，并标出应用所在屏。
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File docs/tools/env-audit.ps1
# 改任何尺寸之前先跑它（纪律见 开场白.md）。
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text; using System.Collections.Generic;
public static class Env {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  public delegate bool EnumProc(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfoW(IntPtr hMon, ref MONITORINFO mi);
  [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint x, out uint y);
  [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
  public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string szDevice; }

  static List<string> _res;
  public static void MakeAware() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch {} }
  public static List<string> Monitors() {
    _res = new List<string>();
    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, new EnumProc(Cb), IntPtr.Zero);
    return _res;
  }
  static bool Cb(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr l) {
    var mi = new MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
    string dev = "?"; int mw=0, mh=0, ww=0, wh=0, wx=0, wy=0; bool prim=false;
    if (GetMonitorInfoW(hMon, ref mi)) {
      dev = mi.szDevice; mw = mi.rcMonitor.R-mi.rcMonitor.L; mh = mi.rcMonitor.B-mi.rcMonitor.T;
      ww = mi.rcWork.R-mi.rcWork.L; wh = mi.rcWork.B-mi.rcWork.T; wx = mi.rcWork.L; wy = mi.rcWork.T;
      prim = (mi.dwFlags & 1) != 0;
    }
    uint dx=0, dy=0; GetDpiForMonitor(hMon, 0, out dx, out dy);
    _res.Add(dev + " | 物理 " + mw + "x" + mh + " | DPI " + dx + " | 缩放 " + (dx*100/96) + "% | 工作区 " + ww + "x" + wh + " @" + wx + "," + wy + " | " + (prim ? "主屏" : "副屏") + " | hMon=" + hMon);
    return true;
  }
  public static string WindowInfo(IntPtr h) {
    uint dpi = GetDpiForWindow(h);
    RECT r; GetWindowRect(h, out r);
    IntPtr mon = MonitorFromWindow(h, 2);
    return "DPI=" + dpi + "(" + (dpi*100/96) + "%) 窗口物理 " + (r.R-r.L) + "x" + (r.B-r.T) + " @" + r.L + "," + r.T + " | 所在屏 hMon=" + mon;
  }
}
"@
[Env]::MakeAware()
Write-Output "=== 显示器 ==="
[Env]::Monitors() | ForEach-Object { Write-Output ("  " + $_) }
Write-Output ""
Write-Output "=== 应用窗口 ==="
$proc = Get-Process -Name TodoWpf -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "  应用未运行" } else {
  $AE=[System.Windows.Automation.AutomationElement]; $root=$AE::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
  foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) {
    $h = [IntPtr]$w.Current.NativeWindowHandle
    if ($h -eq [IntPtr]::Zero) { continue }
    if ($w.Current.Name -eq '桌面待办') { Write-Output ("  主窗: " + [Env]::WindowInfo($h)) }
  }
}
