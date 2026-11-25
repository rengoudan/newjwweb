using Jw.Domain.Auditing;
using Jw.Domain.Common;
using Jw.Domain.Customers;

namespace Jw.Domain.Projects;

/// <summary>
/// 工事（旧版 JwProjectMainData）。
/// 統計（梁数・柱数など）由第 3 阶段的图纸解析写入，这里先占好字段。
/// </summary>
public class Project : AuditedEntity, IAuditable
{
    public string Name { get; set; } = "";
    public string? SiteAddress { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>计划层数，也是可导入图纸的层数上限。</summary>
    public int FloorQuantity { get; set; } = 1;
    /// <summary>已导入（解析）的层数。</summary>
    public int ParsedQuantity { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Created;
    /// <summary>縮尺（来自图纸）。</summary>
    public string? Scale { get; set; }

    public int BeamCount { get; set; }
    public int PillarCount { get; set; }
    public int KPillarCount { get; set; }
    public int SinglePillarCount { get; set; }
    public int BCount { get; set; }
    public int BGCount { get; set; }
    public int ExportCount { get; set; }

    public string AuditDisplay => Name;

    /// <summary>导入图纸前的检查；可以导入时返回 null。</summary>
    public string? ImportBlockedReason()
    {
        if (FloorQuantity <= 0) return "この工事の階数が 0 のため、図面を取り込めません。階数を設定してください。";
        if (ParsedQuantity >= FloorQuantity)
            return $"階数 {FloorQuantity} 階分の図面がすべて取り込み済みです。取り込み直す場合は不要な階を削除するか、階数を変更してください。";
        return null;
    }

    /// <summary>层数不能少于已导入的层数，且至少 1 层。</summary>
    public int MinFloorQuantity => Math.Max(1, ParsedQuantity);
}
