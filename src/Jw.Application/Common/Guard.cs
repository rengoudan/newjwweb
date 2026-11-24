using Jw.Application.Abstractions;

namespace Jw.Application.Common;

public static class Guard
{
    /// <summary>服务端权限校验。页面上隐藏按钮只是体验，真正的拦截在这里。</summary>
    public static void Require(this ICurrentUser user, string permission)
    {
        if (!user.HasPermission(permission)) throw new ForbiddenException(permission);
    }
}
