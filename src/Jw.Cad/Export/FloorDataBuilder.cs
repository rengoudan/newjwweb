using JwCore;
using Jw.Cad.Parsing;

namespace Jw.Cad.Export;

/// <summary>
/// 把一层的各明细组装成旧版 JwProjectSubData（含子集合），供 DataToCanvas 还原画布后输出。
/// 旧版由 LoadSubCollectionAsync / LoadBeamCollectionAsync 从数据库加载出同样的结构。
/// </summary>
public static class FloorDataBuilder
{
    public static JwProjectSubData Build(
        JwProjectSubData floor,
        IEnumerable<JwBeamData> beams, IEnumerable<JwHoleData> holes, IEnumerable<JwBeamVerticalData> verticals,
        IEnumerable<JwPillarData> pillars, IEnumerable<JwLinkPartData> linkParts, IEnumerable<JwLianjieData> lianjies,
        IEnumerable<JwDownPillarData> downPillars, IEnumerable<JwCutting> cuttings, IEnumerable<JwAddedHoleMarkData> addedHoles)
    {
        var holesByBeam = holes.ToLookup(h => h.JwBeamDataId);
        var vertByBeam = verticals.ToLookup(v => v.JwBeamDataId);
        foreach (var b in beams)
        {
            b.JwHoles.Clear();
            b.JwHoles.AddRange(holesByBeam[b.Id]);
            b.JwBeamVerticalDatas.Clear();
            b.JwBeamVerticalDatas.AddRange(vertByBeam[b.Id]);
            floor.JwBeamDatas.Add(b);
        }
        floor.JwPillarDatas.AddRange(pillars);
        floor.JwLinkPartDatas.AddRange(linkParts);
        floor.JwLianjieDatas.AddRange(lianjies);
        floor.JwDownPillarDatas.AddRange(downPillars);
        floor.JwCuttings.AddRange(cuttings);
        floor.JwAddedHoles.AddRange(addedHoles);
        return floor;
    }

    public static JwProjectSubData FromResult(FloorParseResult r) => Build(r.Floor, r.Beams, r.Holes, r.BeamVerticals,
        r.Pillars, r.LinkParts, r.Lianjies, r.DownPillars, r.Cuttings, r.AddedHoleMarks);
}
