using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Jw.Web.Auth;

/// <summary>停用的用户不能登录（SignInManager 登录时会调用）。</summary>
public sealed class ActiveUserConfirmation : IUserConfirmation<AppUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<AppUser> manager, AppUser user) => Task.FromResult(user.IsActive);
}
