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

# 安装器（C4）：Velopack 的 Setup 没有目录选择界面，故自建安装器把用户选定的位置透传给 Setup。
# 必须在上一步 vpk pack 之后构建——要把刚生成的 Setup.exe 内嵌进去（单文件交付）。
Write-Output "building installer (self-contained single-file, embeds Setup) ..."
$instOut = Join-Path $repo 'installer-out'
if (Test-Path $instOut) { Remove-Item $instOut -Recurse -Force }
# 注意：-p:SetupPayload= 的值必须先落到变量——PowerShell 不解析紧跟等号的 (表达式)
$setupForEmbed = Join-Path $repo 'releases\TodoWpf-win-Setup.exe'
& dotnet publish (Join-Path $repo 'src\TodoWpf.Setup\TodoWpf.Setup.csproj') -c Release -o $instOut `
    "-p:Version=$Version" "-p:SetupPayload=$setupForEmbed"
if ($LASTEXITCODE -ne 0) { throw 'installer publish failed' }

# 安装器文件名：桌面待办WPF-安装程序.exe（字符码拼接，保持本脚本 ASCII）
$instName = ((-join ([char]0x684C, [char]0x9762, [char]0x5F85, [char]0x529E, [char]0x57, [char]0x50, [char]0x46, [char]0x2D, [char]0x5B89, [char]0x88C5, [char]0x7A0B, [char]0x5E8F)) + '.exe')
# 防呆（2026-11-09）：本脚本曾因 UTF-8 无 BOM 被 PS 5.1 误解码、中文注释吞掉下一行，
# 导致 $instName 为空 → 拷贝落进 releases 目录（保留原名），静默产出错误文件名。
# 断言把这种静默失败变成显式报错，并校验拷贝后尺寸一致。
if ([string]::IsNullOrEmpty($instName) -or -not $instName.EndsWith('.exe')) {
    throw "installer name build failed (instName='$instName')"
}
$instSrc = Join-Path $instOut 'TodoWpf.Setup.exe'
$instDst = Join-Path $repo "releases\$instName"
Copy-Item $instSrc $instDst -Force -ErrorAction Stop
if ((Get-Item $instDst -ErrorAction Stop).Length -ne (Get-Item $instSrc -ErrorAction Stop).Length) {
    throw 'installer copy size mismatch'
}
Remove-Item $instOut -Recurse -Force
Write-Output "DONE: releases ready ($Version)"
Get-ChildItem (Join-Path $repo 'releases') | ForEach-Object { Write-Output ("  {0}  {1:N0} bytes" -f $_.Name, $_.Length) }
