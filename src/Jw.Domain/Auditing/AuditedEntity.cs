using Jw.Domain.Common;

namespace Jw.Domain.Auditing;

/// <summary>带完整审计字段、软删除和并发控制的业务实体基类。</summary>
public abstract class AuditedEntity : Entity, IAuditedEntity, ISoftDelete, IHasConcurrencyStamp
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public string? ModifiedByName { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
    public string? DeletedByName { get; set; }

    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
