$ErrorActionPreference='Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Windows.Forms
$AE=[System.Windows.Automation.AutomationElement]; $root=$AE::RootElement
$proc = Get-Process -Name TodoWpf | Select-Object -First 1
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
function Wins { $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond) }
function ById($w,[string]$id){ $c=New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty,$id); $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c) }
function MainWin { foreach($w in (Wins)){ if((ById $w 'TodoList') -ne $null){ return $w } }; return $null }
function Shot {
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $g.Dispose()
  return $bmp
}
function DiffPct($a, $b) {
  $diff = 0; $total = 0
  for ($y = 0; $y -lt $a.Height; $y += 4) {
    for ($x = 0; $x -lt $a.Width; $x += 4) {
      $total++
      $ca = $a.GetPixel($x, $y); $cb = $b.GetPixel($x, $y)
      if ([math]::Abs($ca.R - $cb.R) + [math]::Abs($ca.G - $cb.G) + [math]::Abs($ca.B - $cb.B) -gt 24) { $diff++ }
    }
  }
  if ($total -eq 0) { return 0 }
  return [math]::Round(100 * $diff / $total, 2)
}
$m = MainWin
Write-Output ("主窗矩形: " + [int]$m.Current.BoundingRectangle.X + "," + [int]$m.Current.BoundingRectangle.Y)
$before = Shot
# 先打开模板窗，再用模板「＋新建」打开编辑窗（走同一 TextEditDialog）
$qb = ById $m 'QuickBtn'
$tpl = $null
for ($k = 0; $k -lt 3; $k++) {
  foreach ($w in (Wins)) { if ((ById $w 'TemplateList') -ne $null) { $tpl = $w; break } }
  if ($tpl) { break }
  if ($qb) { $qb.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1100 }
}
Write-Output ("模板窗: " + $(if ($tpl) { '已打开' } else { '未打开' }))
$before = Shot
$s1 = $null
if ($tpl) { $s1 = ById $tpl 'AddTemplateBtn' }
if (-not $s1) { Write-Output '未找到模板新建按钮'; exit }
$s1.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 1500
$mid = Shot
$d1 = DiffPct $before $mid
Write-Output ("点模板＋新建后屏幕变化: " + $d1 + "%  → " + $(if ($d1 -gt 0.5) { '编辑窗已绘制(PASS)' } else { '画面无变化(FAIL)' }))
# 关闭：找 UIA 里带 BackBtn 的窗（含模态）——用 TreeWalker 从根往下找
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$found = $null
function Scan($el, $depth) {
  if ($depth -gt 4 -or $el -eq $null) { return $null }
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, 'BackBtn')
  $b = $el.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
  if ($b) { return $b }
  $ch = $walker.GetFirstChild($el)
  while ($ch -ne $null) { $r = Scan $ch ($depth + 1); if ($r) { return $r }; $ch = $walker.GetNextSibling($ch) }
  return $null
}
$back = Scan $root 0
if ($back) {
  $back.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Milliseconds 1200
  $after = Shot
  $d2 = DiffPct $mid $after
  Write-Output ("点返回后屏幕变化: " + $d2 + "%  → " + $(if ($d2 -gt 0.5) { '编辑窗已关闭(PASS)' } else { '画面无变化(FAIL)' }))
} else { Write-Output '未找到返回键（无法自动关闭）' }
