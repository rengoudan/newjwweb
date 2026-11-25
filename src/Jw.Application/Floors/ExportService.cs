using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Budgets;
using Jw.Application.Common;
using Jw.Cad.Export;
using Jw.Cad.Parsing;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Jw.Domain.Projects;
using JwCore;
using JwShapeCommon;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

public sealed class ExportSettingsInput
{
    [Range(0.1, 100, ErrorMessage = "孔径は 0.1～100 mm で入力してください。")]
    public double HoleDiameter { get; set; } = 17.5;

    [Range(-100, 100, ErrorMessage = "-100～100 mm で入力してください。")]
    public double CsvYAdjust { get; set; }
}

/// <summary>
/// 出力（JW 図面・梁製造 CSV）。旧版は工事一覧の右クリックメニューから階ごとに出力していた。
/// 新版は階のデータと「その階を取り込んだときの解析設定」から作り、ブラウザにダウンロードさせる。
/// </summary>
public sealed class ExportService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IAuditLogger audit)
{
    private const string Zip = "application/zip";

    public static string KindName(FloorExportKind kind) => kind switch
    {
        FloorExportKind.BeamJw => "梁JW出力",
        FloorExportKind.TopPlan => "番付図上JW出力",
        FloorExportKind.BottomPlan => "番付図下JW出力",
        FloorExportKind.BracePlan => "ブレース施工図",
        _ => "梁製造CSV",
    };

    // ---------------- 出力設定 ----------------

