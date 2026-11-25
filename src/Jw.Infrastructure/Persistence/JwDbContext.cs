using System.Linq.Expressions;
using Jw.Application.Abstractions;
using Jw.Domain.Auditing;
using Jw.Domain.Budgets;
using Jw.Domain.Customers;
using Jw.Domain.Materials;
using Jw.Domain.Projects;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Jw.Infrastructure.Persistence;

public class JwDbContext(DbContextOptions<JwDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerParseSetting> CustomerParseSettings => Set<CustomerParseSetting>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Jw.Domain.Files.StoredFile> StoredFiles => Set<Jw.Domain.Files.StoredFile>();
    public DbSet<Jw.Domain.Common.SystemSetting> SystemSettings => Set<Jw.Domain.Common.SystemSetting>();
    public DbSet<JwCore.JwBeamData> FloorBeams => Set<JwCore.JwBeamData>();
    public DbSet<JwCore.JwHoleData> FloorHoles => Set<JwCore.JwHoleData>();
    public DbSet<JwCore.JwBeamVerticalData> FloorBeamVerticals => Set<JwCore.JwBeamVerticalData>();
    public DbSet<JwCore.JwPillarData> FloorPillars => Set<JwCore.JwPillarData>();
    public DbSet<JwCore.JwLinkPartData> FloorLinkParts => Set<JwCore.JwLinkPartData>();
    public DbSet<JwCore.JwLianjieData> FloorLianjies => Set<JwCore.JwLianjieData>();
    public DbSet<JwCore.JwDownPillarData> FloorDownPillars => Set<JwCore.JwDownPillarData>();
    public DbSet<JwCore.JwCutting> FloorCuttings => Set<JwCore.JwCutting>();
    public DbSet<JwCore.JwAddedHoleMarkData> FloorAddedHoleMarks => Set<JwCore.JwAddedHoleMarkData>();

    /// <summary>
    /// 解析明细里的几何点（NetTopologySuite Point）统一存为 "X,Y" 文本：
    /// 不依赖数据库的空间类型扩展，SQL Server 和测试用的 SQLite 都能用，往返不损失精度。
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        base.ConfigureConventions(c);
        c.Properties<NetTopologySuite.Geometries.Point>().HaveConversion<PointTextConverter>().HaveMaxLength(80);
    }
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
            e.HasOne(c => c.ParseSetting).WithOne(s => s.Customer)
                .HasForeignKey<CustomerParseSetting>(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CustomerParseSetting>(e =>
        {
            e.ToTable("CustomerParseSettings");
            e.HasIndex(s => s.CustomerId).IsUnique();
        });

        b.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.SiteAddress).HasMaxLength(400);
            e.Property(p => p.Scale).HasMaxLength(50);
            e.HasOne(p => p.Customer).WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.Name);
        });

        b.Entity<MaterialCategory>(e =>
        {
            e.ToTable("MaterialCategories");
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Name).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasMany(c => c.Materials).WithOne(m => m.Category).HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Material>(e =>
        {
            e.ToTable("Materials");
            e.Property(m => m.Name).HasMaxLength(200).IsRequired();
            e.Property(m => m.Spec).HasMaxLength(200);
            e.Property(m => m.GeneralTitle).HasMaxLength(100);
            e.Property(m => m.UnitName).HasMaxLength(20);
            e.Property(m => m.UnitPrice).HasPrecision(18, 2);
            e.Property(m => m.Remark).HasMaxLength(400);
            e.HasIndex(m => new { m.Name, m.Spec }).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(m => m.GeneralTitle);
        });

        b.Entity<Budget>(e =>
        {
            e.ToTable("Budgets");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProjectId).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasMany(x => x.Lines).WithOne(l => l.Budget).HasForeignKey(l => l.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BudgetLine>(e =>
        {
            e.ToTable("BudgetLines");
            e.Property(l => l.ItemName).HasMaxLength(200).IsRequired();
            e.Property(l => l.Spec).HasMaxLength(200);
            e.Property(l => l.UnitName).HasMaxLength(20);
            e.Property(l => l.Quantity).HasPrecision(18, 3);
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
            e.Property(l => l.Amount).HasPrecision(18, 2);
        });

        b.Entity<Floor>(e =>
        {
            e.ToTable("Floors");
            e.Property(f => f.Name).HasMaxLength(100).IsRequired();
            e.Property(f => f.ScaleText).HasMaxLength(50);
            e.HasOne(f => f.Project).WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.SourceFile).WithMany().HasForeignKey(f => f.SourceFileId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => new { f.ProjectId, f.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<Jw.Domain.Files.StoredFile>(e =>
        {
            e.ToTable("StoredFiles");
            e.Property(f => f.FileName).HasMaxLength(260);
            e.Property(f => f.ContentType).HasMaxLength(100);
            e.Property(f => f.Sha256).HasMaxLength(64);
            e.Property(f => f.StorageKey).HasMaxLength(200);
            e.Property(f => f.Purpose).HasMaxLength(30);
            e.Property(f => f.UploadedByName).HasMaxLength(256);
            e.HasIndex(f => f.Sha256);
        });

        b.Entity<Jw.Domain.Common.SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.Property(s => s.Value).HasMaxLength(2000);
            e.HasIndex(s => s.Key).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        MapParseResults(b);
        ApplyConventions(b);
    }

    /// <summary>
    /// 图纸解析明细：沿用旧版数据类（JwCore），表名加 Floor 前缀。
    /// 这些是解析器批量生成的数据，不逐行记审计（导入、修改等操作另行记录业务审计）。
    /// </summary>
    private static void MapParseResults(ModelBuilder b)
    {
        static void Common<T>(ModelBuilder b, string table) where T : class
        {
            b.Entity<T>(e =>
            {
                e.ToTable(table);
                e.HasKey("Id");
                if (typeof(T).GetProperty("JwProjectSubData") is not null) e.Ignore("JwProjectSubData");
                e.Property<int>("Seq"); // 解析時の並び順（ParseRows）
                foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)
                             && (p.Name == "Id" || p.Name.EndsWith("Id", StringComparison.Ordinal))))
                    e.Property(p.Name).HasMaxLength(64);
                if (typeof(T).GetProperty("JwProjectSubDataId") is not null) e.HasIndex("JwProjectSubDataId");
            });
        }

        Common<JwCore.JwBeamData>(b, "FloorBeams");
        Common<JwCore.JwHoleData>(b, "FloorHoles");
        Common<JwCore.JwBeamVerticalData>(b, "FloorBeamVerticals");
        Common<JwCore.JwPillarData>(b, "FloorPillars");
        Common<JwCore.JwLinkPartData>(b, "FloorLinkParts");
        Common<JwCore.JwLianjieData>(b, "FloorLianjies");
        Common<JwCore.JwDownPillarData>(b, "FloorDownPillars");
        Common<JwCore.JwCutting>(b, "FloorCuttings");
        Common<JwCore.JwAddedHoleMarkData>(b, "FloorAddedHoleMarks");

        b.Entity<JwCore.JwBeamData>(e =>
        {
            e.HasMany(x => x.JwHoles).WithOne(h => h.JwBeamData).HasForeignKey(h => h.JwBeamDataId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.JwBeamVerticalDatas).WithOne(v => v.JwBeamData).HasForeignKey(v => v.JwBeamDataId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<JwCore.JwAddedHoleMarkData>().HasIndex(x => x.JwBeamDataId);
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
