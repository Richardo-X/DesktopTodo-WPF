# Verify batch-counter fixed width (Plan A): button X must not move when count text changes.
# ASCII only (PS 5.1 no-BOM safe). Read-only on data: select-all then exit batch, no delete.
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

function Snap($win, $countId, $btnId) {
    $c = ById $win $countId
    $b = ById $win $btnId
    if (-not $c -or -not $b) { return $null }
    $cr = $c.Current.BoundingRectangle
    $br = $b.Current.BoundingRectangle
    return @{ CountW = $cr.Width; CountText = $c.Current.Name; BtnX = $br.X }
}

function CheckBar($win, $enterId, $allId, $exitId, $countId, $btnId, $label) {
    $enter = ById $win $enterId
    $exitB = ById $win $exitId
    if ($enter) { InvokeEl $enter; Start-Sleep -Milliseconds 800 }
    elseif (-not $exitB) { Write-Output "$label SKIP: cannot enter batch (no items?)"; return }
    $a = Snap $win $countId $btnId
    if (-not $a) { Write-Output "$label FAIL: batch bar elements not found"; return }
    InvokeEl (ById $win $allId)
    Start-Sleep -Milliseconds 500
    $b = Snap $win $countId $btnId
    if (-not $b) { Write-Output "$label FAIL: bar lost after select-all"; return }
    $same = ([math]::Abs($a.BtnX - $b.BtnX) -lt 0.6) -and ([math]::Abs($a.CountW - $b.CountW) -lt 0.6)
    Write-Output ("{0}: before(countW={1:N1} btnX={2:N1} text='{3}') after(countW={4:N1} btnX={5:N1} text='{6}') SAME={7}" -f $label, $a.CountW, $a.BtnX, $a.CountText, $b.CountW, $b.BtnX, $b.CountText, $same)
    InvokeEl (ById $win $exitId)
    Start-Sleep -Milliseconds 500
}

$m = MainWin
if (-not $m) { Write-Output 'FAIL: main window not found'; exit 1 }
CheckBar $m 'BatchToggleBtn' 'BatchAllBtn' 'BatchExitBtn' 'BatchCount' 'BatchExitBtn' 'MAIN'

$t = TplWin
for ($k = 0; $k -lt 3; $k++) {
    if (TplWin) { break }
    InvokeEl (ById $m 'QuickBtn'); Start-Sleep -Milliseconds 1200
}
$t = TplWin
if ($t) {
    CheckBar $t 'TemplateBatchBtn' 'TemplateBatchAllBtn' 'TemplateBatchExitBtn' 'TemplateBatchCount' 'TemplateBatchDeleteBtn' 'TPL'
} else {
    Write-Output 'TPL SKIP: template window not available'
}
