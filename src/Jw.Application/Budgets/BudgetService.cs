using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Budgets;
using Jw.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Budgets;

public sealed record BudgetListItem(
    Guid Id, Guid ProjectId, string ProjectName, string? CustomerName, decimal Amount, int LineCount,
    DateTime CreatedAt, DateTime? ModifiedAt, string? ModifiedByName, string? CreatedByName);

public sealed record BudgetLineItem(
    Guid Id, Guid? MaterialId, string ItemName, string? Spec, string UnitName, decimal Quantity, decimal UnitPrice,
    decimal Amount, MaterialKind Kind, BudgetLineSource Source, string? CategoryName, Guid ConcurrencyStamp);

public sealed record BudgetDetail(
    Guid Id, Guid ProjectId, string ProjectName, string? CustomerName, string? SiteAddress, DateTime CreatedAt,
    IReadOnlyList<BudgetLineItem> Lines, decimal TaxRate)
{
    public decimal Subtotal => Lines.Sum(l => l.Amount);
    public decimal Tax => Subtotal * TaxRate;
    public decimal Total => Subtotal + Tax;
}

public sealed class BudgetLineInput
{
    public Guid BudgetId { get; set; }
    public Guid? LineId { get; set; }
    public Guid? MaterialId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>为 null 时用材料的当前单价。</summary>
    public decimal? UnitPrice { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}

/// <summary>见積書文件。</summary>
public sealed record ExportedFile(string FileName, string ContentType, byte[] Content);

/// <summary>见積書导出（实现在 Infrastructure，使用 Excel 模板）。</summary>
public interface IQuotationExporter
{
    byte[] Export(BudgetDetail budget);
}

/// <summary>
/// 予算。每个工事一份；明细从材料选择并按数量计算金额，合计、消费税、总计实时计算。
/// 按解析结果自动生成明细（旧版“予算の作成”）在第 3 阶段加入。
/// </summary>
public sealed class BudgetService(
    IAppDbContextFactory dbFactory, ICurrentUser currentUser, IQuotationExporter exporter, IAuditLogger audit)
{
    /// <summary>消费税率（旧版固定 10%）。</summary>
    public const decimal TaxRate = 0.10m;

    public async Task<IReadOnlyList<BudgetListItem>> ListAsync(string? keyword = null, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Budgets.AsNoTracking().Where(b => !b.Project.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(b => b.Project.Name.Contains(keyword));
        return await q.OrderByDescending(b => b.ModifiedAt ?? b.CreatedAt)
            .Select(b => new BudgetListItem(b.Id, b.ProjectId, b.Project.Name,
                b.Project.Customer != null ? b.Project.Customer.CompanyName : null,
                b.Amount, b.Lines.Count(l => !l.IsDeleted), b.CreatedAt, b.ModifiedAt, b.ModifiedByName, b.CreatedByName))
            .ToListAsync(ct);
    }

    /// <summary>还没有予算的工事（新建予算时选择）。</summary>
    public async Task<IReadOnlyList<Projects.LookupItem>> ProjectsWithoutBudgetAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Projects.AsNoTracking()
            .Where(p => !db.Budgets.Any(b => b.ProjectId == p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new Projects.LookupItem(p.Id, p.Name)).ToListAsync(ct);
    }

    public async Task<BudgetDetail> GetAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.View);
        await using var db = await dbFactory.CreateAsync(ct);
        return await LoadDetailAsync(db, id, ct);
    }

