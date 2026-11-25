using System.ComponentModel.DataAnnotations;
using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Jw.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Projects;

public sealed record ProjectListItem(
    Guid Id, string Name, string? SiteAddress, Guid? CustomerId, string? CustomerName,
    int FloorQuantity, int ParsedQuantity, ProjectStatus Status, string? Scale,
    int BeamCount, int KPillarCount, int SinglePillarCount, int BCount, int BGCount,
    bool HasBudget, DateTime CreatedAt, DateTime? ModifiedAt, string? ModifiedByName, string? CreatedByName);

public sealed class ProjectInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "工事名を入力してください。")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "建設現場住所を入力してください。")]
    [StringLength(400)]
    public string? SiteAddress { get; set; }

    [Required(ErrorMessage = "顧客を選択してください。")]
    public Guid? CustomerId { get; set; }

    [Range(1, 200, ErrorMessage = "階数は 1～200 の範囲で入力してください。")]
    public int FloorQuantity { get; set; } = 1;

    public Guid ConcurrencyStamp { get; set; }

    /// <summary>只读：已导入层数，编辑时用于提示层数下限。</summary>
    public int ParsedQuantity { get; set; }
}

public sealed record LookupItem(Guid Id, string Name);

/// <summary>工事管理。图纸导入、解析、导出在第 3、4 阶段加入。</summary>
public sealed class ProjectService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<ProjectListItem>> ListAsync(string? keyword = null, Guid? customerId = null, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Projects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(p => p.Name.Contains(keyword) || (p.SiteAddress != null && p.SiteAddress.Contains(keyword)));
        if (customerId is not null) q = q.Where(p => p.CustomerId == customerId);

        return await q.OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListItem(p.Id, p.Name, p.SiteAddress, p.CustomerId,
                p.Customer != null ? p.Customer.CompanyName : null,
                p.FloorQuantity, p.ParsedQuantity, p.Status, p.Scale,
                p.BeamCount, p.KPillarCount, p.SinglePillarCount, p.BCount, p.BGCount,
                db.Budgets.Any(b => b.ProjectId == p.Id),
                p.CreatedAt, p.ModifiedAt, p.ModifiedByName, p.CreatedByName))
            .ToListAsync(ct);
    }

    public async Task<ProjectListItem> GetAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Projects.AsNoTracking().Where(p => p.Id == id)
                   .Select(p => new ProjectListItem(p.Id, p.Name, p.SiteAddress, p.CustomerId,
                       p.Customer != null ? p.Customer.CompanyName : null,
                       p.FloorQuantity, p.ParsedQuantity, p.Status, p.Scale,
                       p.BeamCount, p.KPillarCount, p.SinglePillarCount, p.BCount, p.BGCount,
                       db.Budgets.Any(b => b.ProjectId == p.Id),
                       p.CreatedAt, p.ModifiedAt, p.ModifiedByName, p.CreatedByName))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("工事");
    }

    public async Task<ProjectInput> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("工事");
        return new ProjectInput
        {
            Id = p.Id, Name = p.Name, SiteAddress = p.SiteAddress, CustomerId = p.CustomerId,
            FloorQuantity = p.FloorQuantity, ParsedQuantity = p.ParsedQuantity, ConcurrencyStamp = p.ConcurrencyStamp,
        };
    }

    /// <summary>编辑工事时选择顧客用。</summary>
    public async Task<IReadOnlyList<LookupItem>> CustomerLookupAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Customers.AsNoTracking().OrderBy(c => c.CompanyName)
            .Select(c => new LookupItem(c.Id, c.CompanyName)).ToListAsync(ct);
    }

    public async Task<Guid> SaveAsync(ProjectInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Edit);
        await using var db = await dbFactory.CreateAsync(ct);

        if (input.CustomerId is { } cid && !await db.Customers.AnyAsync(c => c.Id == cid, ct))
            throw new AppException("選択した顧客は削除されています。選び直してください。");

        Project entity;
        if (input.Id is null)
        {
            entity = new Project();
            db.Projects.Add(entity);
        }
        else
        {
            entity = await db.Projects.FirstOrDefaultAsync(p => p.Id == input.Id, ct) ?? throw new NotFoundException("工事");
            db.Db.Entry(entity).Property(p => p.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
        }

        if (input.FloorQuantity < entity.MinFloorQuantity)
            throw new AppException($"階数は {entity.MinFloorQuantity} 以上にしてください（取り込み済み {entity.ParsedQuantity} 階）。");

        entity.Name = input.Name.Trim();
        entity.SiteAddress = string.IsNullOrWhiteSpace(input.SiteAddress) ? null : input.SiteAddress.Trim();
        entity.CustomerId = input.CustomerId;
        entity.FloorQuantity = input.FloorQuantity;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
        return entity.Id;
    }

    /// <summary>删除工事：连同其予算一起软删除（第 3 阶段起还包括各层数据）。</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Delete);
        await using var db = await dbFactory.CreateAsync(ct);
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("工事");
        var budgets = await db.Budgets.Include(b => b.Lines).Where(b => b.ProjectId == id).ToListAsync(ct);
        foreach (var b in budgets)
        {
            db.BudgetLines.RemoveRange(b.Lines);
            db.Budgets.Remove(b);
        }
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
    }
}
