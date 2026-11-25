using System.ComponentModel.DataAnnotations;
using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Jw.Domain.Materials;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Materials;

public sealed record CategoryItem(Guid Id, string Name, MaterialKind Kind, Guid? DefaultMaterialId, int MaterialCount, Guid ConcurrencyStamp);

public sealed record MaterialItem(
    Guid Id, Guid CategoryId, string CategoryName, MaterialKind Kind, string Name, string? Spec, string? GeneralTitle,
    string UnitName, decimal UnitPrice, double NumParameter, string? Remark, DateTime? ModifiedAt, DateTime CreatedAt, string? ModifiedByName, string? CreatedByName)
{
    public string Description => string.IsNullOrEmpty(Spec) ? Name : $"{Name} {Spec}";
}

public sealed class CategoryInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "種類名を入力してください。")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    public MaterialKind Kind { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}

public sealed class MaterialInput
{
    public Guid? Id { get; set; }
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "材料名称を入力してください。")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(200)] public string? Spec { get; set; }
    [StringLength(100)] public string? GeneralTitle { get; set; }

    [Required(ErrorMessage = "単位を入力してください。")]
    [StringLength(20)]
    public string UnitName { get; set; } = "";

    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "単価は 0 より大きい値を入力してください。")]
    public decimal UnitPrice { get; set; }

    public double NumParameter { get; set; }
    [StringLength(400)] public string? Remark { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}

/// <summary>基礎データ：材料种类与材料。</summary>
public sealed class MaterialService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct = default)
    {
        RequireView();
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.MaterialCategories.AsNoTracking()
            .OrderBy(c => c.Kind).ThenBy(c => c.Name)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Kind, c.DefaultMaterialId,
                db.Materials.Count(m => m.CategoryId == c.Id), c.ConcurrencyStamp))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialItem>> MaterialsAsync(Guid? categoryId = null, string? keyword = null, CancellationToken ct = default)
    {
        RequireView();
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Materials.AsNoTracking();
        if (categoryId is not null) q = q.Where(m => m.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(m => m.Name.Contains(keyword) || (m.Spec != null && m.Spec.Contains(keyword))
                             || (m.GeneralTitle != null && m.GeneralTitle.Contains(keyword)));
        return await q.OrderBy(m => m.Kind).ThenBy(m => m.Name).ThenBy(m => m.Spec)
            .Select(m => new MaterialItem(m.Id, m.CategoryId, m.Category.Name, m.Kind, m.Name, m.Spec, m.GeneralTitle,
                m.UnitName, m.UnitPrice, m.NumParameter, m.Remark, m.ModifiedAt, m.CreatedAt, m.ModifiedByName, m.CreatedByName))
            .ToListAsync(ct);
    }

    public async Task<Guid> SaveCategoryAsync(CategoryInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.BaseData.Edit);
        var name = input.Name.Trim();
        await using var db = await dbFactory.CreateAsync(ct);
        if (await db.MaterialCategories.AnyAsync(c => c.Name == name && c.Id != input.Id, ct))
            throw new AppException($"種類「{name}」は既に登録されています。");

        MaterialCategory entity;
        if (input.Id is null)
        {
            entity = new MaterialCategory { Kind = input.Kind };
            db.MaterialCategories.Add(entity);
        }
        else
        {
            entity = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == input.Id, ct) ?? throw new NotFoundException("種類");
            db.Db.Entry(entity).Property(c => c.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
            if (entity.Kind != input.Kind && await db.Materials.AnyAsync(m => m.CategoryId == entity.Id, ct))
                throw new AppException("材料が登録されている種類のカテゴリーは変更できません。");
            entity.Kind = input.Kind;
        }
        entity.Name = name;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
        return entity.Id;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.BaseData.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var entity = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("種類");
        if (await db.Materials.AnyAsync(m => m.CategoryId == id, ct))
            throw new AppException("材料が登録されているため削除できません。先に材料を削除してください。");
        db.MaterialCategories.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>设置种类的默认材料（传 null 取消）。</summary>
    public async Task SetDefaultAsync(Guid categoryId, Guid? materialId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.BaseData.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var cat = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct) ?? throw new NotFoundException("種類");
        if (materialId is { } mid && !await db.Materials.AnyAsync(m => m.Id == mid && m.CategoryId == categoryId, ct))
            throw new NotFoundException("材料");
        cat.DefaultMaterialId = materialId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<MaterialInput> GetMaterialForEditAsync(Guid id, CancellationToken ct = default)
    {
        RequireView();
        await using var db = await dbFactory.CreateAsync(ct);
        var m = await db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("材料");
        return new MaterialInput
        {
            Id = m.Id, CategoryId = m.CategoryId, Name = m.Name, Spec = m.Spec, GeneralTitle = m.GeneralTitle,
            UnitName = m.UnitName, UnitPrice = m.UnitPrice, NumParameter = m.NumParameter, Remark = m.Remark,
            ConcurrencyStamp = m.ConcurrencyStamp,
        };
    }

    public async Task<Guid> SaveMaterialAsync(MaterialInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.BaseData.Edit);
        var name = input.Name.Trim();
        var spec = Clean(input.Spec);
        await using var db = await dbFactory.CreateAsync(ct);

        var cat = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == input.CategoryId, ct) ?? throw new NotFoundException("種類");
        if (await db.Materials.AnyAsync(m => m.Name == name && m.Spec == spec && m.Id != input.Id, ct))
            throw new AppException("同じ材料名称・仕様が既に登録されています。");

        Material entity;
        if (input.Id is null)
        {
            entity = new Material();
            db.Materials.Add(entity);
        }
        else
        {
            entity = await db.Materials.FirstOrDefaultAsync(m => m.Id == input.Id, ct) ?? throw new NotFoundException("材料");
            db.Db.Entry(entity).Property(m => m.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
        }

        entity.CategoryId = cat.Id;
        entity.Kind = cat.Kind;
        entity.Name = name;
        entity.Spec = spec;
        entity.GeneralTitle = Clean(input.GeneralTitle);
        entity.UnitName = input.UnitName.Trim();
        entity.UnitPrice = input.UnitPrice;
        entity.NumParameter = input.NumParameter;
        entity.Remark = Clean(input.Remark);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
        return entity.Id;
    }

    /// <summary>删除材料。已在予算里用过的明细保留快照，不受影响。</summary>
    public async Task DeleteMaterialAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.BaseData.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var m = await db.Materials.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("材料");
        var defaultOf = await db.MaterialCategories.Where(c => c.DefaultMaterialId == id).ToListAsync(ct);
        foreach (var c in defaultOf) c.DefaultMaterialId = null;
        db.Materials.Remove(m);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>予算明细里选材料用：予算编辑权限也可以读取。</summary>
    private void RequireView()
    {
        if (!currentUser.HasPermission(Permissions.BaseData.View) && !currentUser.HasPermission(Permissions.Budget.Edit))
            throw new ForbiddenException(Permissions.BaseData.View);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
