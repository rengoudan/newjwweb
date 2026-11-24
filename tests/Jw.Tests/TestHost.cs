using System.Security.Claims;
using Jw.Application;
using Jw.Application.Abstractions;
using Jw.Domain.Authorization;
using Jw.Domain.Identity;
using Jw.Infrastructure;
using Jw.Infrastructure.Persistence;
using Jw.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Tests;

/// <summary>测试宿主：真实的服务注册 + SQLite 内存库（整个测试期间保持连接）。</summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }

    private TestHost(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public static async Task<TestHost> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection(); // 重置密码令牌需要（Web 宿主里自动注册）
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:AdminPassword"] = "Admin12345" })
            .Build());
        services.AddApplication();
        services.AddInfrastructure(o => o.UseSqlite(connection));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<JwDbContext>()
            .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await DataSeeder.SeedAsync(sp); // SQLite 下走 EnsureCreated
        return new TestHost(connection, sp);
    }

    /// <summary>以指定用户（拥有指定权限）开一个作用域，相当于一次请求或一个 Blazor 连接。</summary>
    public AsyncServiceScope ScopeAs(string userName, params string[] permissions) =>
        ScopeAs(Guid.NewGuid(), userName, permissions);

    public AsyncServiceScope ScopeAs(Guid userId, string userName, params string[] permissions)
    {
        var scope = Services.CreateAsyncScope();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
        };
        claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));
        scope.ServiceProvider.GetRequiredService<CurrentUserState>()
            .Set(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), "10.0.0.1");
        return scope;
    }

    /// <summary>以种子数据里的管理员身份开作用域（拥有全部权限）。</summary>
    public async Task<AsyncServiceScope> ScopeAsAdminAsync()
    {
        await using var probe = Services.CreateAsyncScope();
        var admin = await probe.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByNameAsync("admin");
        return ScopeAs(admin!.Id, "admin", Permissions.All.ToArray());
    }

    public async Task<JwDbContext> RawDbAsync()
    {
        // 不经过审计拦截器的上下文，用来检查数据库里的真实状态（含已软删除的数据）
        var options = new DbContextOptionsBuilder<JwDbContext>().UseSqlite(_connection).Options;
        return await Task.FromResult(new JwDbContext(options));
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
