using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Floors;
using Jw.Cad.Parsing;
using Jw.Domain.Auditing;
using Jw.Domain.Budgets;
using Jw.Domain.Common;
using Jw.Domain.Customers;
using Jw.Domain.Materials;
using Jw.Domain.Projects;
using JwCore;
using Microsoft.EntityFrameworkCore;
using ProjectStatus = Jw.Domain.Common.ProjectStatus;

namespace LegacyImport;

public sealed class ImportOptions
{
    /// <summary>只读取和检查，不写入新库。</summary>
    public bool DryRun { get; init; }
    /// <summary>旧库 CreationTime 的时区（旧版保存的是电脑本地时间）。</summary>
    public TimeZoneInfo SourceTimeZone { get; init; } = TimeZoneInfo.Local;
}

public sealed class ImportReport
{
    public Dictionary<string, int> Counts { get; } = [];
    public List<string> Warnings { get; } = [];
    public void Add(string what, int n) => Counts[what] = Counts.GetValueOrDefault(what) + n;
}

/// <summary>
/// 旧版数据 → 新库。
/// - 只能导入到"空库"（没有顧客和工事）：一次性迁移，避免重复导入造成重复数据。
/// - 全部在一个事务里，失败时新库不留下半截数据。
/// - 通过新版的数据上下文写入：顧客、材料、工事、階、予算的创建会记入审计（操作者为执行导入的账号）。
/// - 原来的创建时间保留（换算为 UTC）。
/// - 材料、材料种类、階沿用旧版的 GUID：梁的 BeamXHId（材料 ID）、解析明细的 JwProjectSubDataId（階 ID）引用都能保持。
/// </summary>
public sealed class LegacyImporter(IAppDbContextFactory dbFactory, IAuditLogger audit, ICurrentUser currentUser)
{
    public async Task<ImportReport> ImportAsync(LegacySource src, ImportOptions options, CancellationToken ct = default)
    {
        var report = new ImportReport();
        await using var db = await dbFactory.CreateAsync(ct);
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(ct) || await db.Projects.IgnoreQueryFilters().AnyAsync(ct))
            throw new InvalidOperationException("新しいデータベースに既に顧客または工事があります。旧データの移行は空のデータベースにだけ実行できます。");

