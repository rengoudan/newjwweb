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
src/Jw.Cad/              jww 解析（Legacy/ 为移植的旧版解析器）、导出（Export/）、梁 3D 的 CSV 解析（Beam3D/）；原生调用经 JwwNative.Sync 串行
src/Jw.Application/      业务服务、DTO、权限校验、当前用户
src/Jw.Infrastructure/   DbContext、迁移、审计拦截器、初始数据
src/Jw.Web/              Blazor 站点（页面、登录、权限策略）
tests/Jw.Tests/          自动化测试（SQLite 内存库）
tools/JwwSmokeTest/      第 0 阶段：JwwHelper 读写冒烟测试
tools/ParseRegression/   与旧版解析・导出结果的回归比对
tools/LegacyImport/      旧版数据库 → 新库的迁移工具
deploy/                  IIS 部署说明
scripts/                 构建（native）、发布、IIS 初次配置
```

## 阶段

| 阶段 | 内容 | 状态 |
|---|---|---|
| 0 | JwwHelper 拷贝并改为 .NET 10 x64，验证读写 | ✅ 完成 |
| 1 | 解决方案骨架、登录、用户和角色、审计 | ✅ 完成 |
| 2 | 工事、顧客、予算、基礎データ 增删改查 | ✅ 完成 |
| 3 | 上传 jww、后台解析、楼层作业台、要確認 | ✅ 完成 |
| 4 | 图纸预览、全部导出和批量 zip、ワークベンチ | ✅ 完成 |
| 5 | 3D 预览、IIS 部署、旧数据导入工具 | ✅ 完成 |

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

## 第 2 阶段内容（2026-11-25）

| 模块 | 内容 | 与旧版的对应 |
|---|---|---|
| 工事 | 一览、搜索、新增/编辑/删除、详情页（概要、各层状态、解析统计、予算入口） | `NewProjectMainPage`；层数不能少于已导入层数，规则同旧版 |
| 顧客 | 增加工事数、解析設定状态；删除前检查是否有工事 | `JwCustomerPage` |
| 解析設定 | 每个顧客一套：梁輪郭線、注釈、柱、継手記号、識別範囲エラー、下階柱記号、ブレース | `CustomerDesignSetting` / `JwCustDesignConstData`；第 3 阶段解析时读取 |
| 基礎データ | 材料种类（梁/柱/金物）与材料（名称、仕様、共通識別子、単位、単価、数値パラメータ、摘要），默认材料 | `NewBaseDataPage`；同名同规格不能重复，与旧版一致 |
| 予算 | 每个工事一份；从材料选择后填数量，名称/规格/单价按当时快照保存；可改数量和单价；小計・消費税 10%・総合計 | `JwBudgePage` / `AddBudget` |
| 见積書 | 按旧版模板导出 Excel，格子位置不变；导出记入"業務操作"审计 | `ExcelExporter` + `template.xlsx`（已拷贝到 `src/Jw.Infrastructure/Templates`） |

与旧版不同的地方：
- **予算的建立方式**：旧版"予算の作成"必须先有解析结果，再自动生成明细。现在可以先建空予算、手工添加明细；按解析结果自动生成见第 4 阶段。
- **删除方式**：删除工事时，它的予算一起软删除。删除材料不影响已有予算明细，因为明细保存的是快照。
- **审计范围**：全部新表都纳入审计，包括逐字段变更、操作者、并发冲突检测。

## 第 3 阶段内容（2026-11-25）

| 模块 | 内容 | 与旧版的对应 |
|---|---|---|
| 解析器 | 旧版 `JwShapeCommon` 移植到 `src/Jw.Cad`（命名空间保持不变，便于对照），去掉 WinForms 绘图部分 | `JwFileHandle`、`JwCanvas` 等 |
| 上传 | `.jww`，最大 50MB，保存到 `Storage:Root`，记 SHA-256 | 旧版直接读本地路径 |
| 后台解析 | `ParseJobRunner`（BackgroundService + Channel），并发数 `Parse:MaxConcurrency`；页面实时显示各步骤进度；解析完成后由用户确认「保存」或「破棄」 | 旧版"解析→确认→保存" |
| 楼层作业台 | 図面 / 梁 / 柱 / 金物 / ブレース / 解析情報；梁可修改并记入审计 | `JwProjectSubPage` 等 |
| 要確認 | 无梁、无柱、穴なし梁 | 旧版ワークベンチ |

解析器的状态处理（多用户 Web 的要求）：
- 旧版 `JwFileConsts` 等静态可变状态全部删除。比例尺、精度、解析设定、导出设定、事件都放进每张图纸各自的 `JwDrawing` 对象，并发解析互不影响（有测试覆盖）。
- 原生 JwwHelper 不是线程安全的，所有调用都用 `JwwNative.Sync` 串行化。
- 原生读取原来以独占方式打开文件、且不检查打开结果，文件被其他句柄占用时会直接崩溃（SEHException）。已改为共享读，失败时抛 `IOException`。

与旧版对照（`tools/ParseRegression`，用旧版 DLL 生成基准后逐项比较）：
- 14 个用例全部一致，或只有梁编号后缀不同。
- **旧版自身的结果不确定**：`FindRectangles` 用 `Parallel.For`，同一张图多次解析，梁数会在 855–858 之间变化，梁编号后缀也会变。新版改为确定性的两阶段实现。
- 剩余差异只来自面积为 0 的退化矩形。新增选项 `ExcludeDegenerateRectangles`（默认开启）排除它们；关闭时与旧版完全一致。
- 顺带修正：重复导入时 MaxBeamScope 偏移会累加；部分图纸 `Pillars` 为 null 时崩溃。

## 第 4 阶段内容（2026-11-25）

| 模块 | 内容 | 与旧版的对应 |
|---|---|---|
| 图纸预览 | SVG：梁、柱、穴、B/BG、ブレース；滚轮缩放、拖动；点梁与表格联动 | 旧版画布 |
| 出力 | 梁 JW（多文件时打包 zip）、番付図上/下 JW、ブレース施工図、梁製造 CSV（Shift-JIS） | 各导出按钮，文件名同旧版 |
| 一括出力 | 一个工事所有层的全部图纸打成一个 zip，按层分文件夹 | 旧版逐层手动导出 |
| 系统设定 | 孔径、CSV Y 方向调整（管理员） | 旧版每台电脑各自的「CSV 設定」 |
| 予算自动生成 | 梁按材料汇总长度(m)，金物按共通識別子匹配材料，柱按 TaggTitle；匹配不到的显示为「識別失敗」；手工明细保留 | 旧版「予算の作成」 |
| ワークベンチ | 件数与待办：要確認、未取込、CSV 未出力 | 旧版ワークベンチ |

与旧版不同的地方：
- **导出使用该层导入时的解析设定**。旧版用的是"最后一次解析"留在静态变量里的设定，导出别的层时结果可能不对。
- 解析明细保存时记录顺序（`Seq` 列），从数据库导出与解析后直接导出逐实体一致（有测试覆盖）。
- 导出一致性：同一份解析数据分别交给旧版和新版导出，3 张图纸共 73 个文件完全一致。
- 孔径默认 17.5（旧版安装环境中实际配置的值）。旧版 CSV 设定中的 Hx/Zx 从未被读取，已去掉。
- 梁材料在导入时选择（旧版的「モジュール」）。

## 第 5 阶段内容（2026-11-25）

| 模块 | 内容 | 与旧版的对应 |
|---|---|---|
| 梁 3D | 从楼层作业台的梁一览或图纸预览打开。以该梁的加工 CSV 为唯一输入，显示 H 形钢、穴（有问题的穴显示为红色）、穴间距尺寸、面名；可切换"加工机方向"；鼠标移到穴上显示 CSV 行内容；另有 CSV 页签 | `JwBeam3D`（WPF + HelixToolkit）→ three.js。CSV 解析和穴的位置计算原样移植到 `src/Jw.Cad/Beam3D` |
| IIS 部署 | `scripts/publish.ps1`（发布并检查必需文件）、`scripts/install-iis.ps1`（检查前提，创建应用池和站点，设置数据目录、权限和生产配置）、`deploy/IIS.md` | 旧版为安装包 |
| 旧数据迁移 | `tools/LegacyImport`：从旧版 SQLite `jwdata.db` 或 SQL Server 迁移顧客、解析設定、基礎データ、工事、階、全部解析明细、予算 | 无 |

three.js（r187，MIT）放在 `src/Jw.Web/wwwroot/lib/three`，内网服务器不需要连外网。

### 旧数据迁移

先启动一次网站，让新库完成迁移，然后执行：

```powershell
dotnet run --project tools/LegacyImport -- --sqlite "D:\旧版\jwdata.db" --target "Server=.;Database=NewJwWeb;Trusted_Connection=True;TrustServerCertificate=True" --dry-run
```

```powershell
dotnet run --project tools/LegacyImport -- --sqlite "D:\旧版\jwdata.db" --target "Server=.;Database=NewJwWeb;Trusted_Connection=True;TrustServerCertificate=True"
```

- 旧库是 SQL Server 时，用 `--sqlserver "<连接串>"` 代替 `--sqlite`。旧库只读打开，不会被修改。
- **只能迁移到空库**：新库里只要有顧客或工事，就会中止，避免重复迁移。全部在一个事务里执行，失败时新库不变。
- 材料、材料種類、階沿用旧版的 GUID，因此梁的材料引用、解析明细的楼层引用不变。顧客和工事是旧版的整数 ID，会改为新 ID。
- 创建时间保留：旧版保存的是电脑本地时间，按 `--timezone`（默认东京）换算为 UTC。
- 迁移的创建记录会写入审计，操作者显示为"旧データ移行(Windows 用户名)"。
- 旧版没有保存解析设定。导出时使用该工事顧客的解析設定，并且不排除退化矩形，与旧版的解析结果保持一致。
- 迁移完成后自动核对（可用 `--no-verify` 跳过）：解析明细逐行、逐项与旧库比较，顺序也要相同。
- 旧版的操作日志只有 ID 和时间，不迁移。
- 实测：用 `RGBJWMain\bin\Debug` 的 jwdata.db 迁移到临时库。
  - 顧客 9、工事 4、階 4、梁 105、穴 539 等全部迁移，核对全部一致。
  - 迁移后的楼层，5 种导出全部正常。

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
dotnet ef migrations add <名称> -p src/Jw.Infrastructure -s src/Jw.Web -o Persistence/Migrations
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
- 详细步骤见 [deploy/IIS.md](deploy/IIS.md)（`scripts/publish.ps1` → `scripts/install-iis.ps1`）
