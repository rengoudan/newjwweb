using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Auditing;

public sealed class AuditQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public AuditCategory? Category { get; set; }
    public string? UserName { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    /// <summary>在说明、对象 ID 中模糊查找。</summary>
    public string? Keyword { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
}

public sealed record AuditLogItem(
    long Id, DateTime Time, AuditCategory Category, string Action, string? UserName, string? IpAddress,
    string? EntityType, string? EntityId, string Summary, string? Detail, string? CorrelationId)
{
    /// <summary>Detail 能解析成字段变更列表时返回列表，否则返回 null（按原文显示）。</summary>
    public IReadOnlyList<AuditChange>? Changes
    {
        get
        {
            if (Category != AuditCategory.Data || string.IsNullOrEmpty(Detail)) return null;
            try { return JsonSerializer.Deserialize<List<AuditChange>>(Detail); }
            catch (JsonException) { return null; }
        }
    }
}

public sealed class AuditQueryService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<PagedResult<AuditLogItem>> SearchAsync(AuditQuery q, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Audit);
        await using var db = await dbFactory.CreateAsync(ct);

        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (q.FromUtc is { } from) query = query.Where(a => a.Time >= from);
        if (q.ToUtc is { } to) query = query.Where(a => a.Time < to);
        if (q.Category is { } cat) query = query.Where(a => a.Category == cat);
        if (!string.IsNullOrWhiteSpace(q.UserName)) query = query.Where(a => a.UserName != null && a.UserName.Contains(q.UserName));
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
            query = query.Where(a => a.Summary.Contains(q.Keyword) || (a.EntityId != null && a.EntityId.Contains(q.Keyword)));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Id)
            .Skip(q.Page * q.PageSize).Take(q.PageSize)
            .Select(a => new AuditLogItem(a.Id, a.Time, a.Category, a.Action, a.UserName, a.IpAddress,
                a.EntityType, a.EntityId, a.Summary, a.Detail, a.CorrelationId))
            .ToListAsync(ct);
        return new PagedResult<AuditLogItem>(items, total);
    }

    /// <summary>某条业务数据的变更履历（详情页用）。</summary>
    public async Task<IReadOnlyList<AuditLogItem>> HistoryAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        var r = await SearchAsync(new AuditQuery { EntityType = entityType, EntityId = entityId, PageSize = 200 }, ct);
        return r.Items;
    }
}
