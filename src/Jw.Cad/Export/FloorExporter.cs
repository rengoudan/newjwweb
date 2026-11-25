using System.Text;
using JwCore;
using JwShapeCommon;

namespace Jw.Cad.Export;

/// <summary>出力ファイル（ファイル名と内容）。</summary>
public sealed record ExportFile(string FileName, byte[] Content);

public enum FloorExportKind
{
    /// <summary>梁 JW 図（梁番号ごとに 1 ファイル）。</summary>
    BeamJw,
    /// <summary>番付図（上）：上F孔伏図。</summary>
    TopPlan,
    /// <summary>番付図（下）：下F孔伏図。</summary>
    BottomPlan,
    /// <summary>ブレース施工図。</summary>
    BracePlan,
    /// <summary>梁製造 CSV。</summary>
    BeamCsv,
}

/// <summary>
/// 階データから各種ファイルを作る（旧版 NewProjectMainPage の SaveSubBeamsTo / SaveSubTopCanvasTo / SaveSubBottomCanvasTo /
/// SaveSubCanvasLinesTo / ExportCsv と同じ処理）。フォルダ選択や書き込み先の決定は呼び出し側（Web）が行う。
/// JW 図面の書き出しには原生 JwwHelper を使うため、<see cref="JwwNative.Sync"/> で直列化する。
/// </summary>
public static class FloorExporter
{
    private static readonly string TemplateDir = Path.Combine(AppContext.BaseDirectory, "Templates");

    /// <summary>CSV は旧版と同じく Shift-JIS で書き出す（加工機側の読み込み形式）。</summary>
    private static readonly Encoding ShiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;

    /// <param name="parseSettings">该层导入时的解析设定（旧版用的是“最后一次解析”留在静态变量里的设定）。</param>
    public static IReadOnlyList<ExportFile> Export(JwProjectSubData floor, FloorExportKind kind, ExportSettings settings, ParseSettings? parseSettings = null)
    {
        // JwwData（原生ラッパー）の生成も含めて直列化する。生成だけをロック外で行うと、並行する読み込みと衝突して SEHException になる。
        lock (JwwNative.Sync) return ExportCore(floor, kind, settings, parseSettings);
    }

    /// <summary>
    /// 梁 1 本分の加工 CSV（旧版の梁画面の CSV 欄と同じ <c>JwBeam.ToProcessCsv()</c>）。3D 表示はこの CSV から描く。
    /// 梁がない、または CSV 対象外の梁なら null。
    /// </summary>
    public static string? BeamProcessCsv(JwProjectSubData floor, string beamId, ExportSettings settings, ParseSettings? parseSettings = null)
    {
        lock (JwwNative.Sync)
        {
            var beam = floor.DataToCanvas(settings, parseSettings).Beams.FirstOrDefault(b => b.Id == beamId);
            return beam is { HasCsv: true } ? beam.ToProcessCsv() : null;
        }
    }

    private static IReadOnlyList<ExportFile> ExportCore(JwProjectSubData floor, FloorExportKind kind, ExportSettings settings, ParseSettings? parseSettings)
    {
        var canvas = floor.DataToCanvas(settings, parseSettings);
        return kind switch
        {
            FloorExportKind.BeamJw => BeamJw(floor, canvas),
            FloorExportKind.TopPlan => [WriteJww(floor, $"{floor.FloorName} 上F孔伏図.jww", Bind(canvas).DrawShigong(true))],
            FloorExportKind.BottomPlan => [WriteJww(floor, $"{floor.FloorName} 下F孔伏図.jww", Bind(canvas).DrawShigong(false))],
            FloorExportKind.BracePlan => floor.JwLianjieDatas.Count == 0
                ? throw new InvalidOperationException("この階にはブレースデータがありません。")
                : [WriteJww(floor, $"{floor.FloorName}_ブレース施工図.jww", Bind(canvas).DrawLinesToJww())],
            FloorExportKind.BeamCsv => [BeamCsv(floor, canvas)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static JwCanvas Bind(JwCanvas canvas)
    {
        canvas.BindDrawOtherEvent();
        return canvas;
    }

    private static List<ExportFile> BeamJw(JwProjectSubData floor, JwCanvas canvas)
    {
        var files = new List<ExportFile>();
        foreach (var g in canvas.Beams.GroupBy(t => t.BeamCode))
        {
            var count = g.Count();
            var name = $"{g.Key}{(count > 1 ? $"({count})" : "")}.jww";
            var draw = new NewJwBeamJwDraw(g.First());
            draw.CreateBeam();
            if (draw.Sens.Count > 0) files.Add(WriteJww(floor, name, draw.Datas));
        }
        return files;
    }

    private static ExportFile BeamCsv(JwProjectSubData floor, JwCanvas canvas)
    {
        if (canvas.Beams.Count == 0) throw new InvalidOperationException("この階には梁がありません。");
        var csv = canvas.ToProcessCsv();
        if (string.IsNullOrEmpty(csv)) throw new InvalidOperationException("CSV に出力する梁がありません。");
        return new ExportFile($"{floor.FloorName}-3015-2.csv", ShiftJis.GetBytes(csv));
    }

    /// <summary>テンプレート（縮尺ごと）のヘッダーを使って JW 図面を書き出し、内容を返す。</summary>
    private static ExportFile WriteJww(JwProjectSubData floor, string fileName, IEnumerable<JwwHelper.JwwData> datas)
    {
        var template = Path.Combine(TemplateDir, $"template{floor.Biaochi}.jww");
        if (!File.Exists(template))
            throw new InvalidOperationException($"縮尺 1/{floor.Biaochi} の図面テンプレートがありません（対応：1/100・1/150・1/200）。");

        var temp = Path.Combine(Path.GetTempPath(), $"jw-export-{Guid.NewGuid():N}.jww");
        try
        {
            lock (JwwNative.Sync)
            {
                using var writer = new JwwHelper.JwwWriter();
                writer.InitHeader(template);
                foreach (var d in datas) writer.AddData(d);
                writer.Write(temp);
            }
            return new ExportFile(SafeName(fileName), File.ReadAllBytes(temp));
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { }
        }
    }

    private static string SafeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
