using Jw.Domain.Auditing;
using Jw.Domain.Common;

namespace Jw.Domain.Customers;

/// <summary>
/// 顧客ごとの図面解析設定（旧版 JwCustDesignConstData）。每个顧客一条。
/// 线色、线型番号对应 JW_CAD 的 lc / ls 番号；-1 表示未设定。
/// </summary>
public class CustomerParseSetting : AuditedEntity, IAuditable
{
    public const int Unset = -1;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>梁輪郭線の線色。</summary>
    public int BeamColor { get; set; } = Unset;
    /// <summary>注釈（梁符号文字）の線色。</summary>
    public int BeamTextColor { get; set; } = Unset;
    /// <summary>柱の線色。</summary>
    public int PillarColor { get; set; } = Unset;
    /// <summary>柱の線種。</summary>
    public int PillarPenStyle { get; set; } = Unset;
    /// <summary>柱識別方法。</summary>
    public ShapeRecognition PillarRecognition { get; set; } = ShapeRecognition.Line;
    /// <summary>継手記号の線色。</summary>
    public int SplitColor { get; set; } = Unset;
    /// <summary>継手記号の線種。</summary>
    public int SplitPenStyle { get; set; } = Unset;
    /// <summary>継手記号の識別方法。</summary>
    public ShapeRecognition SplitRecognition { get; set; } = ShapeRecognition.Line;
    /// <summary>識別範囲エラー（継手を近いとみなす最大距離）。</summary>
    public double NearSplitMax { get; set; } = 1;
    /// <summary>下階柱記号の線色。</summary>
    public int DownPillarColor { get; set; } = Unset;
    /// <summary>ブレースの線色。</summary>
    public int BraceColor { get; set; } = Unset;
    public BraceOrigin BraceOrigin { get; set; } = BraceOrigin.BeamEnd;
    public BraceComponent BraceComponent { get; set; } = BraceComponent.BR8;

    /// <summary>拾い精度（旧版既定 6）。</summary>
    public int PickPrecision { get; set; } = 6;
    /// <summary>隙間（旧版既定 120）。</summary>
    public double Gap { get; set; } = 120;
    /// <summary>縮尺（旧版既定 100）。</summary>
    public double Scale { get; set; } = 100;

    public string AuditDisplay => Customer?.CompanyName ?? CustomerId.ToString();
}
