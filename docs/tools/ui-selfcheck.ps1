$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class Win {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  public static int CountTitle(uint target, string title) {
    int n = 0;
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target) { var t = new StringBuilder(256); GetWindowTextW(h, t, 256); if (t.ToString() == title) n++; }
      return true;
    }, IntPtr.Zero);
    return n;
  }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
$out = New-Object System.Collections.Generic.List[string]
function Log([string]$n, [bool]$ok, [string]$d) {
  $t = if ($ok) { 'PASS' } else { 'FAIL' }
  $script:out.Add("[$t] $n — $d")
}
$proc = Get-Process -Name TodoWpf -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output '[FAIL] 应用未运行'; exit 1 }
$PIDU = [uint32]$proc.Id
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
function Wins { $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond) }
function ById($w, [string]$id) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
  $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function ByType($w, $t) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $t)
  $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function InvokeEl($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function MainWin { foreach ($w in (Wins)) { if ((ById $w 'TodoList') -ne $null) { return $w } }; return $null }
function DialogCount { [Win]::CountTitle($PIDU, '桌面待办') }
function BatchCount {
  $m = MainWin
  if (-not $m) { return $null }
  foreach ($t in (ByType $m ([System.Windows.Automation.ControlType]::Text))) {
    if ($t.Current.Name -match '^已选\s*\d+\s*项$') { return $t.Current.Name }
  }
  return $null
}
function ExitBatch {
  for ($k = 0; $k -lt 4; $k++) {
    $m = MainWin; if (-not $m) { return }
    $e = ById $m 'BatchExitBtn'; if (-not $e) { return }
    InvokeEl $e; Start-Sleep -Milliseconds 450
  }
}
function OpenAndCloseModal([string]$btnId, [string]$label) {
  $before = DialogCount
  $m = MainWin
  $b = ById $m $btnId
  if (-not $b) { Log $label $false "按钮 $btnId 未找到"; return }
  try { InvokeEl $b } catch { Log $label $false "Invoke 异常: $_"; return }
  Start-Sleep -Milliseconds 1000
  $after = DialogCount
  Log $label ($after -gt $before) ("同名窗数 " + $before + " -> " + $after)
  # 关闭：找到最新对话框窗口，用其「返回」键 Invoke（不依赖键盘焦点）
  if ($after -gt $before) {
    $closed = $false
    foreach ($w in (Wins)) {
      if ($w.Current.Name -ne '桌面待办') { continue }
      if ((ById $w 'TodoList') -ne $null) { continue }   # 主窗跳过
      $back = ById $w 'BackBtn'
      if ($back) { try { InvokeEl $back; $closed = $true } catch { } ; Start-Sleep -Milliseconds 700; break }
    }
    Log ($label + '·关闭') ($closed -and (DialogCount -le $before)) ("同名窗数回落至 " + (DialogCount))
  }
}
ExitBatch
$m = MainWin
if (-not $m) { Write-Output '[FAIL] 未定位主窗'; exit 1 }
$r = $m.Current.BoundingRectangle
Log '主窗定位' $true ("矩形=" + [int]$r.X + "," + [int]$r.Y + " " + [int]$r.Width + "x" + [int]$r.Height + "（scale≈" + [math]::Round([int]$r.Width / 360, 3) + "）")
Log '常规态' ((BatchCount) -eq $null) '批量条未展开'
OpenAndCloseModal 'SettingsBtn' '设置窗可打开/关闭'
$m = MainWin
$qb = ById $m 'QuickBtn'
if ($qb) { InvokeEl $qb; Start-Sleep -Milliseconds 900 }
$tpl = $null
foreach ($w in (Wins)) { if ((ById $w 'TemplateList') -ne $null) { $tpl = $w; break } }
Log '模板窗可开关' ($tpl -ne $null) ("模板窗=" + $(if ($tpl) { '已出现' } else { '未出现' }))
if ($tpl) { OpenAndCloseModal 'AddTemplateBtn' '编辑窗可打开/关闭' }
ExitBatch
$m = MainWin
$b = ById $m 'BatchToggleBtn'
if ($b) {
  InvokeEl $b; Start-Sleep -Milliseconds 700
  $c0 = BatchCount
  Log '批量条出现' ($c0 -ne $null) ("计数=" + $c0)
  if ($c0) {
    $ws = New-Object -ComObject WScript.Shell
    $seq = @()
    for ($i = 0; $i -lt 5; $i++) {
      $cb = (ByType (MainWin) ([System.Windows.Automation.ControlType]::CheckBox))[0]
      $cb.SetFocus(); Start-Sleep -Milliseconds 60
      $ws.SendKeys(' ')
      Start-Sleep -Milliseconds 150
      $seq += (BatchCount)
    }
    Log '勾选框可交互(Click 生效)' ($seq[0] -eq '已选 1 项') ("首次切换后=" + $seq[0])
    Log '快速连点切换' (($seq | Select-Object -Unique).Count -ge 2) ("5 次序列=" + ($seq -join ' -> '))
    $all = ById (MainWin) 'BatchAllBtn'
    if ($all) { InvokeEl $all; Start-Sleep -Milliseconds 500; Log '全选' ((BatchCount) -match '已选 10 项') ("全选后=" + (BatchCount)) }
  }
}
ExitBatch
Log '收尾恢复常规态' ((BatchCount) -eq $null) '批量条已收起'
$out | ForEach-Object { Write-Output $_ }
