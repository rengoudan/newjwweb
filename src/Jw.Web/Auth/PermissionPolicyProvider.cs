using Jw.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Jw.Web.Auth;

/// <summary>
/// 策略名就是权限名：[Authorize(Policy = Permissions.Customers.View)]、
/// &lt;AuthorizeView Policy="..."&gt; 都会检查登录凭据里有没有这个权限。
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (Permissions.IsDefined(policyName))
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(Permissions.ClaimType, policyName)
                .Build();
        return await base.GetPolicyAsync(policyName);
    }
}
