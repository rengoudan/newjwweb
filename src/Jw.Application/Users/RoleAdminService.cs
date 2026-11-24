using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Application.Users;

public sealed record RoleListItem(
    Guid Id, string Name, string DisplayName, string? Description, bool IsSystem, int UserCount, IReadOnlyList<string> Permissions);

public sealed class RoleInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "ロールIDを入力してください。")]
    [RegularExpression("^[a-zA-Z][a-zA-Z0-9_]{1,49}$", ErrorMessage = "ロールIDは半角英字で始まる英数字と _ の2～50文字です。")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "表示名を入力してください。")]
    [StringLength(100)]
    public string DisplayName { get; set; } = "";

    [StringLength(400)]
    public string? Description { get; set; }

    public HashSet<string> Permissions { get; set; } = [];
}

/// <summary>角色和权限管理。管理员角色的权限固定为全部，避免把自己锁在外面。</summary>
public sealed class RoleAdminService(OperationScope scope, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<RoleListItem>> ListAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Roles);
        return await scope.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext>();
            var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
            var claims = await db.Db.Set<IdentityRoleClaim<Guid>>().AsNoTracking()
                .Where(c => c.ClaimType == Permissions.ClaimType).ToListAsync(ct);
            var counts = await db.Db.Set<IdentityUserRole<Guid>>().GroupBy(x => x.RoleId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            return (IReadOnlyList<RoleListItem>)roles.Select(r => new RoleListItem(
                r.Id, r.Name!, r.DisplayName, r.Description, r.IsSystem, counts.GetValueOrDefault(r.Id),
                claims.Where(c => c.RoleId == r.Id).Select(c => c.ClaimValue!).ToList())).ToList();
        });
    }

    public async Task SaveAsync(RoleInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Roles);
        var unknown = input.Permissions.Where(p => !Permissions.IsDefined(p)).ToList();
        if (unknown.Count > 0) throw new AppException("未定義の権限が含まれています：" + string.Join(",", unknown));

        await scope.RunAsync(async sp =>
        {
            var roles = sp.GetRequiredService<RoleManager<AppRole>>();
            var audit = sp.GetRequiredService<IAuditLogger>();

            if (input.Id is null)
            {
                if (await roles.RoleExistsAsync(input.Name)) throw new AppException($"ロールID「{input.Name}」は既に使われています。");
                var role = new AppRole { Name = input.Name, DisplayName = input.DisplayName.Trim(), Description = input.Description };
                UserAdminService.Check(await roles.CreateAsync(role));
                foreach (var p in input.Permissions)
                    UserAdminService.Check(await roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, p)));
                await audit.LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.RoleCreate, $"ロール「{role.DisplayName}」を追加")
                { EntityType = nameof(AppRole), EntityId = role.Id.ToString(), Detail = new { role.Name, role.DisplayName, Permissions = input.Permissions.Order() } }, ct);
                return;
            }

            var existing = await roles.FindByIdAsync(input.Id.ToString()!) ?? throw new NotFoundException("ロール");
            var changes = new List<AuditChange>();
            if (existing.DisplayName != input.DisplayName.Trim()) changes.Add(new("DisplayName", existing.DisplayName, input.DisplayName.Trim()));
            if (existing.Description != input.Description) changes.Add(new("Description", existing.Description, input.Description));
            existing.DisplayName = input.DisplayName.Trim();
            existing.Description = input.Description;
            UserAdminService.Check(await roles.UpdateAsync(existing));

            // 管理员角色：权限固定为全部，忽略页面上的勾选
            var target = existing.Name == SystemRoles.Admin ? Permissions.All.ToHashSet() : input.Permissions;
            var current = (await roles.GetClaimsAsync(existing)).Where(c => c.Type == Permissions.ClaimType).ToList();
            var currentSet = current.Select(c => c.Value).ToHashSet();
            var permsChanged = !currentSet.SetEquals(target);
            if (permsChanged)
            {
                foreach (var c in current.Where(c => !target.Contains(c.Value)))
                    UserAdminService.Check(await roles.RemoveClaimAsync(existing, c));
                foreach (var p in target.Where(p => !currentSet.Contains(p)))
                    UserAdminService.Check(await roles.AddClaimAsync(existing, new Claim(Permissions.ClaimType, p)));
                changes.Add(new("Permissions", string.Join(",", currentSet.Order()), string.Join(",", target.Order())));

                // 让该角色下的用户重新登录，以拿到新权限
                var users = sp.GetRequiredService<UserManager<AppUser>>();
                foreach (var u in await users.GetUsersInRoleAsync(existing.Name!))
                    await users.UpdateSecurityStampAsync(u);
            }

            if (changes.Count > 0)
                await audit.LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.RoleUpdate, $"ロール「{existing.DisplayName}」を変更")
                { EntityType = nameof(AppRole), EntityId = existing.Id.ToString(), Detail = changes }, ct);
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Roles);
        await scope.RunAsync(async sp =>
        {
            var roles = sp.GetRequiredService<RoleManager<AppRole>>();
            var role = await roles.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("ロール");
            if (role.IsSystem) throw new AppException("システム標準のロールは削除できません。");
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            if ((await users.GetUsersInRoleAsync(role.Name!)).Count > 0)
                throw new AppException("このロールが割り当てられたユーザーがいるため削除できません。");
            UserAdminService.Check(await roles.DeleteAsync(role));
            await sp.GetRequiredService<IAuditLogger>().LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.RoleDelete,
                $"ロール「{role.DisplayName}」を削除") { EntityType = nameof(AppRole), EntityId = role.Id.ToString() }, ct);
        });
    }
}
