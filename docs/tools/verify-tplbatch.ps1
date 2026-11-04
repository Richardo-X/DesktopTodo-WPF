# 验证模板批量「选中→直接返回」是否有选中样式残留（前后截图对比法）
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W4 {
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
function ByType($w, $t) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $t)
    $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function InvokeEl($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function MainWin { foreach ($w in (Wins)) { if ((ById $w 'TodoList') -ne $null) { return $w } }; return $null }
function TplWin { foreach ($w in (Wins)) { if ((ById $w 'TemplateList') -ne $null) { return $w } }; return $null }

$ws = New-Object -ComObject WScript.Shell
$m = MainWin
for ($k = 0; $k -lt 3; $k++) {
    if (TplWin) { break }
    $qb = ById $m 'QuickBtn'
    if ($qb) { InvokeEl $qb; Start-Sleep -Milliseconds 1200 }
}
if (-not (TplWin)) { Write-Output 'FAIL: 模板窗未显示'; exit 1 }

function Shot {
    $h = [IntPtr](TplWin).Current.NativeWindowHandle
    $rc = New-Object W4+RECT
    [void][W4]::GetWindowRect($h, [ref]$rc)
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

$A = Shot
$bb = ById (TplWin) 'TemplateBatchBtn'
if (-not $bb) { Write-Output 'FAIL: 未找到模板垃圾桶'; exit 1 }
InvokeEl $bb
Start-Sleep -Milliseconds 800
if ((ById (TplWin) 'TemplateBatchExitBtn') -eq $null) { Write-Output 'FAIL: 未进入批量'; exit 1 }

$cb = (ByType (TplWin) ([System.Windows.Automation.ControlType]::CheckBox))[0]
$cb.SetFocus()
Start-Sleep -Milliseconds 200
$ws.SendKeys(' ')
Start-Sleep -Milliseconds 600
$cnt = ''
try { $cnt = (ById (TplWin) 'TemplateBatchCount').Current.Name } catch { $cnt = '(读取失败)' }
$B = Shot

InvokeEl (ById (TplWin) 'TemplateBatchExitBtn')
Start-Sleep -Milliseconds 1100
$C = Shot

$dAB = DiffPct $A $B
$dAC = DiffPct $A $C
$dBC = DiffPct $B $C
$A.Dispose(); $B.Dispose(); $C.Dispose()

Write-Output ('选中后计数          = ' + $cnt)
Write-Output ('进批量前 vs 选中后  = ' + $dAB + '%   (应明显 >0，说明选中样式确实显示过)')
Write-Output ('进批量前 vs 退出后  = ' + $dAC + '%   (应≈0，说明退出后无残留)')
Write-Output ('选中后   vs 退出后  = ' + $dBC + '%')
$verdict = if ($dAB -gt 0.3 -and $dAC -lt 0.3) { 'PASS 无残留' } else { 'FAIL 仍有残留' }
Write-Output ('判定 = ' + $verdict)
