using Jw.Domain.Auditing;

namespace Jw.Application.Auditing;

/// <summary>
/// 显式记录业务操作和安全事件（登录、导出、用户变更等）。
/// 实体字段级的增删改由数据库拦截器自动记录，不需要调用这里。
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed record AuditEntry(AuditCategory Category, string Action, string Summary)
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    /// <summary>附加信息，序列化为 JSON 存入 Detail。</summary>
    public object? Detail { get; init; }
    /// <summary>未登录场景（例如登录失败）用来指定操作者名。</summary>
    public string? UserNameOverride { get; init; }
    public Guid? UserIdOverride { get; init; }
}
