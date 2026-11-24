namespace Jw.Domain.Auditing;

/// <summary>由审计拦截器自动填写的创建、修改信息。</summary>
public interface IAuditedEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedById { get; set; }
    string? CreatedByName { get; set; }
    DateTime? ModifiedAt { get; set; }
    Guid? ModifiedById { get; set; }
    string? ModifiedByName { get; set; }
}

/// <summary>软删除：删除时只做标记，数据和审计记录都保留。</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedById { get; set; }
    string? DeletedByName { get; set; }
}

/// <summary>乐观并发控制：每次保存时更新，多人同时修改时后提交的会失败。</summary>
public interface IHasConcurrencyStamp
{
    Guid ConcurrencyStamp { get; set; }
}

/// <summary>实现该接口的实体，其增、改、删会逐字段写入审计日志。</summary>
public interface IAuditable
{
    /// <summary>在审计日志里显示的名称，例如顾客名。</summary>
    string AuditDisplay { get; }
}
