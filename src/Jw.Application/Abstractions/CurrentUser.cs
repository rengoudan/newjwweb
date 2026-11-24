using System.Security.Claims;
using Jw.Domain.Authorization;

namespace Jw.Application.Abstractions;

/// <summary>
/// 当前操作者的上下文（每个作用域一份）。
/// - 普通 HTTP 请求：由 Web 层中间件在请求开始时写入。
/// - Blazor 交互连接：由 Web 层的连接处理器写入，并在登录状态变化时更新。
/// - 服务内部新开作用域时，用 <see cref="CopyFrom"/> 把操作者带过去。
/// </summary>
public sealed class CurrentUserState
{
    public ClaimsPrincipal Principal { get; private set; } = new(new ClaimsIdentity());
    public string? IpAddress { get; private set; }
    public string CorrelationId { get; private set; } = Guid.NewGuid().ToString("N");

    public void Set(ClaimsPrincipal principal, string? ipAddress = null)
    {
        Principal = principal;
        if (ipAddress is not null) IpAddress = ipAddress;
    }

    public void SetIpAddress(string? ipAddress) => IpAddress = ipAddress;

    public void SetCorrelationId(string correlationId) => CorrelationId = correlationId;

    public void CopyFrom(CurrentUserState other)
    {
        Principal = other.Principal;
        IpAddress = other.IpAddress;
        CorrelationId = other.CorrelationId;
    }
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string? UserName { get; }
    string? DisplayName { get; }
    string? IpAddress { get; }
    string CorrelationId { get; }
    bool HasPermission(string permission);
}

public sealed class CurrentUser(CurrentUserState state) : ICurrentUser
{
    public const string DisplayNameClaim = "display_name";

    private ClaimsPrincipal P => state.Principal;

    public bool IsAuthenticated => P.Identity?.IsAuthenticated == true;

    public Guid? Id => Guid.TryParse(P.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => IsAuthenticated ? P.Identity!.Name : null;

    public string? DisplayName => P.FindFirstValue(DisplayNameClaim) ?? UserName;

    public string? IpAddress => state.IpAddress;

    public string CorrelationId => state.CorrelationId;

    public bool HasPermission(string permission) =>
        IsAuthenticated && P.HasClaim(Permissions.ClaimType, permission);
}
