using System.Linq.Expressions;
using Jw.Application.Abstractions;
using Jw.Domain.Auditing;
using Jw.Domain.Customers;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Jw.Infrastructure.Persistence;

public class JwDbContext(DbContextOptions<JwDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    DbSet<AppUser> IAppDbContext.Users => Users;
    DbSet<AppRole> IAppDbContext.Roles => Roles;
    DbContext IAppDbContext.Db => this;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Identity 表去掉 AspNet 前缀
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.DisplayName).HasMaxLength(100);
        });
        b.Entity<AppRole>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.DisplayName).HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(400);
        });
        b.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        b.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(a => a.Action).HasMaxLength(50);
            e.Property(a => a.UserName).HasMaxLength(256);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.EntityType).HasMaxLength(100);
            e.Property(a => a.EntityId).HasMaxLength(100);
            e.Property(a => a.Summary).HasMaxLength(500);
            e.Property(a => a.CorrelationId).HasMaxLength(64);
            e.HasIndex(a => a.Time);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.UserName);
        });

        b.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.Property(c => c.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(c => c.Address).HasMaxLength(400);
            e.Property(c => c.Contact).HasMaxLength(100);
            e.Property(c => c.Telephone).HasMaxLength(50);
            e.Property(c => c.Note).HasMaxLength(1000);
            e.HasIndex(c => c.CompanyName).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        ApplyConventions(b);
    }

    /// <summary>所有审计实体通用：软删除过滤、并发令牌、操作者名长度。</summary>
    private static void ApplyConventions(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes())
        {
            var clr = et.ClrType;
            if (typeof(ISoftDelete).IsAssignableFrom(clr))
            {
                var p = Expression.Parameter(clr, "e");
                var body = Expression.Not(Expression.Property(p, nameof(ISoftDelete.IsDeleted)));
                b.Entity(clr).HasQueryFilter(Expression.Lambda(body, p));
                b.Entity(clr).Property(nameof(ISoftDelete.DeletedByName)).HasMaxLength(256);
            }
            if (typeof(IHasConcurrencyStamp).IsAssignableFrom(clr))
                b.Entity(clr).Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp)).IsConcurrencyToken();
            if (typeof(IAuditedEntity).IsAssignableFrom(clr))
            {
                b.Entity(clr).Property(nameof(IAuditedEntity.CreatedByName)).HasMaxLength(256);
                b.Entity(clr).Property(nameof(IAuditedEntity.ModifiedByName)).HasMaxLength(256);
            }
        }
    }
}
