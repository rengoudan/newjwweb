using System.ComponentModel.DataAnnotations;
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

public sealed record UserListItem(
    Guid Id, string UserName, string DisplayName, string? Email, bool IsActive, bool IsLockedOut,
    bool MustChangePassword, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record RoleOption(string Name, string DisplayName);

public sealed class UserInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "ユーザーIDを入力してください。")]
    [RegularExpression("^[a-zA-Z0-9._-]{3,50}$", ErrorMessage = "ユーザーIDは半角英数字と . _ - の3～50文字です。")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "氏名を入力してください。")]
    [StringLength(100)]
    public string DisplayName { get; set; } = "";

    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public HashSet<string> Roles { get; set; } = [];
}

/// <summary>用户管理。用户只停用不删除；所有变更写入安全类审计。</summary>
public sealed class UserAdminService(OperationScope scope, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<UserListItem>> ListAsync(string? keyword = null, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        return await scope.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext>();
            var q = db.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(u => u.UserName!.Contains(keyword) || u.DisplayName.Contains(keyword)
                                 || (u.Email != null && u.Email.Contains(keyword)));
            var users = await q.OrderBy(u => u.UserName).ToListAsync(ct);

            var userRoles = await (from ur in db.Db.Set<IdentityUserRole<Guid>>()
                                   join r in db.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, r.Name }).ToListAsync(ct);
            var map = userRoles.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name!).ToList());
            var now = DateTimeOffset.UtcNow;
            return (IReadOnlyList<UserListItem>)users.Select(u => new UserListItem(
                u.Id, u.UserName!, u.DisplayName, u.Email, u.IsActive, u.LockoutEnd > now, u.MustChangePassword,
                map.GetValueOrDefault(u.Id, []), u.CreatedAt, u.LastLoginAt)).ToList();
        });
    }

    public async Task<IReadOnlyList<RoleOption>> RoleOptionsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        return await scope.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext>();
            return (IReadOnlyList<RoleOption>)await db.Roles.AsNoTracking().OrderBy(r => r.Name)
                .Select(r => new RoleOption(r.Name!, r.DisplayName)).ToListAsync(ct);
        });
    }

    /// <summary>新建用户，返回临时密码（只显示这一次，首次登录必须修改）。</summary>
    public async Task<string> CreateAsync(UserInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        return await scope.RunAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            if (await users.FindByNameAsync(input.UserName) is not null)
                throw new AppException($"ユーザーID「{input.UserName}」は既に使われています。");

            var user = new AppUser
            {
                UserName = input.UserName.Trim(),
                DisplayName = input.DisplayName.Trim(),
                Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
                IsActive = input.IsActive,
                MustChangePassword = true,
            };
            var password = PasswordGenerator.Generate();
            Check(await users.CreateAsync(user, password));
            if (input.Roles.Count > 0) Check(await users.AddToRolesAsync(user, input.Roles));

            await sp.GetRequiredService<IAuditLogger>().LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.UserCreate,
                $"ユーザー「{user.UserName}（{user.DisplayName}）」を追加")
            {
                EntityType = nameof(AppUser), EntityId = user.Id.ToString(),
                Detail = new { user.UserName, user.DisplayName, user.Email, user.IsActive, Roles = input.Roles },
            }, ct);
            return password;
        });
    }

    public async Task UpdateAsync(UserInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        await scope.RunAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(input.Id.ToString()!) ?? throw new NotFoundException("ユーザー");
            var oldRoles = (await users.GetRolesAsync(user)).ToHashSet();
            var isSelf = user.Id == currentUser.Id;

            if (isSelf && !input.IsActive) throw new AppException("自分自身を無効にすることはできません。");
            if (isSelf && oldRoles.Contains(SystemRoles.Admin) && !input.Roles.Contains(SystemRoles.Admin))
                throw new AppException("自分自身から管理者ロールを外すことはできません。");
            var losesAdmin = oldRoles.Contains(SystemRoles.Admin) && (!input.Roles.Contains(SystemRoles.Admin) || !input.IsActive);
            if (losesAdmin && await CountOtherActiveAdminsAsync(sp, user.Id, ct) == 0)
                throw new AppException("有効な管理者が1人もいなくなるため、変更できません。");

            var changes = new List<AuditChange>();
            void Track(string name, object? oldV, object? newV)
            {
                if (!Equals(oldV, newV)) changes.Add(new AuditChange(name, oldV?.ToString(), newV?.ToString()));
            }

            var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
            Track("DisplayName", user.DisplayName, input.DisplayName.Trim());
            Track("Email", user.Email, email);
            Track("IsActive", user.IsActive, input.IsActive);
            var rolesChanged = !oldRoles.SetEquals(input.Roles);
            if (rolesChanged) changes.Add(new AuditChange("Roles", string.Join(",", oldRoles.Order()), string.Join(",", input.Roles.Order())));
            if (changes.Count == 0) return;

            user.DisplayName = input.DisplayName.Trim();
            user.Email = email;
            user.IsActive = input.IsActive;
            Check(await users.UpdateAsync(user));

            if (rolesChanged)
            {
                var remove = oldRoles.Except(input.Roles).ToList();
                var add = input.Roles.Except(oldRoles).ToList();
                if (remove.Count > 0) Check(await users.RemoveFromRolesAsync(user, remove));
                if (add.Count > 0) Check(await users.AddToRolesAsync(user, add));
            }
            // 权限或启用状态变了：让该用户已有的登录会话在下次校验时失效，重新登录后拿到新权限
            if (rolesChanged || !input.IsActive) Check(await users.UpdateSecurityStampAsync(user));

            await sp.GetRequiredService<IAuditLogger>().LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.UserUpdate,
                $"ユーザー「{user.UserName}」を変更") { EntityType = nameof(AppUser), EntityId = user.Id.ToString(), Detail = changes }, ct);
        });
    }

    /// <summary>重置密码，返回新的临时密码。</summary>
    public async Task<string> ResetPasswordAsync(Guid userId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        return await scope.RunAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("ユーザー");
            var password = PasswordGenerator.Generate();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            Check(await users.ResetPasswordAsync(user, token, password));
            user.MustChangePassword = true;
            Check(await users.UpdateAsync(user));
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);

            await sp.GetRequiredService<IAuditLogger>().LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.ResetPassword,
                $"ユーザー「{user.UserName}」のパスワードをリセット") { EntityType = nameof(AppUser), EntityId = user.Id.ToString() }, ct);
            return password;
        });
    }

    public async Task UnlockAsync(Guid userId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Users);
        await scope.RunAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("ユーザー");
            Check(await users.SetLockoutEndDateAsync(user, null));
            Check(await users.ResetAccessFailedCountAsync(user));
            await sp.GetRequiredService<IAuditLogger>().LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.UserUnlock,
                $"ユーザー「{user.UserName}」のロックを解除") { EntityType = nameof(AppUser), EntityId = user.Id.ToString() }, ct);
        });
    }

    private static async Task<int> CountOtherActiveAdminsAsync(IServiceProvider sp, Guid excludeUserId, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IAppDbContext>();
        return await (from ur in db.Db.Set<IdentityUserRole<Guid>>()
                      join r in db.Roles on ur.RoleId equals r.Id
                      join u in db.Users on ur.UserId equals u.Id
                      where r.Name == SystemRoles.Admin && u.IsActive && u.Id != excludeUserId
                      select u.Id).CountAsync(ct);
    }

    internal static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new AppException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
