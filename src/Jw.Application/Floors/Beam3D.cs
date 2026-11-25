using Jw.Cad.Beam3D;

namespace Jw.Application.Floors;

public sealed record Beam3DHole(
    string Face, string FaceName, double Diameter, double X, double Y, bool IsPairPartner,
    double[] P1, double[] P2, IReadOnlyList<string> Problems, int LineNumber, string SourceLine);

/// <summary>
/// 梁 1 本の 3D 表示データ（旧版 JwBeam3D と同じく「加工 CSV」が唯一の入力）。
/// 座標は mm：X＝材軸（先端 0）、Y＝横（+ は上面側）、Z＝鉛直（0＝梁下端）。
/// </summary>
public sealed record Beam3DModel(
    string BeamCode, string? Message, string Csv,
    string SizeRaw, double H, double B, double Tw, double Tf, double Length,
    IReadOnlyList<Beam3DHole> Holes, IReadOnlyList<string> Warnings)
{
    public static Beam3DModel Error(string beamCode, string message, string csv = "") =>
        new(beamCode, message, csv, "", 0, 0, 0, 0, 0, [], []);

    /// <param name="pairPitch">2D 図と同じく、縦穴数 1 の CSV 行を対の 2 穴で表示するピッチ（旧版 EllipseSpacing）。</param>
    public static Beam3DModel FromCsv(string beamCode, string? csv, double pairPitch)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Error(beamCode, "この梁は CSV 出力の対象外です。");
        var product = ProcessCsvParser.Parse(csv).FirstOrDefault();
        if (product is null) return Error(beamCode, "CSV に製品データ（START～END）がありません。", csv);
        if (product.Size is not { IsH: true } s)
            return Error(beamCode, $"3D 表示は H形鋼のみ対応しています（鋼材サイズ「{product.SizeRaw}」）。", csv);

        var holes = BeamGeometry.Place(product, pairPitch)
            .Where(h => h.Source.Face != CsvFace.Bottom)
            .Select(h => new Beam3DHole(h.Source.Face.ToString(), h.FaceName, h.Source.Diameter, h.Source.X, h.Y, h.IsPairPartner,
                [h.P1.X, h.P1.Y, h.P1.Z], [h.P2.X, h.P2.Y, h.P2.Z], h.Problems, h.Source.LineNumber, h.Source.SourceLine))
            .ToList();
        return new Beam3DModel(beamCode, null, csv, s.Raw, s.H, s.B, s.Tw, s.Tf, product.Length, holes, product.Warnings);
    }
}
