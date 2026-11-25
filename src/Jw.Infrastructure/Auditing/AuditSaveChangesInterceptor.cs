using System.Globalization;
using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jw.Infrastructure.Auditing;

/// <summary>
/// 保存前统一处理：
/// 1. 填写创建人/修改人/删除人和时间；
/// 2. 删除转软删除；
/// 3. 更新并发令牌；
/// 4. 对 <see cref="IAuditable"/> 实体逐字段记录变更，与业务数据在同一个事务里写入 AuditLogs。
/// 业务代码不需要为审计写任何代码。
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    /// <summary>不记入变更明细的字段（由系统维护）。</summary>
    private static readonly HashSet<string> Ignored =
    [
        nameof(IAuditedEntity.CreatedAt), nameof(IAuditedEntity.CreatedById), nameof(IAuditedEntity.CreatedByName),
        nameof(IAuditedEntity.ModifiedAt), nameof(IAuditedEntity.ModifiedById), nameof(IAuditedEntity.ModifiedByName),
        nameof(ISoftDelete.DeletedAt), nameof(ISoftDelete.DeletedById), nameof(ISoftDelete.DeletedByName),
        nameof(IHasConcurrencyStamp.ConcurrencyStamp),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();

        var now = clock.GetUtcNow().UtcDateTime;
        var userId = currentUser.Id;
        var userName = currentUser.UserName;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditActions.Create,
                EntityState.Deleted => AuditActions.Delete,
                _ => AuditActions.Update,
            };

            // 变更明细要在改写审计字段之前取
            var changes = entry.Entity is IAuditable ? CollectChanges(entry) : null;

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAt = now;
                soft.DeletedById = userId;
                soft.DeletedByName = userName;
            }

            if (entry.Entity is IAuditedEntity audited)
            {
                if (entry.State == EntityState.Added)
                {
                    audited.CreatedAt = now;
                    audited.CreatedById = userId;
                    audited.CreatedByName = userName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // 创建信息不允许被改写
                    entry.Property(nameof(IAuditedEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditedEntity.CreatedById)).IsModified = false;
                    entry.Property(nameof(IAuditedEntity.CreatedByName)).IsModified = false;
                    audited.ModifiedAt = now;
                    audited.ModifiedById = userId;
                    audited.ModifiedByName = userName;
                }
            }

            if (entry.Entity is IHasConcurrencyStamp stamped && entry.State == EntityState.Modified)
                stamped.ConcurrencyStamp = Guid.NewGuid();

            if (entry.Entity is IAuditable auditable && (changes!.Count > 0 || action != AuditActions.Update))
            {
                var typeName = entry.Metadata.ClrType.Name;
                logs.Add(new AuditLog
                {
                    Time = now,
                    Category = AuditCategory.Data,
                    Action = action,
                    UserId = userId,
                    UserName = userName,
                    IpAddress = currentUser.IpAddress,
                    EntityType = typeName,
                    EntityId = KeyOf(entry),
                    Summary = $"{EntityDisplayNames.Of(typeName)}「{auditable.AuditDisplay}」を{AuditActions.DisplayName(action)}",
                    Detail = changes!.Count > 0 ? JsonSerializer.Serialize(changes) : null,
                    CorrelationId = currentUser.CorrelationId,
                });
            }
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private static List<AuditChange> CollectChanges(EntityEntry entry)
    {
        var list = new List<AuditChange>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (Ignored.Contains(name) || p.Metadata.IsPrimaryKey()) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    if (p.CurrentValue is not null && name != nameof(ISoftDelete.IsDeleted))
                        list.Add(new AuditChange(name, null, Format(p.CurrentValue)));
                    break;
                case EntityState.Deleted:
                    if (p.OriginalValue is not null && name != nameof(ISoftDelete.IsDeleted))
                        list.Add(new AuditChange(name, Format(p.OriginalValue), null));
                    break;
                case EntityState.Modified:
                    if (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        list.Add(new AuditChange(name, Format(p.OriginalValue), Format(p.CurrentValue)));
                    break;
            }
        }
        return list;
    }

    private static string? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return null;
        return string.Join(",", key.Properties.Select(p => Format(entry.Property(p.Name).CurrentValue)));
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}

/// <summary>实体类名在审计说明里的显示名。新增业务实体时在这里补一行。</summary>
public static class EntityDisplayNames
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["Customer"] = "顧客",
        ["CustomerParseSetting"] = "解析設定",
        ["Floor"] = "階",
        ["SystemSetting"] = "システム設定",
        ["Project"] = "工事",
        ["MaterialCategory"] = "材料種類",
        ["Material"] = "材料",
        ["Budget"] = "予算",
        ["BudgetLine"] = "予算明細",
        ["AppUser"] = "ユーザー",
        ["AppRole"] = "ロール",
    };

    public static string Of(string typeName) => Map.GetValueOrDefault(typeName, typeName);
}
