# IIS 部署

## 服务器前提

| 项目 | 说明 |
|---|---|
| Windows Server x64 | JwwHelper 是 C++/CLI + MFC，只能在 Windows x64 上运行 |
| IIS + WebSocket 协议 | Blazor Server 通过 WebSocket 通信；没有的话会退化为长轮询，操作明显变慢 |
| ASP.NET Core 10 Hosting Bundle | 提供 ASP.NET Core Module V2（`AspNetCoreModuleV2`）和 .NET 10 运行时 |
| VC++ 2015-2022 x64 运行库 | JwwHelper 依赖（含 MFC）。缺少时，读取或输出图纸会失败 |
| SQL Server | 新库。首次启动会自动迁移（`Database:AutoMigrate`） |

## 发布（开发机）

```powershell
./scripts/publish.ps1
```

输出在 `publish/Jw.Web`。脚本会检查 `JwwHelper.dll`、`Ijwhost.dll`、`Templates/*.jww`、`web.config` 是否都在。缺少任何一个，都会导致图纸读取或输出失败。

## 首次配置（服务器，管理员 PowerShell）

1. 把 `publish/Jw.Web` 复制到服务器，例如 `D:\Sites\JwWeb`。
2. 执行：

```powershell
./install-iis.ps1 -SitePath D:\Sites\JwWeb -DataRoot D:\JwData -Port 80 -ConnectionString "Server=.;Database=NewJwWeb;Trusted_Connection=True;TrustServerCertificate=True"
```

脚本做的事情：
- 检查前提（IIS、Hosting Bundle、VC++ 运行库、WebSocket、发布文件）。
- 在站点目录之外建立数据目录：`files`（上传的图纸）、`keys`（登录 Cookie 密钥）、`logs`。
- 应用池设为：无托管代码、64 位、`AlwaysRunning`、空闲超时 0、加载用户配置文件、最多 1 个工作进程；并开启站点预加载。
- 给应用池账号授权：数据目录可修改，站点目录可读。
- 生成 `appsettings.Production.json`，只在文件不存在时生成，不会覆盖已有配置。

3. SQL Server 用 Windows 认证时：
   - 为 `IIS AppPool\JwWeb` 创建登录名。
   - 授予 `db_owner`。首次迁移需要建表权限，之后可以降为 `db_datareader` + `db_datawriter`，同时把 `Database:AutoMigrate` 设为 `false`。
4. 初始管理员 `admin`：
   - 密码取配置项 `Seed:AdminPassword`。
   - 未配置时会随机生成，只在首次启动的日志里出现一次。
   - 首次登录后必须修改。

## 配置项（`appsettings.Production.json`）

| 键 | 说明 |
|---|---|
| `ConnectionStrings:Default` | 数据库连接串 |
| `Storage:Root` | 上传图纸的保存目录。**必须是站点目录之外的绝对路径**，否则重新部署时会丢失（启动日志会给出警告） |
| `DataProtection:KeysPath` | 登录 Cookie 密钥的保存目录。不配置时，应用池回收后所有人都要重新登录 |
| `Parse:MaxConcurrency` | 同时解析的图纸数，默认 2。原生读写在进程内是串行的 |
| `Parse:KeepResultMinutes` | 解析结果等待"保存/破棄"的时间，默认 60 分钟 |
| `Serilog:WriteTo:1:Args:path` | 日志文件路径 |
| `Seed:AdminPassword` | 初始管理员密码，只在首次建库时使用 |

## 更新版本

1. 用 `publish.ps1` 重新发布。
2. 在站点目录放一个 `app_offline.htm`，让 IIS 释放文件锁并停止应用。
3. 覆盖站点目录的文件。保留服务器上的 `appsettings.Production.json`，发布输出里不包含这个文件。
4. 删除 `app_offline.htm`。启动时会自动执行数据库迁移。

## 注意事项

- **解析任务在内存中**：应用池回收或更新版本时，正在解析、或解析完还没"保存"的任务会丢失，需要重新取込。应用池已设为 `AlwaysRunning`、空闲不超时，以减少这种情况。建议把定时回收安排在夜间，或者关闭定时回收。
- **工作进程只能有 1 个**（Web Garden 不可用）：
  - 解析任务和 Blazor 连接都在进程内存里。
  - 原生 JwwHelper 的串行锁只在进程内有效。
- 默认是进程内托管（`hostingModel="inprocess"`）。JwwHelper 加载在 `w3wp.exe` 里。
- 原生代码异常导致工作进程退出时，IIS 会自动重启它，详细信息看 Windows 事件日志和 `logs`。
