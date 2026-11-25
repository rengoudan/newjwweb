using Jw.Domain.Auditing;
using Jw.Domain.Files;

namespace Jw.Domain.Projects;

/// <summary>
/// 工事の階（旧版 JwProjectSubData）。一张图纸解析后保存为一个楼层。
/// 梁・孔・柱・金物・ブレース等の明细保存在各解析结果表中（以 FloorId 关联），数据结构沿用旧版。
/// </summary>
public class Floor : AuditedEntity, IAuditable
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>階名（同一工事内で重複不可）。</summary>
    public string Name { get; set; } = "";

    /// <summary>取り込んだ図面ファイル。</summary>
    public Guid? SourceFileId { get; set; }
    public StoredFile? SourceFile { get; set; }

    // ---- 解析結果の集計（旧版 JwProjectSubData と同じ項目） ----
    public int BeamCount { get; set; }
    public int HorizontalBeamsCount { get; set; }
    public int VerticalBeamsCount { get; set; }
    public int PillarCount { get; set; }
    public int KPillarCount { get; set; }
    public int SinglePillarCount { get; set; }
    public int BCount { get; set; }
    public int BGCount { get; set; }
    /// <summary>梁製造 CSV などの出力回数。</summary>
    public int ExportCount { get; set; }

    // ---- 図面の情報 ----
    /// <summary>縮尺（1/Scale）。</summary>
    public double Scale { get; set; }
    /// <summary>縮尺の表示文字列（旧版 Biaochi）。</summary>
    public string? ScaleText { get; set; }
    public int? MarkBeam { get; set; }
    /// <summary>図面範囲（左上座標・幅・高さ、図面座標）。</summary>
    public double LocationX { get; set; }
    public double LocationY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int DirectionType { get; set; }

    // ---- 取り込み時の設定（後から再現・確認できるように保存） ----
    /// <summary>K 柱種別（旧版 KPillarType の値：900 / 910）。</summary>
    public int KPillarType { get; set; }
    /// <summary>ブレース部材（0 = BR8, 1 = VPL）。</summary>
    public int BraceComponent { get; set; }
    /// <summary>穴あけ方式：最少打孔。</summary>
    public bool MinimalStrategy { get; set; }
    /// <summary>解析に使った設定一式（JSON）。</summary>
    public string? ParseOptionsJson { get; set; }
    /// <summary>解析時の注意メッセージ（JSON 配列）。</summary>
    public string? WarningsJson { get; set; }
    public long ParseElapsedMs { get; set; }

    public string AuditDisplay => Name;
}
