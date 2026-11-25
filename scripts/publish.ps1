# 发布 Jw.Web（IIS 用，Release，win-x64，依赖框架）。
# 用法：./scripts/publish.ps1 [-Output publish\Jw.Web] [-SkipNative]
# 先编译 native JwwHelper（需要 VS + C++ MFC），再 dotnet publish，最后检查 IIS 运行必需的文件是否都在。
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\publish\Jw.Web'),
    [switch]$SkipNative
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $SkipNative) {
    & (Join-Path $PSScriptRoot 'build-native.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'native JwwHelper 编译失败' }
}

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
dotnet publish (Join-Path $root 'src\Jw.Web\Jw.Web.csproj') -c Release -r win-x64 --self-contained false -o $Output -p:UseAppHost=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }

# IIS 上运行必需：原生 JwwHelper、C++/CLI 加载器 Ijwhost、JW 图纸模板、web.config（ASP.NET Core Module）
$required = @(
    'Jw.Web.dll', 'web.config', 'JwwHelper.dll', 'Ijwhost.dll',
    'Templates\template100.jww', 'Templates\template150.jww', 'Templates\template200.jww'
)
$missing = $required | Where-Object { -not (Test-Path (Join-Path $Output $_)) }
if ($missing) { throw "发布结果缺少文件：$($missing -join ', ')" }

# 发布目录里不应带开发用配置和本地数据
foreach ($f in 'appsettings.Development.json') {
    $p = Join-Path $Output $f
    if (Test-Path $p) { Remove-Item $p }
}

Write-Host "发布完成：$((Resolve-Path $Output).Path)" -ForegroundColor Green
Write-Host '下一步：把目录复制到服务器，并按 deploy/IIS.md 配置（首次可用 scripts/install-iis.ps1）。'
