using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Infrastructure.Auditing;
using Jw.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Infrastructure;

public static class DependencyInjection
{
    /// <param name="configureDb">配置数据库提供程序，例如 o => o.UseSqlServer(cs)；测试时可换成 SQLite。</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();

        // 工厂和上下文都是 Scoped：拦截器要拿到当前作用域（当前操作者）
        services.AddDbContextFactory<JwDbContext>((sp, o) =>
        {
            configureDb(o);
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        }, ServiceLifetime.Scoped);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<JwDbContext>>().CreateDbContext());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<JwDbContext>());
        services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        return services;
    }
}
