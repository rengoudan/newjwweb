using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Projects;
using JwCore;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

/// <summary>
/// 要確認の判定（旧版 FloorCheck と同じ基準）：梁が無い・柱が無い・穴が 1 つも無い梁がある。
/// </summary>
public sealed record FloorCheck(int BeamRows, int PillarRows, int BeamsWithoutHoles)
{
    public bool NeedsCheck => PillarRows == 0 || BeamRows == 0 || BeamsWithoutHoles > 0;

    public string Reason
    {
        get
        {
            var parts = new List<string>();
            if (BeamRows == 0) parts.Add("梁なし");
            if (PillarRows == 0) parts.Add("柱なし");
            if (BeamsWithoutHoles > 0) parts.Add($"穴なし梁 {BeamsWithoutHoles}本");
            return string.Join("・", parts);
        }
    }
}

public sealed record FloorListItem(
    Guid Id, Guid ProjectId, string Name, int BeamCount, int PillarCount, int KPillarCount, int SinglePillarCount,
    int BCount, int BGCount, int ExportCount, string? ScaleText, DateTime CreatedAt, string? CreatedByName, FloorCheck Check);

public sealed record BeamRow(
    string Id, string BeamCode, string InitialBeamCode, string? GongQu, BeamDirectionType Direction, double Length,
    KongzuType StartType, KongzuType EndType, int HoleCount, bool IsQiegeBeam, bool HasCsv, double X, double Y);

public sealed record PillarRow(string Id, string PillarCode, PillarBaseType BaseType, string? TaggTitle, double X, double Y);

public sealed record LinkPartRow(string Id, GouJianType Type, string? BujianName, string? BeamId, bool IsNoBeam, double X, double Y);

public sealed record LianjieRow(string Id, double Length, double StartX, double StartY, double EndX, double EndY);

public sealed record FloorWorkbench(
    Guid Id, Guid ProjectId, string ProjectName, string Name, string? ScaleText, string? FileName, Guid? FileId,
    DateTime CreatedAt, string? CreatedByName, IReadOnlyList<string> Warnings, string? ParseOptionsJson, long ParseElapsedMs,
    FloorCheck Check, int KPillarType, int BraceComponent, bool MinimalStrategy, int ExportCount,
    IReadOnlyList<BeamRow> Beams, IReadOnlyList<PillarRow> Pillars, IReadOnlyList<LinkPartRow> LinkParts, IReadOnlyList<LianjieRow> Lianjies);

public sealed class BeamEditInput
{
    public string BeamId { get; set; } = "";

    [Required(ErrorMessage = "梁番号を入力してください。")]
    [StringLength(50)]
    public string BeamCode { get; set; } = "";

    [StringLength(50)]
    public string? GongQu { get; set; }

    public bool HasCsv { get; set; } = true;
}

