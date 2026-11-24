# 编译 native/JwwHelper（C++/CLI, .NET 10, x64）。
# dotnet CLI 无法编译 .vcxproj，必须用 Visual Studio 的 MSBuild，且需要“C++ MFC”组件。
param([ValidateSet('Release','Debug')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.ATLMFC -property installationPath
if (-not $vs) { throw '未找到安装了 C++ MFC 组件的 Visual Studio' }
$msbuild = Join-Path $vs 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$proj = Join-Path $PSScriptRoot '..\native\JwwHelper\JwwHelper.vcxproj'
& $msbuild $proj "-p:Configuration=$Configuration" "-p:Platform=x64" "-v:m" "-nologo"
if ($LASTEXITCODE -ne 0) { throw "JwwHelper 编译失败 ($LASTEXITCODE)" }
