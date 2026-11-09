# C4 release script: dotnet publish (self-contained) -> vpk pack -> releases dir
# Produces Setup.exe + full/delta packages + RELEASE-NOTES.txt. ASCII only (PS 5.1 no-BOM safe).
param(
  [Parameter(Mandatory = $true)][string]$Version,
  [string]$Notes = "",
  [switch]$SkipUpload   # 只本地出包、不上传（调试用）。正式发布必须上传，否则其他用户收不到更新
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

# 幂等：若本地 releases/ 已含本版本（上次发布中途失败会留下），先剔除再打包——
# vpk pack 拒绝"已存在同版本或更高版本"（2026-11-09 踩坑：上传失败后重试必须升版本号）。
$prune = Join-Path $repo 'docs\tools\prune-releases.py'
if (Test-Path $prune) {
    & python $prune --dir (Join-Path $repo 'releases') --drop-version $Version | Out-Null
}

Write-Output "vpk pack ..."
# --noPortable：便携版不做（C4 定稿）。注意不能用"打包后删 zip"代替——
# vpk upload 仍会按清单去找 Portable.zip，缺失即上传失败（2026-11-09 实测踩坑）。
& $vpk pack --packId TodoWpf --packTitle $title --packVersion $Version --packDir (Join-Path $repo 'publish') --mainExe TodoWpf.exe --noPortable --outputDir (Join-Path $repo 'releases')
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

# 上传到 GitHub Releases（其他用户从这里检查/下载更新；私有仓库拉不到，故必须公开）
$repoUrl = 'https://github.com/Richardo-X/DesktopTodo-WPF'
if ($SkipUpload) {
    Write-Output "SKIP upload (-SkipUpload): 未上传到 GitHub，其他用户不会收到本版本"
} else {
    Write-Output "uploading to GitHub Releases ..."
    $token = (& gh auth token 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'gh 未登录，无法上传（先 gh auth login）' }

    # 幂等：同名 release 若还停在草稿（上次上传中途失败会留下），先删掉再重建，
    # 否则 vpk 会报 "already an existing release tagged" 且 merge 不支持覆盖同名资产。
    # 用 release list 而非 release view：后者在"不存在"时会往 stderr 写错误，
    # 在 $ErrorActionPreference='Stop' 下会被当成异常中断（2026-11-09 实测踩坑）。
    # 再包一层容错：GitHub 偶发 5xx/网络抖动不该让整条发布流程失败——
    # 读不到列表就跳过清理，真有同名 release 时下一步上传会明确报错（2026-11-09 实测踩坑）。
    $all = $null
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $all = (& gh release list --repo $repoUrl --limit 100 --json tagName,isDraft 2>$null) } catch { }
    $ErrorActionPreference = $prevEap
    if ($LASTEXITCODE -ne 0) {
        Write-Output "WARN: 读不到 release 列表（GitHub 服务端/网络问题），跳过草稿清理继续上传"
        $all = $null
    }
    if ($all) {
        $match = ($all | ConvertFrom-Json) | Where-Object { $_.tagName -eq "v$Version" }
        if ($match -and $match.isDraft) {
            Write-Output "removing leftover draft release v$Version ..."
            & gh release delete "v$Version" --repo $repoUrl --yes --cleanup-tag 2>$null
        } elseif ($match) {
            throw "v$Version 已正式发布，不能覆盖（请提升版本号）"
        }
    }

    & $vpk upload github --repoUrl $repoUrl --token $token --outputDir (Join-Path $repo 'releases') `
        --publish --releaseName "$title $Version" --tag "v$Version"
    if ($LASTEXITCODE -ne 0) { throw 'vpk upload github failed' }

    # 附带自建安装器（用户从 Release 页面手动安装时用）。
    # 资产名用 **ASCII**：中文名经 shell 传参会乱码（实测上传成 'WPF-.exe'），且下载页 ASCII 更稳。
    $instUpload = Join-Path $env:TEMP "DesktopTodoWPF-Setup-$Version.exe"
    Copy-Item $instDst $instUpload -Force
    & gh release upload "v$Version" $instUpload --repo $repoUrl --clobber
    if ($LASTEXITCODE -ne 0) { throw 'gh release upload (installer) failed' }
}

Write-Output "DONE: releases ready ($Version)"
Get-ChildItem (Join-Path $repo 'releases') | ForEach-Object { Write-Output ("  {0}  {1:N0} bytes" -f $_.Name, $_.Length) }
