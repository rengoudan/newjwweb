# NewJwWeb

D:\code\jw（WinForms 版）的 Web 版本。保持核心业务流程，新增用户管理和业务数据操作审计。

- 本仓库**不引用** D:\code\jw 的任何工程，需要的类库按 Web 特性在这里重写。
- 唯一例外是 `native/JwwHelper`（C++/CLI 的 jww 读写库）：从旧仓库拷贝而来，C++ 源码未改。

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET 10 LTS，**Windows x64**（JwwHelper 依赖 C++/CLI + MFC） |
| Web | ASP.NET Core Blazor Web App（Interactive Server），上传/下载用 Minimal API |
| UI | MudBlazor |
| 数据 | EF Core 10 + SQL Server（新库，旧数据等测试需要时再导入） |
| 认证 | ASP.NET Core Identity（Cookie）+ 权限 Policy |
| 绘图 | 服务端算几何，前端 SVG/Canvas 渲染；3D 用 three.js |
| 日志 | Serilog |

## 目录结构

```
native/JwwHelper/        C++/CLI，.NET 10，x64（拷贝自旧仓库）
src/Jw.Domain/           实体、审计基类和接口、用户/角色、权限定义
src/Jw.Cad/              （第 3 阶段）几何、jww 解析、导出；Jww/ 子目录是唯一接触 JwwHelper 的地方
src/Jw.Application/      业务服务、DTO、权限校验、当前用户
src/Jw.Infrastructure/   DbContext、迁移、审计拦截器、初始数据
src/Jw.Web/              Blazor 站点（页面、登录、权限策略）
tests/Jw.Tests/          自动化测试（SQLite 内存库）
tools/JwwSmokeTest/      第 0 阶段：JwwHelper 读写冒烟测试
scripts/                 构建脚本
```

## 阶段

| 阶段 | 内容 | 状态 |
|---|---|---|
| 0 | JwwHelper 拷贝并改为 .NET 10 x64，验证读写 | ✅ 完成 |
| 1 | 解决方案骨架、登录、用户和角色、审计 | ✅ 完成 |
| 2 | 工事、顧客、予算、基礎データ 增删改查 | |
| 3 | 上传 jww、后台解析、楼层作业台、要確認 | |
| 4 | 图纸预览、全部导出和批量 zip、ワークベンチ | |
| 5 | 3D 预览、IIS 部署、旧数据导入工具 | |

## 第 0 阶段结论（2026-11-25）

**改动**：`native/JwwHelper/JwwHelper.vcxproj` 只改了两处编译配置，C++ 源码未改。
- `CLRSupport` 由 `true`（.NET Framework 4.7.2）改为 `NetCore`
- `TargetFramework` 由空改为 `net10.0`

**验证**：用 `tools/JwwSmokeTest` 做"读取 → 原样写出到日文路径 → 再读回"，逐实体比对数量和内容（坐标、文字等）。

| 样例（D:\code\jw） | 实体 | 结果 |
|---|---|---|
| 1伏図.jww | 線 491、文字 95、ソリッド 95、円弧 22、点 62 | 一致 |
| 2伏図.jww | 線 699、文字 99、ソリッド 88、円弧 24、点 62 | 一致 |
| 2FC.jww | 線 355 | 一致 |
| 積算根拠(伏図20240722).jww | 線 78780、文字 4159、ブロック 463 等 | 一致 |

**结论**：.NET 10 可以直接加载这份 JwwHelper，不需要另起进程包装。

## 第 1 阶段内容（2026-11-25）

**登录与账户**
- 登录、退出、修改密码。连续失败 5 次锁定 15 分钟。
- 新建用户或被重置密码后，必须先改密码才能使用其他页面。

**用户与角色**
- 预置角色：管理者（全部权限，不可减少）、作業者（业务全部）、閲覧者（只读）。可以新增自定义角色。
- 权限粒度为"模块.操作"，定义在 `src/Jw.Domain/Authorization/Permissions.cs`，后续阶段的模块已预先列出。
- 页面按钮和服务端都会校验权限。
- 用户只停用、不删除。系统会阻止：停用自己、去掉自己的管理员角色、让系统里一个有效管理员都没有。
- 角色或权限变更后，相关用户的已登录会话在 1 分钟内失效，重新登录后拿到新权限。

**审计**
- 业务实体继承 `AuditedEntity`，由拦截器自动填写创建人、修改人、删除人和时间。删除自动转为软删除。
- 实现了 `IAuditable` 的实体，增、改、删会逐字段记录变更前后的值，与业务数据在同一个事务里写入。
- 登录、登录失败、退出、改密码、用户和角色变更作为"セキュリティ"类记录。
- 每条记录都带操作者、IP、时间和关联 ID。审计页面可以筛选、展开看字段对比；没有修改和删除入口。
- 多人同时编辑同一条数据时，后保存的人会收到冲突提示，不会覆盖别人的修改。

**业务示例**：顧客的增删改查，作为审计和权限的完整样板，第 2 阶段照此扩展其他模块。

新增业务实体时要做的：
1. 继承 `AuditedEntity`，实现 `IAuditable`；
2. 在 `EntityDisplayNames`（审计说明）和 `FieldNames`（字段显示名）里补上名称。

## 开发运行

开发环境用本机 SQL Server 的 `NewJwWeb_Dev` 库，连接串在 `src/Jw.Web/appsettings.Development.json`。启动时会自动迁移，并创建初始管理员 `admin`：
- 初始密码取配置项 `Seed:AdminPassword`，首次登录后必须修改。
- 生产环境不配这个值时，会随机生成密码并写入日志，只出现这一次。

```powershell
dotnet run --project src/Jw.Web --launch-profile http
```

```powershell
dotnet test
```

修改实体后生成迁移：

```powershell
dotnet ef migrations add <名称> -p src/Jw.Infrastructure -s src/Jw.Infrastructure -o Persistence/Migrations
```

## 构建

需要 Visual Studio 2022 或更高版本，并安装"C++ 桌面开发"和"C++ MFC"组件。dotnet CLI 编不了 .vcxproj，所以要先编译 native，再编译 .NET 工程。

```powershell
./scripts/build-native.ps1
dotnet build NewJwWeb.sln
dotnet run --project tools/JwwSmokeTest -- "D:\code\jw\1伏図.jww"
```

## 部署前提（服务器）

- Windows Server x64
- ASP.NET Core 10 Hosting Bundle
- VC++ 2015-2022 x64 运行库（包含 MFC，JwwHelper 需要）
- 应用输出目录里要有 `Ijwhost.dll`（编译 JwwHelper 时自动生成）
