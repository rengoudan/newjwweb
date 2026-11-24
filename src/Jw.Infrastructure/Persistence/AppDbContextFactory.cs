using Jw.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jw.Infrastructure.Persistence;

internal sealed class AppDbContextFactory(IDbContextFactory<JwDbContext> inner) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}

/// <summary>dotnet ef 生成迁移时使用。连接串只用于设计时，不会连接数据库。</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JwDbContext>
{
    public JwDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JwDbContext>()
            .UseSqlServer("Server=.;Database=NewJwWeb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