/// <summary>階（解析済み図面）の参照・削除と、梁の修正。</summary>
public sealed class FloorService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IAuditLogger audit)
{
    public async Task<IReadOnlyList<FloorListItem>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var floors = await db.Floors.AsNoTracking().Where(f => f.ProjectId == projectId).OrderBy(f => f.CreatedAt).ToListAsync(ct);
        var checks = await ChecksAsync(db, floors.Select(f => f.Id.ToString()).ToList(), ct);
        return floors.Select(f => new FloorListItem(f.Id, f.ProjectId, f.Name, f.BeamCount, f.PillarCount, f.KPillarCount,
            f.SinglePillarCount, f.BCount, f.BGCount, f.ExportCount, f.ScaleText, f.CreatedAt, f.CreatedByName,
            checks.GetValueOrDefault(f.Id.ToString(), new FloorCheck(0, 0, 0)))).ToList();
    }

    /// <summary>要確認の階（ワークベンチの「対応が必要なもの」用）。</summary>
    public async Task<IReadOnlyList<(FloorListItem Floor, string ProjectName)>> NeedsCheckAsync(int take = 20, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var floors = await db.Floors.AsNoTracking().Include(f => f.Project).Where(f => !f.Project.IsDeleted)
            .OrderByDescending(f => f.CreatedAt).Take(200).ToListAsync(ct);
        var checks = await ChecksAsync(db, floors.Select(f => f.Id.ToString()).ToList(), ct);
        return floors.Select(f => (Floor: new FloorListItem(f.Id, f.ProjectId, f.Name, f.BeamCount, f.PillarCount, f.KPillarCount,
                    f.SinglePillarCount, f.BCount, f.BGCount, f.ExportCount, f.ScaleText, f.CreatedAt, f.CreatedByName,
                    checks.GetValueOrDefault(f.Id.ToString(), new FloorCheck(0, 0, 0))), ProjectName: f.Project.Name))
            .Where(x => x.Floor.Check.NeedsCheck).Take(take).ToList();
    }

    public async Task<FloorWorkbench> GetWorkbenchAsync(Guid floorId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var f = await db.Floors.AsNoTracking().Include(x => x.Project).Include(x => x.SourceFile)
                    .FirstOrDefaultAsync(x => x.Id == floorId, ct) ?? throw new NotFoundException("階");
        var key = f.Id.ToString();

        var holeCounts = await (from h in db.FloorHoles
                                join b in db.FloorBeams on h.JwBeamDataId equals b.Id
                                where b.JwProjectSubDataId == key
                                group h by h.JwBeamDataId into g
                                select new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var beams = (await db.FloorBeams.AsNoTracking().Where(b => b.JwProjectSubDataId == key).ToListAsync(ct))
            .Select(b => new BeamRow(b.Id, b.BeamCode, b.InitialBeamCode, b.GongQu, b.DirectionType, b.Length,
                b.StartTelosType, b.EndTelosType, holeCounts.GetValueOrDefault(b.Id), b.IsQiegeBeam, b.HasCsv,
                b.CenterPoint?.X ?? b.Location?.X ?? 0, b.CenterPoint?.Y ?? b.Location?.Y ?? 0))
            .OrderBy(b => b.BeamCode, NaturalComparer.Instance).ToList();

        var pillars = (await db.FloorPillars.AsNoTracking().Where(p => p.JwProjectSubDataId == key).ToListAsync(ct))
            .Select(p => new PillarRow(p.Id, p.PillarCode, p.BaseType, p.TaggTitle, p.Location?.X ?? 0, p.Location?.Y ?? 0))
            .OrderBy(p => p.PillarCode, NaturalComparer.Instance).ToList();

        var links = (await db.FloorLinkParts.AsNoTracking().Where(l => l.JwProjectSubDataId == key).ToListAsync(ct))
            .Select(l => new LinkPartRow(l.Id, l.GouJianType, l.BujianName, l.BeamId, l.IsNoBeam, l.Location?.X ?? 0, l.Location?.Y ?? 0))
            .OrderBy(l => l.Type).ThenBy(l => l.X).ThenBy(l => l.Y).ToList();

        var lianjies = (await db.FloorLianjies.AsNoTracking().Where(l => l.JwProjectSubDataId == key).ToListAsync(ct))
            .Select(l => new LianjieRow(l.Id, l.Length, l.Start.X, l.Start.Y, l.End.X, l.End.Y)).ToList();

        var warnings = string.IsNullOrEmpty(f.WarningsJson) ? [] : JsonSerializer.Deserialize<List<string>>(f.WarningsJson) ?? [];
        var check = new FloorCheck(beams.Count, pillars.Count, beams.Count(b => b.HoleCount == 0));

        return new FloorWorkbench(f.Id, f.ProjectId, f.Project.Name, f.Name, f.ScaleText, f.SourceFile?.FileName, f.SourceFileId,
            f.CreatedAt, f.CreatedByName, warnings, f.ParseOptionsJson, f.ParseElapsedMs, check, f.KPillarType, f.BraceComponent,
            f.MinimalStrategy, f.ExportCount, beams, pillars, links, lianjies);
    }

    /// <summary>梁番号・工区・CSV 出力対象の修正（旧版 ChangeBeamCode / ChangeBeamGongqu / ChangeBeamHasCsv）。</summary>
    public async Task UpdateBeamAsync(BeamEditInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var beam = await db.FloorBeams.FirstOrDefaultAsync(b => b.Id == input.BeamId, ct) ?? throw new NotFoundException("梁");
        if (!Guid.TryParse(beam.JwProjectSubDataId, out var floorId) || !await db.Floors.AnyAsync(f => f.Id == floorId, ct))
            throw new NotFoundException("階");

        var code = input.BeamCode.Trim();
        var gongqu = string.IsNullOrWhiteSpace(input.GongQu) ? "" : input.GongQu.Trim();
        var changes = new List<AuditChange>();
        if (beam.BeamCode != code) changes.Add(new("BeamCode", beam.BeamCode, code));
        if ((beam.GongQu ?? "") != gongqu) changes.Add(new("GongQu", beam.GongQu, gongqu));
        if (beam.HasCsv != input.HasCsv) changes.Add(new("HasCsv", beam.HasCsv.ToString(), input.HasCsv.ToString()));
        if (changes.Count == 0) return;

        beam.BeamCode = code;
        beam.GongQu = gongqu;
        beam.HasCsv = input.HasCsv;
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry(AuditCategory.Data, AuditActions.Update, $"梁「{code}」を変更（{beam.FloorName}）")
        {
            EntityType = "FloorBeam", EntityId = beam.Id, Detail = changes,
        }, ct);
    }

    /// <summary>階を削除する（論理削除）。解析明細は追跡用に残し、工事の集計を再計算する。</summary>
    public async Task DeleteAsync(Guid floorId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Delete);
        await using var db = await dbFactory.CreateAsync(ct);
        var floor = await db.Floors.FirstOrDefaultAsync(f => f.Id == floorId, ct) ?? throw new NotFoundException("階");
        var project = await db.Projects.FirstAsync(p => p.Id == floor.ProjectId, ct);
        db.Floors.Remove(floor);
        await ProjectStats.RecalculateAsync(db, project, removedFloorId: floor.Id, ct: ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, FloorCheck>> ChecksAsync(IAppDbContext db, List<string> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return [];
        var beams = await db.FloorBeams.Where(b => keys.Contains(b.JwProjectSubDataId))
            .GroupBy(b => b.JwProjectSubDataId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var pillars = await db.FloorPillars.Where(p => keys.Contains(p.JwProjectSubDataId))
            .GroupBy(p => p.JwProjectSubDataId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var noHole = await db.FloorBeams.Where(b => keys.Contains(b.JwProjectSubDataId) && !db.FloorHoles.Any(h => h.JwBeamDataId == b.Id))
            .GroupBy(b => b.JwProjectSubDataId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return keys.ToDictionary(k => k, k => new FloorCheck(beams.GetValueOrDefault(k), pillars.GetValueOrDefault(k), noHole.GetValueOrDefault(k)));
    }
}

/// <summary>梁番号などの自然順比較（B2 &lt; B10）。</summary>
internal sealed class NaturalComparer : IComparer<string?>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var c = long.Parse(x.AsSpan(si, Math.Min(i - si, 18))).CompareTo(long.Parse(y.AsSpan(sj, Math.Min(j - sj, 18))));
                if (c != 0) return c;
            }
            else
            {
                var c = x[i].CompareTo(y[j]);
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
