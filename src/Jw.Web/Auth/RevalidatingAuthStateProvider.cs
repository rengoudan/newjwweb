using System.Security.Claims;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Jw.Web.Auth;

/// <summary>
/// Blazor 交互连接会持续很久。每分钟检查一次：用户被停用、角色或权限变了（安全戳变化）时，
/// 连接里的登录状态立即失效，页面会转到登录页，重新登录后拿到新权限。
/// </summary>
public sealed class RevalidatingAuthStateProvider(
    ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(state.User);
        if (user is null || !user.IsActive) return false;
        if (!users.SupportsUserSecurityStamp) return true;
        var principalStamp = state.User.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        return principalStamp == await users.GetSecurityStampAsync(user);
    }
}
