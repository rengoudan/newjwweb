namespace Jw.Domain.Common;

/// <summary>工事状态（旧版 ProjectStatus）。</summary>
public enum ProjectStatus
{
    Created = 0,
    Parsed = 1,
    Budgeted = 2,
}

/// <summary>材料大类（旧版 MaterialType：梁/柱/金物）。</summary>
public enum MaterialKind
{
    Beam = 0,
    Pillar = 1,
    Hardware = 2,
}

/// <summary>预算明细的来源（旧版 BudgetFrom）。</summary>
public enum BudgetLineSource
{
    Auto = 0,
    AutoFailed = 1,
    Custom = 2,
}

/// <summary>ブレース起点（旧版 LianjieParsingMethod）。</summary>
public enum BraceOrigin
{
    BeamEnd = 0,
    GridIntersection = 1,
}

/// <summary>柱、継手的识别方式（旧版 PillarDrawingMethod）。</summary>
public enum ShapeRecognition
{
    Line = 0,
    Shape = 1,
}

/// <summary>ブレース部材（旧版 LianjieCompentType）。</summary>
public enum BraceComponent
{
    BR8 = 0,
    VPL = 1,
}

public static class EnumNames
{
    public static string Of(ProjectStatus v) => v switch
    {
        ProjectStatus.Created => "作成済み",
        ProjectStatus.Parsed => "解析済み",
        ProjectStatus.Budgeted => "予算作成済み",
        _ => v.ToString(),
    };

    public static string Of(MaterialKind v) => v switch
    {
        MaterialKind.Beam => "梁",
        MaterialKind.Pillar => "柱",
        MaterialKind.Hardware => "金物",
        _ => v.ToString(),
    };

    public static string Of(BudgetLineSource v) => v switch
    {
        BudgetLineSource.Auto => "自動識別",
        BudgetLineSource.AutoFailed => "識別失敗",
        BudgetLineSource.Custom => "カスタム予算",
        _ => v.ToString(),
    };

    public static string Of(BraceOrigin v) => v switch
    {
        BraceOrigin.BeamEnd => "梁端部",
        BraceOrigin.GridIntersection => "通り芯交点",
        _ => v.ToString(),
    };

    public static string Of(ShapeRecognition v) => v switch
    {
        ShapeRecognition.Line => "線",
        ShapeRecognition.Shape => "図形",
        _ => v.ToString(),
    };

    public static string Of(BraceComponent v) => v.ToString();
}
