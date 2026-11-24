using System.Security.Claims;
using Jw.Application.Abstractions;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Jw.Infrastructure.Identity;

/// <summary>
/// 登录凭据里除了用户和角色，还放入：显示名、是否必须改密码。
/// 角色上的权限（RoleClaim）由基类自动展开到凭据里。
/// </summary>
public sealed class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, AppRole>(userManager, roleManager, options)
{
    public const string MustChangePasswordClaim = "must_change_password";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(CurrentUser.DisplayNameClaim, string.IsNullOrEmpty(user.DisplayName) ? user.UserName! : user.DisplayName));
        if (user.MustChangePassword) identity.AddClaim(new Claim(MustChangePasswordClaim, "1"));
        return identity;
    }
}
