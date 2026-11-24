namespace Jw.Domain.Common;

/// <summary>业务实体基类。主键用客户端生成的有序 Guid（v7），新增时审计就能拿到主键。</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
