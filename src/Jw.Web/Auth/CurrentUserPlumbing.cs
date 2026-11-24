using Jw.Application.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Jw.Web.Auth;

/// <summary>普通 HTTP 请求：把登录用户、客户端 IP、请求 ID 写入当前作用域的 <see cref="CurrentUserState"/>。</summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx, CurrentUserState state)
    {
        state.Set(ctx.User, ctx.Connection.RemoteIpAddress?.ToString());
        state.SetCorrelationId(ctx.TraceIdentifier);
        return next(ctx);
    }
}

/// <summary>
/// Blazor 交互连接：连接作用域里没有 HttpContext，登录状态从 AuthenticationStateProvider 取，
/// 状态变化（例如被重新校验判为失效）时同步更新。
/// IP 由 Routes 组件在初始化时写入（来自首次 HTTP 请求）。
/// </summary>
public sealed class UserCircuitHandler(AuthenticationStateProvider authProvider, CurrentUserState state)
    : CircuitHandler, IDisposable
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        authProvider.AuthenticationStateChanged += OnChanged;
        state.Set((await authProvider.GetAuthenticationStateAsync()).User);
        state.SetCorrelationId("c-" + circuit.Id[..Math.Min(16, circuit.Id.Length)]);
    }

    private async void OnChanged(Task<AuthenticationState> task)
    {
        try { state.Set((await task).User); }
        catch { /* 登录状态取不到时保持原样，下次校验会处理 */ }
    }

    public void Dispose() => authProvider.AuthenticationStateChanged -= OnChanged;
}

/// <summary>
/// 必须改密码的用户（新建或被重置后），在改完之前只能访问账户相关页面。
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed = ["/account", "/_framework", "/_blazor", "/_content", "/not-found", "/error"];

    public Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path;
        if (ctx.User.Identity?.IsAuthenticated == true
            && ctx.User.HasClaim(Jw.Infrastructure.Identity.AppClaimsPrincipalFactory.MustChangePasswordClaim, "1")
            && HttpMethods.IsGet(ctx.Request.Method)
            && !Allowed.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase))
            && !Path.HasExtension(path.Value))
        {
            ctx.Response.Redirect("/account/change-password");
            return Task.CompletedTask;
        }
        return next(ctx);
    }
}
