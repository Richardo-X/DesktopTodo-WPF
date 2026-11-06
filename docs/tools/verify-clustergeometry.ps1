# Same-screen regression check after ref-scale placement change:
# main & quick windows must stay adjacent (small gap), positions sane.
# ASCII only. Opens quick window via QuickBtn if needed, read-only otherwise.
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase

$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
$proc = Get-Process -Name TodoWpf | Select-Object -First 1
if (-not $proc) { Write-Output 'FAIL: app not running'; exit 1 }
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
function Wins { $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond) }
function ById($w, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function InvokeEl($e) {
    if ($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
function MainWin { foreach ($w in (Wins)) { if ((ById $w 'TodoList') -ne $null) { return $w } }; return $null }
function TplWin { foreach ($w in (Wins)) { if ((ById $w 'TemplateList') -ne $null) { return $w } }; return $null }

$m = MainWin
if (-not $m) { Write-Output 'FAIL: main window not found'; exit 1 }
for ($k = 0; $k -lt 3; $k++) {
    if (TplWin) { break }
    InvokeEl (ById $m 'QuickBtn'); Start-Sleep -Milliseconds 1200
}
$t = TplWin
if (-not $t) { Write-Output 'FAIL: quick window not open'; exit 1 }

$mr = $m.Current.BoundingRectangle
$tr = $t.Current.BoundingRectangle
Write-Output ("MAIN  x={0:N0} y={1:N0} w={2:N0} h={3:N0}" -f $mr.X, $mr.Y, $mr.Width, $mr.Height)
Write-Output ("QUICK x={0:N0} y={1:N0} w={2:N0} h={3:N0}" -f $tr.X, $tr.Y, $tr.Width, $tr.Height)
$gapRightOfMain = $tr.X - ($mr.X + $mr.Width)   # quick sits right of main
$gapLeftOfMain  = $mr.X - ($tr.X + $tr.Width)   # quick sits left of main
Write-Output ("GAP if-quick-right-of-main = {0:N1}px ; if-quick-left-of-main = {1:N1}px" -f $gapRightOfMain, $gapLeftOfMain)
$adjacent = ($gapRightOfMain -gt -2 -and $gapRightOfMain -lt 20) -or ($gapLeftOfMain -gt -2 -and $gapLeftOfMain -lt 20)
Write-Output ("ADJACENT={0}" -f $adjacent)