    public async Task<Guid?> FindByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.View);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Budgets.Where(b => b.ProjectId == projectId).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>为工事新建空的予算（每个工事只能有一份），工事状态改为“予算作成済み”。</summary>
    public async Task<Guid> CreateAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct) ?? throw new NotFoundException("工事");
        if (await db.Budgets.AnyAsync(b => b.ProjectId == projectId, ct))
            throw new AppException("この工事の予算は既に存在します。");
        var budget = new Budget { ProjectId = project.Id, Project = project };
        db.Budgets.Add(budget);
        project.Status = ProjectStatus.Budgeted;
        await db.SaveChangesAsync(ct);
        return budget.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var budget = await db.Budgets.Include(b => b.Lines).Include(b => b.Project)
                         .FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("予算");
        db.BudgetLines.RemoveRange(budget.Lines);
        db.Budgets.Remove(budget);
        if (budget.Project.Status == ProjectStatus.Budgeted)
            budget.Project.Status = budget.Project.ParsedQuantity > 0 ? ProjectStatus.Parsed : ProjectStatus.Created;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 図面の解析結果から予算明細を自動作成する（旧版 JwBudgePage.CreateBudge と同じ規則）：
    /// 金物は種別（B/BG）、柱は記号（TaggTitle）を材料の共通識別子と照合して個数を、
    /// 梁は取り込み時に選んだ梁材料ごとに長さの合計（m）を数量にする。
    /// 再作成すると前回の自動明細を置き換え、手入力の明細（カスタム予算）は残す。
    /// 旧版は材料が見つからないと途中で止まったが、新版は「識別失敗」明細（単価 0）として残し、材料を登録して再作成できるようにした。
    /// </summary>
    public async Task<Guid> GenerateAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct) ?? throw new NotFoundException("工事");
        var floorKeys = (await db.Floors.Where(f => f.ProjectId == projectId).Select(f => f.Id).ToListAsync(ct)).Select(id => id.ToString()).ToList();
        if (floorKeys.Count == 0) throw new AppException("図面がまだ解析されていません。先に図面を取り込んでください。");

        var budget = await db.Budgets.Include(b => b.Lines).FirstOrDefaultAsync(b => b.ProjectId == projectId, ct);
        if (budget is null)
        {
            budget = new Budget { ProjectId = project.Id, Project = project };
            db.Budgets.Add(budget);
        }
        foreach (var old in budget.Lines.Where(l => l.Source != BudgetLineSource.Custom).ToList())
        {
            db.BudgetLines.Remove(old);
            old.IsDeleted = true;
        }

        var materials = await db.Materials.AsNoTracking().ToListAsync(ct);
        var byTitle = materials.Where(m => !string.IsNullOrEmpty(m.GeneralTitle)).GroupBy(m => m.GeneralTitle!).ToDictionary(g => g.Key, g => g.First());
        var byId = materials.ToDictionary(m => m.Id.ToString(), m => m);

        void Add(string key, decimal quantity, Domain.Materials.Material? m, MaterialKind kind, string unitIfMissing)
        {
            var line = m is null
                ? new BudgetLine { ItemName = key, UnitName = unitIfMissing, UnitPrice = 0, Kind = kind, Source = BudgetLineSource.AutoFailed }
                : new BudgetLine { MaterialId = m.Id, ItemName = m.Name, Spec = m.Spec, UnitName = m.UnitName, UnitPrice = m.UnitPrice, Kind = m.Kind, Source = BudgetLineSource.Auto };
            line.BudgetId = budget.Id;
            line.Budget = budget;
            line.Quantity = quantity;
            line.Recalculate();
            budget.Lines.Add(line);
            db.BudgetLines.Add(line);
        }

        var links = await db.FloorLinkParts.AsNoTracking().Where(l => floorKeys.Contains(l.JwProjectSubDataId)).ToListAsync(ct);
        foreach (var g in links.GroupBy(l => l.GouJianType.ToString()))
            Add(g.Key, g.Count(), byTitle.GetValueOrDefault(g.Key), MaterialKind.Hardware, "個");

        var beams = await db.FloorBeams.AsNoTracking().Where(b => floorKeys.Contains(b.JwProjectSubDataId)).ToListAsync(ct);
        foreach (var g in beams.GroupBy(b => b.BeamXHId ?? ""))
            Add(string.IsNullOrEmpty(g.Key) ? "梁（材料未指定）" : g.Key, Convert.ToDecimal(g.Sum(b => b.Length) / 1000),
                byId.GetValueOrDefault(g.Key), MaterialKind.Beam, "m");

        var pillars = await db.FloorPillars.AsNoTracking().Where(p => floorKeys.Contains(p.JwProjectSubDataId) && p.TaggTitle != null && p.TaggTitle != "").ToListAsync(ct);
        foreach (var g in pillars.GroupBy(p => p.TaggTitle))
            Add(g.Key, g.Count(), byTitle.GetValueOrDefault(g.Key), MaterialKind.Pillar, "本");

        budget.Recalculate();
        project.Status = ProjectStatus.Budgeted;
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "BudgetGenerate", $"予算を自動作成：{project.Name}")
        {
            EntityType = nameof(Budget), EntityId = budget.Id.ToString(),
            Detail = new
            {
                Lines = budget.Lines.Count(l => !l.IsDeleted && l.Source != BudgetLineSource.Custom),
                Failed = budget.Lines.Where(l => !l.IsDeleted && l.Source == BudgetLineSource.AutoFailed).Select(l => l.ItemName).ToList(),
                budget.Amount,
            },
        }, ct);
        return budget.Id;
    }

    /// <summary>新增或修改明细。新增时从材料复制名称、规格、单位、单价。</summary>
    public async Task SaveLineAsync(BudgetLineInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        if (input.Quantity <= 0) throw new AppException("数量は 0 より大きい値を入力してください。");
        if (input.UnitPrice is < 0) throw new AppException("単価は 0 以上を入力してください。");

        await using var db = await dbFactory.CreateAsync(ct);
        var budget = await db.Budgets.Include(b => b.Lines).Include(b => b.Project)
                         .FirstOrDefaultAsync(b => b.Id == input.BudgetId, ct) ?? throw new NotFoundException("予算");

        BudgetLine line;
        if (input.LineId is null)
        {
            if (input.MaterialId is null) throw new AppException("材料を選択してください。");
            var m = await db.Materials.FirstOrDefaultAsync(x => x.Id == input.MaterialId, ct) ?? throw new NotFoundException("材料");
            line = new BudgetLine
            {
                BudgetId = budget.Id, Budget = budget, MaterialId = m.Id, ItemName = m.Name, Spec = m.Spec,
                UnitName = m.UnitName, UnitPrice = input.UnitPrice ?? m.UnitPrice, Kind = m.Kind, Source = BudgetLineSource.Custom,
            };
            budget.Lines.Add(line);
            db.BudgetLines.Add(line);
        }
        else
        {
            line = budget.Lines.FirstOrDefault(l => l.Id == input.LineId) ?? throw new NotFoundException("予算明細");
            db.Db.Entry(line).Property(l => l.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
            if (input.UnitPrice is { } price) line.UnitPrice = price;
        }

        line.Quantity = input.Quantity;
        line.Recalculate();
        budget.Recalculate();

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
    }

    public async Task DeleteLineAsync(Guid budgetId, Guid lineId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var budget = await db.Budgets.Include(b => b.Lines).Include(b => b.Project)
                         .FirstOrDefaultAsync(b => b.Id == budgetId, ct) ?? throw new NotFoundException("予算");
        var line = budget.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new NotFoundException("予算明細");
        db.BudgetLines.Remove(line); // 拦截器转为软删除
        line.IsDeleted = true;       // 让合计立即排除这一行
        budget.Recalculate();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>导出见積書（Excel），并记录业务操作审计。</summary>
    public async Task<ExportedFile> ExportQuotationAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Budget.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var detail = await LoadDetailAsync(db, id, ct);
        var bytes = exporter.Export(detail);
        var fileName = $"{detail.ProjectName}見積書.xlsx";

        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "ExportQuotation", $"見積書を出力：{detail.ProjectName}")
        {
            EntityType = nameof(Budget), EntityId = id.ToString(),
            Detail = new { FileName = fileName, Lines = detail.Lines.Count, detail.Subtotal, detail.Total },
        }, ct);
        return new ExportedFile(SafeFileName(fileName), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes);
    }

    private static async Task<BudgetDetail> LoadDetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var b = await db.Budgets.AsNoTracking().Where(x => x.Id == id)
                    .Select(x => new
                    {
                        x.Id, x.ProjectId, ProjectName = x.Project.Name, x.Project.SiteAddress, x.CreatedAt,
                        CustomerName = x.Project.Customer != null ? x.Project.Customer.CompanyName : null,
                    })
                    .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("予算");

        var lines = await (from l in db.BudgetLines.AsNoTracking()
                           // IgnoreQueryFilters 会作用于整个查询（为了显示已删除材料的种类名），所以要显式排除已删除的明细
                           where l.BudgetId == id && !l.IsDeleted
                           join m in db.Materials.IgnoreQueryFilters() on l.MaterialId equals m.Id into mj
                           from m in mj.DefaultIfEmpty()
                           orderby l.Kind, l.CreatedAt
                           select new BudgetLineItem(l.Id, l.MaterialId, l.ItemName, l.Spec, l.UnitName, l.Quantity, l.UnitPrice,
                               l.Amount, l.Kind, l.Source, m != null ? m.Category.Name : null, l.ConcurrencyStamp))
                          .ToListAsync(ct);

        return new BudgetDetail(b.Id, b.ProjectId, b.ProjectName, b.CustomerName, b.SiteAddress, b.CreatedAt, lines, TaxRate);
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
