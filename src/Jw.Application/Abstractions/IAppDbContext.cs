using Jw.Domain.Auditing;
using Jw.Domain.Budgets;
using Jw.Domain.Common;
using Jw.Domain.Customers;
using Jw.Domain.Materials;
using Jw.Domain.Projects;
using Jw.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Abstractions;

/// <summary>业务服务看到的数据库上下文。实现在 Infrastructure。</summary>
public interface IAppDbContext : IAsyncDisposable, IDisposable
{
    DbSet<Customer> Customers { get; }
    DbSet<CustomerParseSetting> CustomerParseSettings { get; }
    DbSet<Project> Projects { get; }
    DbSet<MaterialCategory> MaterialCategories { get; }
    DbSet<Material> Materials { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetLine> BudgetLines { get; }
    DbSet<Floor> Floors { get; }
    DbSet<Jw.Domain.Files.StoredFile> StoredFiles { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    // 図面解析の明細（旧版のデータ構造をそのまま保存。JwProjectSubDataId = Floor.Id）
    DbSet<JwCore.JwBeamData> FloorBeams { get; }
    DbSet<JwCore.JwHoleData> FloorHoles { get; }
    DbSet<JwCore.JwBeamVerticalData> FloorBeamVerticals { get; }
    DbSet<JwCore.JwPillarData> FloorPillars { get; }
    DbSet<JwCore.JwLinkPartData> FloorLinkParts { get; }
    DbSet<JwCore.JwLianjieData> FloorLianjies { get; }
    DbSet<JwCore.JwDownPillarData> FloorDownPillars { get; }
    DbSet<JwCore.JwCutting> FloorCuttings { get; }
    DbSet<JwCore.JwAddedHoleMarkData> FloorAddedHoleMarks { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }

    DbContext Db { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 每次操作新建一个短生命周期的上下文。
/// Blazor 交互连接会持续很久，不能共用一个上下文，否则会读到旧数据、互相干扰。
/// </summary>
public interface IAppDbContextFactory
{
    Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default);
}
