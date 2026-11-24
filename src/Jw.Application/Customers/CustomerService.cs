using System.ComponentModel.DataAnnotations;
using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using Jw.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Customers;

public sealed record CustomerListItem(
    Guid Id, string CompanyName, string? Address, string? Contact, string? Telephone,
    DateTime CreatedAt, string? CreatedByName, DateTime? ModifiedAt, string? ModifiedByName);

public sealed class CustomerInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "会社名を入力してください。")]
    [StringLength(200)]
    public string CompanyName { get; set; } = "";

    [StringLength(400)] public string? Address { get; set; }
    [StringLength(100)] public string? Contact { get; set; }
    [StringLength(50)] public string? Telephone { get; set; }
    [StringLength(1000)] public string? Note { get; set; }

    /// <summary>编辑开始时读到的版本，保存时用于并发冲突检测。</summary>
    public Guid ConcurrencyStamp { get; set; }
}

/// <summary>顧客管理。审计字段和变更记录由数据库拦截器自动处理。</summary>
public sealed class CustomerService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<CustomerListItem>> ListAsync(string? keyword = null, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(c => c.CompanyName.Contains(keyword) || (c.Contact != null && c.Contact.Contains(keyword))
                             || (c.Telephone != null && c.Telephone.Contains(keyword)));
        return await q.OrderBy(c => c.CompanyName)
            .Select(c => new CustomerListItem(c.Id, c.CompanyName, c.Address, c.Contact, c.Telephone,
                c.CreatedAt, c.CreatedByName, c.ModifiedAt, c.ModifiedByName))
            .ToListAsync(ct);
    }

    public async Task<CustomerInput> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("顧客");
        return new CustomerInput
        {
            Id = c.Id, CompanyName = c.CompanyName, Address = c.Address, Contact = c.Contact,
            Telephone = c.Telephone, Note = c.Note, ConcurrencyStamp = c.ConcurrencyStamp,
        };
    }

    public async Task<Guid> SaveAsync(CustomerInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.Edit);
        var name = input.CompanyName.Trim();
        await using var db = await dbFactory.CreateAsync(ct);

        if (await db.Customers.AnyAsync(c => c.CompanyName == name && c.Id != input.Id, ct))
            throw new AppException($"「{name}」は既に登録されています。");

        Customer entity;
        if (input.Id is null)
        {
            entity = new Customer();
            db.Customers.Add(entity);
        }
        else
        {
            entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == input.Id, ct)
                     ?? throw new NotFoundException("顧客");
            // 用编辑开始时的版本做比较：期间别人改过的话，保存时会抛并发异常
            db.Db.Entry(entity).Property(e => e.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
        }

        entity.CompanyName = name;
        entity.Address = Clean(input.Address);
        entity.Contact = Clean(input.Contact);
        entity.Telephone = Clean(input.Telephone);
        entity.Note = Clean(input.Note);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.Delete);
        await using var db = await dbFactory.CreateAsync(ct);
        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException("顧客");
        db.Customers.Remove(entity); // 拦截器会转为软删除并记录审计
        await db.SaveChangesAsync(ct);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
