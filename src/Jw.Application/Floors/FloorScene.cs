using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using JwCore;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

/// <summary>図面プレビュー用の図形（図面座標。Y は上向き）。</summary>
public sealed record SceneRect(string Id, double X, double Y, double W, double H, string Kind, string Label, bool Warn);
public sealed record ScenePoint(double X, double Y, string Kind);
public sealed record SceneLine(double X1, double Y1, double X2, double Y2, string Kind);

public sealed record FloorSceneData(
    double MinX, double MinY, double MaxX, double MaxY, double Scale,
    IReadOnlyList<SceneRect> Rects, IReadOnlyList<ScenePoint> Points, IReadOnlyList<SceneLine> Lines);

/// <summary>階の図面プレビュー（梁・柱・穴・金物・ブレース）。描画はブラウザの SVG で行う。</summary>
public sealed class FloorSceneService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<FloorSceneData> GetAsync(Guid floorId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);
        var floor = await db.Floors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == floorId, ct) ?? throw new NotFoundException("階");
        var key = floor.Id.ToString();

        var beams = await db.FloorBeams.AsNoTracking().Where(b => b.JwProjectSubDataId == key).InOrder().ToListAsync(ct);
        var beamIds = beams.Select(b => b.Id).ToList();
        var holes = await db.FloorHoles.AsNoTracking().Where(h => beamIds.Contains(h.JwBeamDataId)).InOrder().ToListAsync(ct);
        var holeBeams = holes.Select(h => h.JwBeamDataId).ToHashSet();
        var pillars = await db.FloorPillars.AsNoTracking().Where(p => p.JwProjectSubDataId == key).InOrder().ToListAsync(ct);
        var links = await db.FloorLinkParts.AsNoTracking().Where(l => l.JwProjectSubDataId == key).InOrder().ToListAsync(ct);
        var lianjies = await db.FloorLianjies.AsNoTracking().Where(l => l.JwProjectSubDataId == key).InOrder().ToListAsync(ct);

        var rects = new List<SceneRect>();
        // 旧版 DataToJw：Location が左上、幅・高さは図面座標（Y は上向きなので下端は Y - 高さ）
        foreach (var b in beams.Where(b => b.Location is not null))
            rects.Add(new SceneRect(b.Id, b.Location!.X, b.Location.Y - b.Height, b.Width, b.Height, "beam", b.BeamCode, !holeBeams.Contains(b.Id)));
        foreach (var p in pillars)
        {
            if (p.BaseType == PillarBaseType.KPillar && p.FirstLocation is not null && p.LastLocation is not null)
            {
                AddBlock(rects, p.Id + "-a", p.FirstLocation.X, p.FirstLocation.Y, p.FirstWidth ?? 1, p.FirstHeight ?? 1, p.PillarCode);
                AddBlock(rects, p.Id + "-b", p.LastLocation.X, p.LastLocation.Y, p.LastWidth ?? 1, p.LastHeight ?? 1, "");
            }
            else if (p.Location is not null)
                AddBlock(rects, p.Id, p.Location.X, p.Location.Y, p.Width > 0 ? p.Width : 1, p.Height > 0 ? p.Height : 1, p.PillarCode);
        }

        var points = holes.Where(h => h.Location is not null).Select(h => new ScenePoint(h.Location!.X, h.Location.Y, "hole"))
            .Concat(links.Where(l => l.Location is not null).Select(l => new ScenePoint(l.Location!.X, l.Location.Y, l.GouJianType == GouJianType.BG ? "bg" : "b")))
            .ToList();
        var lines = lianjies.Where(l => l.Start is not null && l.End is not null)
            .Select(l => new SceneLine(l.Start.X, l.Start.Y, l.End.X, l.End.Y, "brace")).ToList();

        // 表示範囲：保存されている図面範囲。図形がそれより外にあれば広げる
        double minX = floor.LocationX, maxX = floor.LocationX + floor.Width, maxY = floor.LocationY, minY = floor.LocationY - floor.Height;
        foreach (var r in rects) { minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y); maxX = Math.Max(maxX, r.X + r.W); maxY = Math.Max(maxY, r.Y + r.H); }
        if (!(maxX > minX) || !(maxY > minY)) { minX = 0; minY = 0; maxX = 100; maxY = 100; }

        return new FloorSceneData(minX, minY, maxX, maxY, floor.Scale, rects, points, lines);
    }

    /// <summary>柱のブロック：保存位置を左上として描く（旧版 DataToJwBlock と同じ解釈）。</summary>
    private static void AddBlock(List<SceneRect> rects, string id, double x, double y, double w, double h, string label) =>
        rects.Add(new SceneRect(id, x, y - h, w, h, "pillar", label, false));
}