        var created = new List<(object Entity, DateTime? LegacyTime)>();
        DateTime? Utc(DateTime? local) => local is { } t && t > DateTime.MinValue.AddDays(1)
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), options.SourceTimeZone)
            : null;

        // ---------- 顧客と解析設定 ----------
        var customerMap = new Dictionary<long, Customer>();
        var names = new UniqueNames();
        foreach (var r in await src.ReadAsync("JwCustomerDatas"))
        {
            var c = new Customer
            {
                CompanyName = names.Make("customer", r.Str("CompanyName").Trim() is { Length: > 0 } n ? n : $"顧客{r.Long("Id")}", report),
                Address = r.StrOrNull("CompanyAddress"), Contact = r.StrOrNull("Contact"), Telephone = r.StrOrNull("Telephone"),
            };
            customerMap[r.Long("Id")] = c;
            db.Customers.Add(c);
            created.Add((c, Utc(r.Time("CreationTime"))));
        }
        report.Add("顧客", customerMap.Count);

        var settingByCustomer = new Dictionary<Guid, CustomerParseSetting>();
        foreach (var r in await src.ReadAsync("JwCustDesignConstDatas"))
        {
            if (r["JwCustomerDataId"] is null || !customerMap.TryGetValue(r.Long("JwCustomerDataId"), out var c))
            {
                report.Warnings.Add($"顧客のない解析設定（Id={r.Long("Id")}）は移行しません。");
                continue;
            }
            if (settingByCustomer.ContainsKey(c.Id))
                report.Warnings.Add($"顧客「{c.CompanyName}」の解析設定が複数あります。後のものを使います。");
            var s = settingByCustomer.TryGetValue(c.Id, out var existing) ? existing : new CustomerParseSetting { CustomerId = c.Id };
            s.BeamColor = r.Int("BeamParseColorNumber");
            s.SplitColor = r.Int("BeamSplitParseColor");
            s.PillarColor = r.Int("BeamPillarParseColor");
            s.PillarPenStyle = r.Int("PillarPenStyle");
            s.NearSplitMax = r.Double("NearSpliteMax");
            s.SplitPenStyle = r.Int("SplitPenStyle");
            s.BeamTextColor = r.Int("BeamSymbolTextColorNumber");
            s.DownPillarColor = r.Int("DownPillarColorNumber");
            s.BraceColor = r.Int("LianjieColorNumber");
            s.BraceOrigin = (BraceOrigin)r.Int("LianjieParsingMethod");
            s.PillarRecognition = (ShapeRecognition)r.Int("PillarDrawingMethod");
            s.SplitRecognition = (ShapeRecognition)r.Int("SplitDrawingMethod");
            s.BraceComponent = (BraceComponent)r.Int("LianjieCompentType");
            s.PickPrecision = r.Int("PickPrecision");
            s.Gap = r.Double("JwJianxi");
            s.Scale = r.Double("JwScale");
            if (existing is null)
            {
                settingByCustomer[c.Id] = s;
                db.CustomerParseSettings.Add(s);
                created.Add((s, Utc(r.Time("CreationTime"))));
            }
        }
        report.Add("解析設定", settingByCustomer.Count);

        // ---------- 基礎データ（ID は旧版の GUID をそのまま使う） ----------
        var categoryIds = new HashSet<Guid>();
        var pendingDefaults = new List<(MaterialCategory Category, Guid DefaultId)>();
        foreach (var r in await src.ReadAsync("JwMaterialTypeDatas"))
        {
            var cat = new MaterialCategory
            {
                Id = GuidOf(r.Str("Id")), Kind = (MaterialKind)r.Int("MaterialType"),
                Name = names.Make("category", r.Str("MaterialTypeName").Trim() is { Length: > 0 } n ? n : "未分類", report),
            };
            if (Guid.TryParse(r.Str("DefaultDataId"), out var def)) pendingDefaults.Add((cat, def));
            categoryIds.Add(cat.Id);
            db.MaterialCategories.Add(cat);
            created.Add((cat, Utc(r.Time("CreationTime"))));
        }
        report.Add("材料種類", categoryIds.Count);

        var materials = new Dictionary<Guid, Material>();
        foreach (var r in await src.ReadAsync("JwMaterialDatas"))
        {
            var catId = GuidOf(r.Str("JwMaterialTypeDataId"));
            if (!categoryIds.Contains(catId))
            {
                report.Warnings.Add($"種類のない材料「{r.Str("MaterialName")}」は移行しません。");
                continue;
            }
            var name = r.Str("MaterialName").Trim();
            var spec = r.StrOrNull("MaterialParameter")?.Trim();
            var m = new Material
            {
                Id = GuidOf(r.Str("Id")), CategoryId = catId, Kind = (MaterialKind)r.Int("MaterialType"),
                Name = names.Make("material|" + spec, name, report), Spec = spec,
                GeneralTitle = r.StrOrNull("GeneralTitle")?.Trim(), UnitName = r.Str("UnitName"),
                UnitPrice = r.Decimal("UnitPrice"), NumParameter = r.Double("NumParameter"), Remark = r.StrOrNull("Remark"),
            };
            materials[m.Id] = m;
            db.Materials.Add(m);
            created.Add((m, Utc(r.Time("CreationTime"))));
        }
        foreach (var (cat, def) in pendingDefaults)
            if (materials.ContainsKey(def)) cat.DefaultMaterialId = def;
        report.Add("材料", materials.Count);

        // ---------- 工事と階 ----------
        var projectMap = new Dictionary<long, Project>();
        foreach (var r in await src.ReadAsync("JwProjectMainDatas"))
        {
            var p = new Project
            {
                Name = r.Str("ProjectName").Trim(), SiteAddress = r.StrOrNull("SiteAddress"),
                CustomerId = r["JwCustomerDataId"] is not null && customerMap.TryGetValue(r.Long("JwCustomerDataId"), out var c) ? c.Id : null,
                FloorQuantity = Math.Max(1, r.Int("FloorQuantity")), Status = (ProjectStatus)r.Int("ProjectStatus"),
                Scale = r.StrOrNull("Biaochi"), ExportCount = r.Int("ExportCount"),
            };
            projectMap[r.Long("Id")] = p;
            db.Projects.Add(p);
            created.Add((p, Utc(r.Time("CreationTime"))));
        }
        report.Add("工事", projectMap.Count);

        var floorMap = new Dictionary<string, Floor>(StringComparer.OrdinalIgnoreCase);
        var floorNames = new UniqueNames();
        foreach (var r in await src.ReadAsync("JwProjectSubDatas"))
        {
            if (!projectMap.TryGetValue(r.Long("JwProjectMainDataId"), out var p))
            {
                report.Warnings.Add($"工事のない階（{r.Str("FloorName")}）は移行しません。");
                continue;
            }
            var settings = p.CustomerId is { } cid ? settingByCustomer.GetValueOrDefault(cid) : null;
            var location = r["Location"] as NetTopologySuite.Geometries.Point;
            var f = new Floor
            {
                Id = GuidOf(r.Str("Id")), ProjectId = p.Id,
                Name = floorNames.Make(p.Id.ToString(), r.StrOrNull("FloorName")?.Trim() ?? "階", report),
                BeamCount = r.Int("BeamCount"), HorizontalBeamsCount = r.Int("HorizontalBeamsCount"), VerticalBeamsCount = r.Int("VerticalBeamsCount"),
                PillarCount = r.Int("PillarCount"), KPillarCount = r.Int("KPillarCount"), SinglePillarCount = r.Int("SinglePillarCount"),
                BCount = r.Int("BCount"), BGCount = r.Int("BGCount"), ExportCount = r.Int("ExportCount"),
                Scale = r.Double("Scale"), ScaleText = r.StrOrNull("Biaochi"),
                MarkBeam = r["MarkBeam"] is null ? null : r.Int("MarkBeam"),
                LocationX = location?.X ?? 0, LocationY = location?.Y ?? 0, Width = r.Double("Width"), Height = r.Double("Height"),
                DirectionType = r.Int("DirectionType"), KPillarType = r.Int("KPillarType"),
                BraceComponent = r.Int("LianjieCompentType"), MinimalStrategy = r.Long("MinimalStrategy") != 0,
            };
            // 旧版は解析設定を保存していなかった。出力に使う設定は、工事の顧客の解析設定から作る（旧版は「最後に解析した設定」を使っていた）。
            f.ParseOptionsJson = JsonSerializer.Serialize(OptionsOf(settings, f));
            if (settings is null) report.Warnings.Add($"工事「{p.Name}」の顧客に解析設定がありません（階「{f.Name}」は既定の設定で出力されます）。");
            floorMap[r.Str("Id")] = f;
            db.Floors.Add(f);
            created.Add((f, Utc(r.Time("CreationTime"))));
        }
        report.Add("階", floorMap.Count);

        foreach (var p in projectMap.Values)
        {
            var floors = floorMap.Values.Where(f => f.ProjectId == p.Id).ToList();
            p.ParsedQuantity = floors.Count;
            p.FloorQuantity = Math.Max(p.FloorQuantity, floors.Count);
            p.BeamCount = floors.Sum(f => f.BeamCount);
            p.PillarCount = floors.Sum(f => f.PillarCount);
            p.KPillarCount = floors.Sum(f => f.KPillarCount);
            p.SinglePillarCount = floors.Sum(f => f.SinglePillarCount);
            p.BCount = floors.Sum(f => f.BCount);
            p.BGCount = floors.Sum(f => f.BGCount);
            p.Scale ??= floors.FirstOrDefault()?.ScaleText;
        }

        // ---------- 解析明細（旧版のデータ類のまま。読み出し順＝解析時の順を保つ） ----------
        string? FloorKey(string? oldId) => oldId is not null && floorMap.TryGetValue(oldId, out var f) ? f.Id.ToString() : null;

        var beams = await DetailsAsync<JwBeamData>(src, "JwBeamDatas", report, "梁", b => (b.JwProjectSubDataId = FloorKey(b.JwProjectSubDataId)!) is not null);
        var beamIds = beams.Select(b => b.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var holes = await DetailsAsync<JwHoleData>(src, "JwHoleDatas", report, "穴", h => beamIds.Contains(h.JwBeamDataId ?? ""));
        var verticals = await DetailsAsync<JwBeamVerticalData>(src, "JwBeamVerticalDatas", report, "梁の縦線", v => beamIds.Contains(v.JwBeamDataId ?? ""));
        var pillars = await DetailsAsync<JwPillarData>(src, "JwPillarDatas", report, "柱", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        var links = await DetailsAsync<JwLinkPartData>(src, "JwLinkPartDatas", report, "金物", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        var lianjies = await DetailsAsync<JwLianjieData>(src, "JwLianjieDatas", report, "ブレース", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        var downs = await DetailsAsync<JwDownPillarData>(src, "JwDownPillarDatas", report, "下階柱", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        var cuttings = await DetailsAsync<JwCutting>(src, "JwCuttings", report, "切断", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        var marks = await DetailsAsync<JwAddedHoleMarkData>(src, "JwAddedHoleMarkDatas", report, "追加穴記号", x => (x.JwProjectSubDataId = FloorKey(x.JwProjectSubDataId)!) is not null);
        ParseRows.AddInOrder(db, db.FloorBeams, beams);
        ParseRows.AddInOrder(db, db.FloorHoles, holes);
        ParseRows.AddInOrder(db, db.FloorBeamVerticals, verticals);
        ParseRows.AddInOrder(db, db.FloorPillars, pillars);
        ParseRows.AddInOrder(db, db.FloorLinkParts, links);
        ParseRows.AddInOrder(db, db.FloorLianjies, lianjies);
        ParseRows.AddInOrder(db, db.FloorDownPillars, downs);
        ParseRows.AddInOrder(db, db.FloorCuttings, cuttings);
        ParseRows.AddInOrder(db, db.FloorAddedHoleMarks, marks);

        // ---------- 予算 ----------
        var budgetByOld = new Dictionary<string, Budget>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in await src.ReadAsync("JwBudgetMainDatas"))
        {
            if (!projectMap.TryGetValue(r.Long("JwProjectMainDataId"), out var p)) continue;
            if (budgetByOld.Values.Any(b => b.ProjectId == p.Id))
            {
                report.Warnings.Add($"工事「{p.Name}」の予算が複数あります。最初のものだけ移行します。");
                continue;
            }
            var b = new Budget { ProjectId = p.Id, Amount = r.Decimal("Amount") };
            budgetByOld[r.Str("Id")] = b;
            db.Budgets.Add(b);
            created.Add((b, Utc(r.Time("CreationTime"))));
        }
        var lineCount = 0;
        foreach (var r in await src.ReadAsync("JwBudgetSubDatas"))
        {
            if (!budgetByOld.TryGetValue(r.Str("JwBudgetMainDataId"), out var b)) continue;
            var materialId = Guid.TryParse(r.Str("JwMaterialDataId"), out var mid) && materials.ContainsKey(mid) ? mid : (Guid?)null;
            var line = new BudgetLine
            {
                BudgetId = b.Id, MaterialId = materialId, ItemName = r.Str("BudgetItemName"), Spec = r.StrOrNull("ModelParm"),
                UnitName = r.Str("UnitName"), Quantity = r.Decimal("Number"), UnitPrice = r.Decimal("UnitPrice"), Amount = r.Decimal("Amount"),
                Kind = (MaterialKind)r.Int("MaterialType"), Source = (BudgetLineSource)r.Int("BudgetType"),
            };
            db.BudgetLines.Add(line);
            created.Add((line, Utc(r.Time("CreationTime"))));
            lineCount++;
        }
        report.Add("予算", budgetByOld.Count);
        report.Add("予算明細", lineCount);

        var logs = await src.ReadAsync("JwOperateLogDatas");
        if (logs.Count > 0) report.Warnings.Add($"旧版の操作ログ {logs.Count} 件は移行しません（操作者の情報がないため）。");

        if (options.DryRun) return report;

        // ---------- 書き込み（1 トランザクション） ----------
        await using var tx = await db.Db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        // 作成日時は監査インターセプターが「今」にするので、旧版の日時に戻す（インターセプターを通さない一括更新）
        foreach (var (entity, time) in created)
        {
            if (time is not { } t) continue;
            await SetCreatedAtAsync(db, entity, t, ct);
        }
        await tx.CommitAsync(ct);

        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "LegacyImport",
            $"旧版データを移行：{src.Description}（{string.Join("、", report.Counts.Select(kv => $"{kv.Key} {kv.Value}"))}）")
        {
            Detail = new { report.Counts, report.Warnings, ImportedBy = currentUser.UserName },
        }, ct);
        return report;
    }

    /// <summary>
    /// 移行後の照合：解析明細を旧 DB と新 DB で 1 行ずつ、全項目比較する（階 ID の付け替えを除く）。
    /// 出力（JW 図面・CSV）は解析明細から作るので、ここが一致していれば出力も旧版と同じデータから作られる。
    /// </summary>
    public async Task<List<string>> VerifyAsync(LegacySource src, CancellationToken ct = default)
    {
        var problems = new List<string>();
        await using var db = await dbFactory.CreateAsync(ct);
        await CompareAsync<JwBeamData>(src, "JwBeamDatas", db.FloorBeams, problems, ct);
        await CompareAsync<JwHoleData>(src, "JwHoleDatas", db.FloorHoles, problems, ct);
        await CompareAsync<JwBeamVerticalData>(src, "JwBeamVerticalDatas", db.FloorBeamVerticals, problems, ct);
        await CompareAsync<JwPillarData>(src, "JwPillarDatas", db.FloorPillars, problems, ct);
        await CompareAsync<JwLinkPartData>(src, "JwLinkPartDatas", db.FloorLinkParts, problems, ct);
        await CompareAsync<JwLianjieData>(src, "JwLianjieDatas", db.FloorLianjies, problems, ct);
        await CompareAsync<JwDownPillarData>(src, "JwDownPillarDatas", db.FloorDownPillars, problems, ct);
        await CompareAsync<JwCutting>(src, "JwCuttings", db.FloorCuttings, problems, ct);
        await CompareAsync<JwAddedHoleMarkData>(src, "JwAddedHoleMarkDatas", db.FloorAddedHoleMarks, problems, ct);
        return problems;
    }

    private static async Task CompareAsync<T>(LegacySource src, string table, DbSet<T> set, List<string> problems, CancellationToken ct)
        where T : class, new()
    {
        var idProp = typeof(T).GetProperty("Id")!;
        var props = typeof(T).GetProperties()
            .Where(p => p.CanWrite && p.Name is not ("JwProjectSubDataId" or "Id")
                        && (p.PropertyType.IsValueType || p.PropertyType == typeof(string) || p.PropertyType == typeof(NetTopologySuite.Geometries.Point)))
            .ToArray();
        var stored = (await set.AsNoTracking().InOrder().ToListAsync(ct))
            .ToDictionary(x => (string)idProp.GetValue(x)!, StringComparer.OrdinalIgnoreCase);
        var legacy = await src.ReadAsync(table);
        var seq = 0;
        var orderBroken = false;
        var storedOrder = stored.Keys.ToList();
        foreach (var row in legacy)
        {
            var old = LegacySource.Materialize<T>(row);
            var id = (string)idProp.GetValue(old)!;
            if (!stored.TryGetValue(id, out var now)) continue; // 移行対象外（所属なし）は Import 時に報告済み
            if (!orderBroken && (seq >= storedOrder.Count || !string.Equals(storedOrder[seq], id, StringComparison.OrdinalIgnoreCase)))
            {
                orderBroken = true;
                problems.Add($"{table}: 並び順が旧版と異なります（{seq} 行目）");
            }
            seq++;
            foreach (var p in props)
            {
                if (!row.Has(p.Name)) continue; // 旧い版の DB にない列は既定値のまま
                var a = p.GetValue(old);
                var b = p.GetValue(now);
                var same = a is NetTopologySuite.Geometries.Point pa && b is NetTopologySuite.Geometries.Point pb
                    ? pa.X == pb.X && pa.Y == pb.Y
                    : Equals(a, b) || (a is null && b is "") || (a is "" && b is null);
                if (!same) problems.Add($"{table} {id}.{p.Name}: 旧 {a} / 新 {b}");
            }
        }
        if (seq != stored.Count) problems.Add($"{table}: 件数が一致しません（旧 {seq} / 新 {stored.Count}）");
    }

    private static async Task SetCreatedAtAsync(IAppDbContext db, object entity, DateTime utc, CancellationToken ct)
    {
        switch (entity)
        {
            case Customer x: await db.Customers.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case CustomerParseSetting x: await db.CustomerParseSettings.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case MaterialCategory x: await db.MaterialCategories.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case Material x: await db.Materials.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case Project x: await db.Projects.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case Floor x: await db.Floors.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case Budget x: await db.Budgets.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
            case BudgetLine x: await db.BudgetLines.IgnoreQueryFilters().Where(e => e.Id == x.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, utc), ct); break;
        }
    }

    private static async Task<List<T>> DetailsAsync<T>(LegacySource src, string table, ImportReport report, string label, Func<T, bool> keep)
        where T : new()
    {
        var rows = await src.ReadAsync(table);
        var list = new List<T>(rows.Count);
        foreach (var r in rows)
        {
            var x = LegacySource.Materialize<T>(r);
            if (keep(x)) list.Add(x);
        }
        if (list.Count < rows.Count) report.Warnings.Add($"{label}：所属する階・梁のないデータ {rows.Count - list.Count} 件は移行しません。");
        report.Add(label, list.Count);
        return list;
    }

    /// <summary>旧版の顧客解析設定と階の設定から、出力時に使う解析設定（新版の階が持つ ParseOptionsJson）を作る。</summary>
    private static ParseOptions OptionsOf(CustomerParseSetting? s, Floor f) => s is null
        ? new ParseOptions { KPillarType = (KPillarType)f.KPillarType, MinimalStrategy = f.MinimalStrategy, BraceComponent = (LianjieCompentType)f.BraceComponent }
        : new ParseOptions
        {
            BeamColor = s.BeamColor, BeamTextColor = s.BeamTextColor,
            PillarColor = s.PillarColor, PillarPenStyle = s.PillarPenStyle, PillarRecognition = (PillarDrawingMethod)s.PillarRecognition,
            SplitColor = s.SplitColor, SplitPenStyle = s.SplitPenStyle, SplitRecognition = (PillarDrawingMethod)s.SplitRecognition,
            NearSplitMax = s.NearSplitMax, DownPillarColor = s.DownPillarColor, BraceColor = s.BraceColor,
            BraceOrigin = (LianjieParsingMethod)s.BraceOrigin, BraceComponent = (LianjieCompentType)f.BraceComponent,
            KPillarType = (KPillarType)f.KPillarType, MinimalStrategy = f.MinimalStrategy,
            ExcludeDegenerateRectangles = false, // 旧版の解析結果そのもの
        };

    private static Guid GuidOf(string s) => Guid.TryParse(s, out var g) ? g : Guid.CreateVersion7();

    /// <summary>新版の一意制約（顧客名・材料種類名・材料名＋仕様・工事内の階名）に合わせて、重複した名前に (2) などを付ける。</summary>
    private sealed class UniqueNames
    {
        private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

        public string Make(string scope, string name, ImportReport report)
        {
            var candidate = name;
            for (var i = 2; !_used.Add(scope + "\u0001" + candidate); i++) candidate = $"{name} ({i})";
            if (candidate != name) report.Warnings.Add($"名前の重複：「{name}」を「{candidate}」として移行します。");
            return candidate;
        }
    }
}
