using Jw.Domain.Auditing;
using Jw.Domain.Common;
using Jw.Domain.Projects;

namespace Jw.Domain.Budgets;

/// <summary>予算（旧版 JwBudgetMainData）。每个工事一份。</summary>
public class Budget : AuditedEntity, IAuditable
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    /// <summary>明细金额合计（不含税），保存明细时重新计算。</summary>
    public decimal Amount { get; set; }
    public List<BudgetLine> Lines { get; set; } = [];

    public string AuditDisplay => Project?.Name ?? ProjectId.ToString();

    public void Recalculate() => Amount = Lines.Where(l => !l.IsDeleted).Sum(l => l.Amount);
}

/// <summary>予算明細（旧版 JwBudgetSubData）。名称、规格、单位、单价取自材料时的快照。</summary>
public class BudgetLine : AuditedEntity, IAuditable
{
    public Guid BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;
    public Guid? MaterialId { get; set; }

    public string ItemName { get; set; } = "";
    public string? Spec { get; set; }
    public string UnitName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public MaterialKind Kind { get; set; }
    public BudgetLineSource Source { get; set; } = BudgetLineSource.Custom;

    public string AuditDisplay => string.IsNullOrEmpty(Spec) ? ItemName : $"{ItemName} {Spec}";

    /// <summary>金额 = 数量 × 单价（与旧版一致，不做取整）。</summary>
    public void Recalculate() => Amount = Quantity * UnitPrice;
}
