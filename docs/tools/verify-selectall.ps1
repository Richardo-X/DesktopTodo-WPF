# 验证模板批量「全选」是否让行显示选中（全选前后截图对比）
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W5 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
$proc = Get-Process -Name TodoWpf | Select-Object -First 1
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
function Wins { $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond) }
function ById($w, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function InvokeEl($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function MainWin { foreach ($w in (Wins)) { if ((ById $w 'TodoList') -ne $null) { return $w } }; return $null }
function TplWin { foreach ($w in (Wins)) { if ((ById $w 'TemplateList') -ne $null) { return $w } }; return $null }
function Shot {
    $h = [IntPtr](TplWin).Current.NativeWindowHandle
    $rc = New-Object W5+RECT
    [void][W5]::GetWindowRect($h, [ref]$rc)
    $W = $rc.R - $rc.L; $H = $rc.B - $rc.T
    $bmp = New-Object System.Drawing.Bitmap($W, $H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rc.L, $rc.T, 0, 0, (New-Object System.Drawing.Size($W, $H)))
    $g.Dispose()
    return $bmp
}
function DiffPct($a, $b) {
    $d = 0; $t = 0
    for ($y = [int]($a.Height * 0.25); $y -lt [int]($a.Height * 0.92); $y += 2) {
        for ($x = [int]($a.Width * 0.05); $x -lt [int]($a.Width * 0.92); $x += 2) {
            $t++
            $p = $a.GetPixel($x, $y); $q = $b.GetPixel($x, $y)
            if ([math]::Abs($p.R - $q.R) + [math]::Abs($p.G - $q.G) + [math]::Abs($p.B - $q.B) -gt 18) { $d++ }
        }
    }
    if ($t -eq 0) { return 0 }
    return [math]::Round(100 * $d / $t, 2)
}

$m = MainWin
for ($k = 0; $k -lt 3; $k++) {
    if (TplWin) { break }
    $qb = ById $m 'QuickBtn'
    if ($qb) { InvokeEl $qb; Start-Sleep -Milliseconds 1200 }
}
if (-not (TplWin)) { Write-Output 'FAIL: 模板窗未显示'; exit 1 }

$bb = ById (TplWin) 'TemplateBatchBtn'
if (-not $bb) { Write-Output 'FAIL: 未找到模板垃圾桶（可能已在批量态）'; exit 1 }
InvokeEl $bb
Start-Sleep -Milliseconds 900
if ((ById (TplWin) 'TemplateBatchExitBtn') -eq $null) { Write-Output 'FAIL: 未进入批量'; exit 1 }
Write-Output '已进入模板批量'

$A = Shot
$cnt0 = (ById (TplWin) 'TemplateBatchCount').Current.Name
InvokeEl (ById (TplWin) 'TemplateBatchAllBtn')
Start-Sleep -Milliseconds 900
$B = Shot
$cnt1 = (ById (TplWin) 'TemplateBatchCount').Current.Name

$d = DiffPct $A $B
$A.Dispose(); $B.Dispose()
Write-Output ('全选前计数 = ' + $cnt0)
Write-Output ('全选后计数 = ' + $cnt1)
Write-Output ('全选前后画面差异 = ' + $d + '%   (行应出现选中底色 → 应明显 >0)')
$verdict = if ($d -gt 1.0) { 'PASS 行已显示选中' } else { 'FAIL 行未显示选中' }
Write-Output ('判定 = ' + $verdict)

InvokeEl (ById (TplWin) 'TemplateBatchExitBtn')
Start-Sleep -Milliseconds 700