    public async Task<ExportSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateAsync(ct);
        var s = await LoadSettingsAsync(db, ct);
        return new ExportSettingsInput { HoleDiameter = s.HoleDiameter, CsvYAdjust = s.CsvYAdjust };
    }

    public async Task SaveSettingsAsync(ExportSettingsInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Admin.Settings);
        await using var db = await dbFactory.CreateAsync(ct);
        await UpsertAsync(db, SystemSetting.Keys.HoleDiameter, input.HoleDiameter, ct);
        await UpsertAsync(db, SystemSetting.Keys.CsvYAdjust, input.CsvYAdjust, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertAsync(IAppDbContext db, string key, double value, CancellationToken ct)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var s = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (s is null) db.SystemSettings.Add(new SystemSetting { Key = key, Value = text });
        else if (s.Value != text) s.Value = text;
    }

    /// <summary>未設定のときは旧版の実際の設定値（孔径 17.5、調整 0）。</summary>
    private static async Task<ExportSettings> LoadSettingsAsync(IAppDbContext db, CancellationToken ct)
    {
        var map = await db.SystemSettings.AsNoTracking().Where(s => s.Key.StartsWith("Export."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        double Get(string k, double d) => map.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : d;
        return new ExportSettings
        {
            HoleDiameter = Get(SystemSetting.Keys.HoleDiameter, 17.5),
            CsvYAdjust = Get(SystemSetting.Keys.CsvYAdjust, 0),
        };
    }

    // ---------------- 出力 ----------------

    /// <summary>1 階の出力。ファイルが複数（梁 JW 図）のときは zip にまとめる。</summary>
    public async Task<ExportedFile> ExportFloorAsync(Guid floorId, FloorExportKind kind, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Export);
        await using var db = await dbFactory.CreateAsync(ct);
        var floor = await db.Floors.Include(f => f.Project).FirstOrDefaultAsync(f => f.Id == floorId, ct) ?? throw new NotFoundException("階");
        var settings = await LoadSettingsAsync(db, ct);
        var data = await LoadFloorDataAsync(db, floor, ct);

        var files = Run(() => FloorExporter.Export(data, kind, settings, ParseSettingsOf(floor)));
        if (files.Count == 0) throw new AppException("出力する図面がありません。");

        if (kind == FloorExportKind.BeamCsv) floor.ExportCount++;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "Export", $"{KindName(kind)}：{floor.Project.Name} {floor.Name}")
        {
            EntityType = nameof(Floor), EntityId = floor.Id.ToString(),
            Detail = new { Kind = kind.ToString(), Files = files.Select(f => f.FileName).ToList(), settings.HoleDiameter, settings.CsvYAdjust },
        }, ct);

        if (files.Count == 1)
        {
            var f = files[0];
            var type = f.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/octet-stream";
            return new ExportedFile(f.FileName, type, f.Content);
        }
        return new ExportedFile($"{floor.Project.Name}_{floor.Name}_{KindName(kind)}.zip", Zip,
            ZipOf(files.Select(f => ($"{floor.Name}/{f.FileName}", f.Content))));
    }

    /// <summary>工事の一括出力：全階の梁 JW 図・番付図（上下）・ブレース施工図・梁製造 CSV を 1 つの zip に。</summary>
    public async Task<ExportedFile> ExportProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Export);
        await using var db = await dbFactory.CreateAsync(ct);
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct) ?? throw new NotFoundException("工事");
        var floors = await db.Floors.Where(f => f.ProjectId == projectId).OrderBy(f => f.CreatedAt).ToListAsync(ct);
        if (floors.Count == 0) throw new AppException("取り込み済みの階がありません。");
        var settings = await LoadSettingsAsync(db, ct);

        var entries = new List<(string, byte[])>();
        var skipped = new List<string>();
        foreach (var floor in floors)
        {
            var data = await LoadFloorDataAsync(db, floor, ct);
            foreach (var kind in Enum.GetValues<FloorExportKind>())
            {
                if (kind == FloorExportKind.BracePlan && data.JwLianjieDatas.Count == 0) continue;
                try
                {
                    var folder = kind == FloorExportKind.BeamJw ? $"{floor.Name}/梁JW" : floor.Name;
                    foreach (var f in FloorExporter.Export(data, kind, settings, ParseSettingsOf(floor)))
                        entries.Add(($"{folder}/{f.FileName}", f.Content));
                    if (kind == FloorExportKind.BeamCsv) floor.ExportCount++;
                }
                catch (InvalidOperationException ex)
                {
                    skipped.Add($"{floor.Name}：{KindName(kind)}（{ex.Message}）");
                }
            }
        }
        if (skipped.Count > 0) entries.Add(("出力できなかった項目.txt", System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", skipped))));
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "Export", $"一括出力：{project.Name}（{floors.Count} 階・{entries.Count} ファイル）")
        {
            EntityType = nameof(Project), EntityId = project.Id.ToString(), Detail = new { Floors = floors.Select(f => f.Name), Files = entries.Count, Skipped = skipped },
        }, ct);
        return new ExportedFile($"{project.Name}_一括出力.zip", Zip, ZipOf(entries));
    }

    /// <summary>DB の階データを旧版と同じ構造（JwProjectSubData と子コレクション）に組み立てる。</summary>
    private static async Task<JwProjectSubData> LoadFloorDataAsync(IAppDbContext db, Floor f, CancellationToken ct)
    {
        var key = f.Id.ToString();
        var beams = await db.FloorBeams.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct);
        var beamIds = beams.Select(b => b.Id).ToList();
        var sub = new JwProjectSubData
        {
            Id = key, FloorName = f.Name, Biaochi = f.ScaleText, Scale = f.Scale, MarkBeam = f.MarkBeam,
            Location = new NetTopologySuite.Geometries.Point(f.LocationX, f.LocationY), Width = f.Width, Height = f.Height,
            DirectionType = (BeamDirectionType)f.DirectionType,
            BeamCount = f.BeamCount, HorizontalBeamsCount = f.HorizontalBeamsCount, VerticalBeamsCount = f.VerticalBeamsCount,
            PillarCount = f.PillarCount, KPillarCount = f.KPillarCount, SinglePillarCount = f.SinglePillarCount,
            BCount = f.BCount, BGCount = f.BGCount, ExportCount = f.ExportCount,
            KPillarType = (KPillarType)f.KPillarType, LianjieCompentType = (LianjieCompentType)f.BraceComponent, MinimalStrategy = f.MinimalStrategy,
        };
        return FloorDataBuilder.Build(sub, beams,
            await db.FloorHoles.AsNoTracking().Where(h => beamIds.Contains(h.JwBeamDataId)).InOrder().ToListAsync(ct),
            await db.FloorBeamVerticals.AsNoTracking().Where(v => beamIds.Contains(v.JwBeamDataId)).InOrder().ToListAsync(ct),
            await db.FloorPillars.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct),
            await db.FloorLinkParts.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct),
            await db.FloorLianjies.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct),
            await db.FloorDownPillars.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct),
            await db.FloorCuttings.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct),
            await db.FloorAddedHoleMarks.AsNoTracking().Where(x => x.JwProjectSubDataId == key).InOrder().ToListAsync(ct));
    }

    // ---------------- 3D 表示 ----------------

    /// <summary>梁 1 本の 3D 表示データ。旧版の梁画面の「3D」と同じく、その梁の加工 CSV から作る。</summary>
    public async Task<Beam3DModel> GetBeam3DAsync(Guid floorId, string beamId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var f = await db.Floors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == floorId, ct) ?? throw new NotFoundException("階");
        var code = await db.FloorBeams.AsNoTracking().Where(b => b.Id == beamId && b.JwProjectSubDataId == f.Id.ToString())
            .Select(b => b.BeamCode).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("梁");
        var settings = await LoadSettingsAsync(db, ct);
        var data = await LoadFloorDataAsync(db, f, ct);
        try
        {
            var csv = FloorExporter.BeamProcessCsv(data, beamId, settings, ParseSettingsOf(f));
            return Beam3DModel.FromCsv(code, csv, JwConstants.EllipseSpacing);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 3D 表示の失敗で画面全体を止めない（旧版と同じ）
            return Beam3DModel.Error(code, "3D 表示に失敗しました: " + ex.Message);
        }
    }

    /// <summary>その階を取り込んだときの解析設定（旧版は「最後に解析したときの設定」が残った静的変数を使っていた）。</summary>
    private static ParseSettings? ParseSettingsOf(Floor f) =>
        string.IsNullOrEmpty(f.ParseOptionsJson) ? null : JsonSerializer.Deserialize<ParseOptions>(f.ParseOptionsJson)?.ToSettings();

    private static IReadOnlyList<ExportFile> Run(Func<IReadOnlyList<ExportFile>> export)
    {
        try { return export(); }
        catch (InvalidOperationException ex) { throw new AppException(ex.Message); }
    }

    private static byte[] ZipOf(IEnumerable<(string Path, byte[] Content)> entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var e = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var s = e.Open();
                s.Write(content);
            }
        }
        return ms.ToArray();
    }
}
