using Jw.Domain.Auditing;
using Jw.Domain.Common;

namespace Jw.Domain.Materials;

/// <summary>材料种类（旧版 JwMaterialTypeData），例如“H 形鋼”“K 柱”。</summary>
public class MaterialCategory : AuditedEntity, IAuditable
{
    public string Name { get; set; } = "";
    public MaterialKind Kind { get; set; }
    /// <summary>该种类的默认材料（例如梁的默认型号）。</summary>
    public Guid? DefaultMaterialId { get; set; }
    public List<Material> Materials { get; set; } = [];

    public string AuditDisplay => Name;
}

/// <summary>材料（旧版 JwMaterialData）。预算明细从这里取名称、规格、单位、单价。</summary>
public class Material : AuditedEntity, IAuditable
{
    public Guid CategoryId { get; set; }
    public MaterialCategory Category { get; set; } = null!;
    /// <summary>与所属种类一致，冗余保存方便按大类筛选。</summary>
    public MaterialKind Kind { get; set; }

    /// <summary>材料名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>仕様（规格）。</summary>
    public string? Spec { get; set; }
    /// <summary>共通識別子：图纸解析结果按它匹配材料（如金物种类 B/BG、柱的标签）。</summary>
    public string? GeneralTitle { get; set; }
    public string UnitName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    /// <summary>数値パラメータ。</summary>
    public double NumParameter { get; set; }
    /// <summary>摘要。</summary>
    public string? Remark { get; set; }

    public string Description => string.IsNullOrEmpty(Spec) ? Name : $"{Name} {Spec}";
    public string AuditDisplay => Description;
}
