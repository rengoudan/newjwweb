using System.Security.Claims;
using Jw.Application.Users;
using Jw.Domain.Authorization;
using Jw.Domain.Identity;
using Jw.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jw.Infrastructure.Seeding;

/// <summary>
/// 启动时执行：迁移数据库、补齐预置角色和权限、没有任何用户时创建初始管理员。
/// 初始管理员密码取配置 Seed:AdminPassword；没配置时随机生成并写入日志（只出现这一次），首次登录必须修改。
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
        var config = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<JwDbContext>();

        if (config.GetValue("Database:AutoMigrate", true))
        {
            // 迁移脚本是为 SQL Server 生成的；其他库（测试用的 SQLite）直接按模型建表
            if (db.Database.IsSqlServer())
                await db.Database.MigrateAsync(ct);
            else
                await db.Database.EnsureCreatedAsync(ct);
        }

        var roles = sp.GetRequiredService<RoleManager<AppRole>>();
        await EnsureRoleAsync(roles, SystemRoles.Admin, "管理者", "すべての機能とシステム管理");
        await EnsureRoleAsync(roles, SystemRoles.Operator, "作業者", "業務データの登録・解析・出力");
        await EnsureRoleAsync(roles, SystemRoles.Viewer, "閲覧者", "参照のみ");

        // 管理员角色始终拥有全部权限（新增权限点后自动补齐）
        var admin = (await roles.FindByNameAsync(SystemRoles.Admin))!;
        var have = (await roles.GetClaimsAsync(admin)).Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).ToHashSet();
        foreach (var p in Permissions.All.Where(p => !have.Contains(p)))
            await roles.AddClaimAsync(admin, new Claim(Permissions.ClaimType, p));

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        if (!await users.Users.AnyAsync(ct))
        {
            var userName = config["Seed:AdminUserName"] is { Length: > 0 } n ? n : "admin";
            var configured = config["Seed:AdminPassword"];
            var password = string.IsNullOrEmpty(configured) ? PasswordGenerator.Generate(16) : configured;
            var user = new AppUser { UserName = userName, DisplayName = "管理者", MustChangePassword = true };
            var r = await users.CreateAsync(user, password);
            if (!r.Succeeded) throw new InvalidOperationException("初始管理员创建失败：" + string.Join(" ", r.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, SystemRoles.Admin);
            if (string.IsNullOrEmpty(configured))
                log.LogWarning("已创建初始管理员 {UserName}，临时密码：{Password}（首次登录后必须修改）", userName, password);
            else
                log.LogInformation("已创建初始管理员 {UserName}（密码取自配置 Seed:AdminPassword，首次登录后必须修改）", userName);
        }
    }

    private static async Task EnsureRoleAsync(RoleManager<AppRole> roles, string name, string displayName, string description)
    {
        if (await roles.FindByNameAsync(name) is not null) return;
        var role = new AppRole { Name = name, DisplayName = displayName, Description = description, IsSystem = true };
        await roles.CreateAsync(role);
        foreach (var p in Permissions.DefaultsFor(name))
            await roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, p));
    }
}
