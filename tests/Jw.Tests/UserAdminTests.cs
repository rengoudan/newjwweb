using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Application.Users;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Tests;

public class UserAdminTests
{
    [Fact]
    public async Task Seed_creates_system_roles_and_admin_with_all_permissions()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = await host.ScopeAsAdminAsync();
        var roles = await scope.ServiceProvider.GetRequiredService<RoleAdminService>().ListAsync();

        Assert.Equal(["Admin", "Operator", "Viewer"], roles.Select(r => r.Name).Order());
        Assert.Equal(Permissions.All.Order(), roles.Single(r => r.Name == "Admin").Permissions.Order());
        Assert.DoesNotContain(roles.Single(r => r.Name == "Operator").Permissions, p => p.StartsWith("Admin."));
        Assert.All(roles.Single(r => r.Name == "Viewer").Permissions, p => Assert.EndsWith(".View", p));

        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByNameAsync("admin");
        Assert.True(admin!.MustChangePassword);
    }

    [Fact]
    public async Task Create_user_returns_temp_password_and_is_audited()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = await host.ScopeAsAdminAsync();
        var svc = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var password = await svc.CreateAsync(new UserInput { UserName = "sato", DisplayName = "佐藤", Roles = ["Operator"] });

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByNameAsync("sato");
        Assert.NotNull(user);
        Assert.True(user.MustChangePassword);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.Equal(["Operator"], await users.GetRolesAsync(user));

        var log = await scope.ServiceProvider.GetRequiredService<AuditQueryService>()
            .SearchAsync(new AuditQuery { Category = AuditCategory.Security });
        var entry = Assert.Single(log.Items, i => i.Action == AuditActions.UserCreate);
        Assert.Equal("admin", entry.UserName);
    }

    [Fact]
    public async Task Cannot_deactivate_self_or_remove_last_admin()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = await host.ScopeAsAdminAsync();
        var svc = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var me = (await svc.ListAsync()).Single(u => u.UserName == "admin");

        var deactivateSelf = new UserInput { Id = me.Id, UserName = "admin", DisplayName = "管理者", IsActive = false, Roles = ["Admin"] };
        await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(deactivateSelf));

        // 另一个管理员要把唯一的管理员（admin）降级：也必须拒绝
        await using var other = host.ScopeAs(Guid.NewGuid(), "someone", Permissions.All.ToArray());
        var otherSvc = other.ServiceProvider.GetRequiredService<UserAdminService>();
        var demote = new UserInput { Id = me.Id, UserName = "admin", DisplayName = "管理者", IsActive = true, Roles = ["Operator"] };
        var ex = await Assert.ThrowsAsync<AppException>(() => otherSvc.UpdateAsync(demote));
        Assert.Contains("管理者", ex.Message);
    }

    [Fact]
    public async Task Role_permission_change_updates_member_security_stamp()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = await host.ScopeAsAdminAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        await users.CreateAsync(new UserInput { UserName = "ito", DisplayName = "伊藤", Roles = ["Viewer"] });

        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var before = (await um.FindByNameAsync("ito"))!.SecurityStamp;

        var roles = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var viewer = (await roles.ListAsync()).Single(r => r.Name == "Viewer");
        await roles.SaveAsync(new RoleInput
        {
            Id = viewer.Id, Name = viewer.Name, DisplayName = viewer.DisplayName,
            Permissions = [.. viewer.Permissions, Permissions.Customers.Edit],
        });

        await using var fresh = host.Services.CreateAsyncScope();
        var after = (await fresh.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByNameAsync("ito"))!.SecurityStamp;
        Assert.NotEqual(before, after);
        Assert.Contains(Permissions.Customers.Edit, (await roles.ListAsync()).Single(r => r.Name == "Viewer").Permissions);
    }

    [Fact]
    public async Task Admin_role_permissions_cannot_be_reduced()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = await host.ScopeAsAdminAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var admin = (await roles.ListAsync()).Single(r => r.Name == "Admin");

        await roles.SaveAsync(new RoleInput { Id = admin.Id, Name = "Admin", DisplayName = "管理者", Permissions = [] });

        Assert.Equal(Permissions.All.Order(), (await roles.ListAsync()).Single(r => r.Name == "Admin").Permissions.Order());
    }

    [Fact]
    public async Task Non_admin_cannot_manage_users()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("op", Permissions.Customers.View);
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<UserAdminService>().ListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<AuditQueryService>().SearchAsync(new AuditQuery()));
    }
}
