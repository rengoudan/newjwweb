using Jw.Domain.Auditing;
using Jw.Domain.Customers;
using Jw.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Abstractions;

/// <summary>业务服务看到的数据库上下文。实现在 Infrastructure。</summary>
public interface IAppDbContext : IAsyncDisposable, IDisposable
{
    DbSet<Customer> Customers { get; }
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
