using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Domain.Auditing;

namespace Jw.Infrastructure.Auditing;

/// <summary>显式审计记录。用独立的短生命周期上下文写入，不受调用方事务影响。</summary>
internal sealed class AuditLogger(IAppDbContextFactory dbFactory, ICurrentUser currentUser, TimeProvider clock) : IAuditLogger
{
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            Time = clock.GetUtcNow().UtcDateTime,
            Category = entry.Category,
            Action = entry.Action,
            UserId = entry.UserIdOverride ?? currentUser.Id,
            UserName = entry.UserNameOverride ?? currentUser.UserName,
            IpAddress = currentUser.IpAddress,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Summary = entry.Summary.Length > 500 ? entry.Summary[..500] : entry.Summary,
            Detail = entry.Detail switch
            {
                null => null,
                string s => s,
                var o => JsonSerializer.Serialize(o),
            },
            CorrelationId = currentUser.CorrelationId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
