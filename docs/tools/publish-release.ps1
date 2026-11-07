# C4 release script: dotnet publish (self-contained) -> vpk pack -> releases dir
# Produces Setup.exe + full/delta packages + RELEASE-NOTES.txt. ASCII only (PS 5.1 no-BOM safe).
param(
  [Parameter(Mandatory = $true)][string]$Version,
  [string]$Notes = ""
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # docs/tools -> repo root
$vpk = Join-Path $env:USERPROFILE '.dotnet\tools\vpk.exe'
if (-not (Test-Path $vpk)) { throw "vpk not found at $vpk (dotnet tool install -g vpk)" }

Write-Output "publish $Version (self-contained, may take a while) ..."
& dotnet publish (Join-Path $repo 'src\TodoWpf\TodoWpf.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -o (Join-Path $repo 'publish')
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# App display name for shortcuts: 桌面待办WPF built from char codes (keeps this file ASCII).
# NOTE: deliberately NOT "桌面待办" — the old Electron app (1.1.10, desktop-todo) is still
# installed under that name; same display name would collide in Apps&features and overwrite
# its shortcuts (2026-11-08 incident, restored manually).
$title = -join ([char]0x684C, [char]0x9762, [char]0x5F85, [char]0x529E, [char]0x57, [char]0x50, [char]0x46)

Write-Output "vpk pack ..."
& $vpk pack --packId TodoWpf --packTitle $title --packVersion $Version --packDir (Join-Path $repo 'publish') --mainExe TodoWpf.exe --outputDir (Join-Path $repo 'releases')
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

Set-Content -Path (Join-Path $repo 'releases\RELEASE-NOTES.txt') -Value $Notes -Encoding UTF8
Write-Output "DONE: releases ready ($Version)"
Get-ChildItem (Join-Path $repo 'releases') | ForEach-Object { Write-Output ("  {0}  {1:N0} bytes" -f $_.Name, $_.Length) }
