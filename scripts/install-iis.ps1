# 在 IIS 上首次配置 Jw.Web（需要管理员权限，在服务器上执行）。
# 前提：已复制 publish.ps1 的输出到 -SitePath；已安装 ASP.NET Core 10 Hosting Bundle、VC++ 2015-2022 x64 运行库、IIS WebSocket 协议。
# 用法示例：
#   ./install-iis.ps1 -SitePath D:\Sites\JwWeb -DataRoot D:\JwData -Port 80 `
#       -ConnectionString "Server=.;Database=NewJwWeb;Trusted_Connection=True;TrustServerCertificate=True"
# 重复执行是安全的：已存在的应用池、站点、配置文件不会被覆盖（配置文件只在不存在时生成）。
#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory)] [string]$SitePath,
    [Parameter(Mandatory)] [string]$DataRoot,
    [string]$SiteName = 'JwWeb',
    [string]$AppPool = 'JwWeb',
    [int]$Port = 80,
    [string]$HostName = '',
    [string]$ConnectionString = ''
)
$ErrorActionPreference = 'Stop'

# ---- 前提检查 ----
function Test-Prerequisite([string]$name, [bool]$ok, [string]$hint) {
    if ($ok) { Write-Host "[OK] $name" -ForegroundColor Green }
    else { Write-Host "[NG] $name — $hint" -ForegroundColor Red; $script:failed = $true }
}
$failed = $false
Test-Prerequisite 'IIS' (Test-Path "$env:windir\System32\inetsrv\appcmd.exe") 'IIS（Web サーバー）をインストールしてください'
Test-Prerequisite 'ASP.NET Core Module V2（Hosting Bundle）' `
    (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll") 'ASP.NET Core 10 Hosting Bundle をインストールしてください'
$vc = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
Test-Prerequisite 'VC++ 2015-2022 x64 ランタイム（MFC を含む）' ($null -ne $vc -and $vc.Installed -eq 1) 'vc_redist.x64.exe をインストールしてください'
Test-Prerequisite 'IIS WebSocket プロトコル' (Test-Path "$env:windir\System32\inetsrv\iiswsock.dll") 'Blazor Server の通信に必要です（Windows の機能 → WebSocket プロトコル）'
Test-Prerequisite '発行ファイル' ((Test-Path (Join-Path $SitePath 'Jw.Web.dll')) -and (Test-Path (Join-Path $SitePath 'JwwHelper.dll'))) "$SitePath に publish.ps1 の出力をコピーしてください"
if ($failed) { throw '前提条件を満たしていません。上の [NG] を解消してから再実行してください。' }

Import-Module WebAdministration

# ---- データ用フォルダー（サイト外）。再デプロイで消えないように分ける ----
$files = Join-Path $DataRoot 'files'
$keys = Join-Path $DataRoot 'keys'
$logs = Join-Path $DataRoot 'logs'
foreach ($d in $files, $keys, $logs) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

# ---- アプリケーションプール：.NET CLR なし、64 ビット、常時起動（解析ジョブはメモリ上で動くため） ----
if (-not (Test-Path "IIS:\AppPools\$AppPool")) { New-WebAppPool -Name $AppPool | Out-Null }
$pool = "IIS:\AppPools\$AppPool"
Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
Set-ItemProperty $pool -Name enable32BitAppOnWin64 -Value $false
Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty $pool -Name processModel.loadUserProfile -Value $true
# 原生 JwwHelper は同時に 1 つしか処理できない（アプリ内で直列化）。ワーカープロセスは 1 つのまま
Set-ItemProperty $pool -Name processModel.maxProcesses -Value 1

# ---- サイト ----
if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $AppPool -Port $Port -HostHeader $HostName | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPool
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationDefaults.preloadEnabled -Value $true

# ---- アクセス権：アプリケーションプールの ID にデータフォルダーの変更権限 ----
$identity = "IIS AppPool\$AppPool"
& icacls $DataRoot /grant "${identity}:(OI)(CI)M" /T /Q | Out-Null
& icacls $SitePath /grant "${identity}:(OI)(CI)RX" /T /Q | Out-Null

# ---- 本番設定（存在しないときだけ作る。既存の設定は上書きしない） ----
$prod = Join-Path $SitePath 'appsettings.Production.json'
if (-not (Test-Path $prod)) {
    $settings = [ordered]@{
        Storage        = @{ Root = $files }
        DataProtection = @{ KeysPath = $keys }
        Serilog        = @{
            # 配列は添字ごとに appsettings.json とマージされる：[0]=Console、[1]=File のパスだけ差し替える
            WriteTo = @(
                @{ Name = 'Console' },
                @{ Name = 'File'; Args = @{ path = (Join-Path $logs 'jw-.log'); rollingInterval = 'Day'; retainedFileCountLimit = 60 } }
            )
        }
    }
    if ($ConnectionString) { $settings.ConnectionStrings = @{ Default = $ConnectionString } }
    $settings | ConvertTo-Json -Depth 6 | Out-File -Encoding utf8 $prod
    Write-Host "作成：$prod"
} else {
    Write-Host "既存の設定を使います：$prod"
}

Restart-WebAppPool -Name $AppPool
Write-Host ''
Write-Host "完了：http://$(if ($HostName) { $HostName } else { 'localhost' }):$Port/" -ForegroundColor Green
Write-Host "・SQL Server が Windows 認証なら、ログイン「$identity」を作成し、データベースの db_owner（初回マイグレーション用）を付与してください。"
Write-Host "・初回起動時に管理者 admin が作られます。パスワードは Seed:AdminPassword、未設定なら $logs のログに 1 回だけ出力されます。"
