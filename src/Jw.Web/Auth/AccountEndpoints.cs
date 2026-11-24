using Jw.Application.Auditing;
using Jw.Domain.Auditing;
using Jw.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Jw.Web.Auth;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // 退出：表单 POST（带防伪令牌），先记审计再注销
        app.MapPost("/account/logout", async (HttpContext ctx, SignInManager<AppUser> signIn, IAuditLogger audit) =>
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
                await audit.LogAsync(new AuditEntry(AuditCategory.Security, AuditActions.Logout, "ログアウト"));
            await signIn.SignOutAsync();
            return Results.LocalRedirect("~/account/login");
        }).RequireAuthorization();
    }
}
