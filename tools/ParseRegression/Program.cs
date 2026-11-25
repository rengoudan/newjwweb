using System.Text;
using Jw.Cad.Parsing;
using JwCore;

// 用法:
//   ParseRegression run <jww> <out.txt> beam split pillar pillarStyle splitStyle near text down lianjie lianjieMethod pillarMethod splitMethod compType
//   ParseRegression diff <old.txt> <new.txt>
// 参数顺序与旧版基准工具一致（旧版基准由 D:\code\jw 的编译产物生成，见 README）。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

if (args[0] == "diff") return Diff(args[1], args[2]);
if (args[0] == "exportdiff") return ExportCompare.Diff(args[1], args[2]);

var a = args[1..];
int I(int i) => int.Parse(a[i]);
var options = new ParseOptions
{
    BeamColor = I(2), SplitColor = I(3), PillarColor = I(4), PillarPenStyle = I(5), SplitPenStyle = I(6),
    NearSplitMax = double.Parse(a[7]), BeamTextColor = I(8), DownPillarColor = I(9), BraceColor = I(10),
    BraceOrigin = (LianjieParsingMethod)I(11), PillarRecognition = (PillarDrawingMethod)I(12),
    SplitRecognition = (PillarDrawingMethod)I(13), BraceComponent = (LianjieCompentType)I(14),
    ExcludeDegenerateRectangles = a.Length > 15 && a[15] == "1",
};
if (args[0] == "exportjson") { ExportFromJson.Run(a[0], a[1], options); Console.WriteLine("exported"); return 0; }
if (args[0] == "export") { ExportCompare.Export(a[0], a[1], options); Console.WriteLine("exported"); return 0; }
var sw = System.Diagnostics.Stopwatch.StartNew();
string status;
var sets = new Dictionary<string, object>();
int beams = 0;
try
{
    var r = FloorParser.Parse(a[0], "F", options);
    sets["sub"] = r.Floor;
    sets["_beamdatas"] = r.Beams;
    sets["_holeDatas"] = r.Holes;
    sets["_jwbvdatas"] = r.BeamVerticals;
    sets["_beampillarDatas"] = r.Pillars;
    sets["_linkPartDatas"] = r.LinkParts;
    sets["_lianjieDatas"] = r.Lianjies;
    sets["_downPillarDatas"] = r.DownPillars;
    sets["_cuttingDatas"] = r.Cuttings;
    sets["_addMarkDatas"] = r.AddedHoleMarks;
    beams = r.Beams.Count;
    status = "OK";
}
catch (Exception ex)
{
    var inner = ex.InnerException ?? ex;
    status = "FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0] + " <= " + inner.GetType().Name
             + " @ " + string.Join(" / ", (inner.StackTrace ?? "").Split('\n').Take(6).Select(s => s.Trim()));
}
File.WriteAllText(a[1], "# " + status + "\n" + ParseDump.Dump(sets));
Console.WriteLine($"{status} beams={beams} {sw.ElapsedMilliseconds}ms");
return 0;

static int Diff(string oldPath, string newPath)
{
    var o = File.ReadAllLines(oldPath).Skip(1).ToList();
    var n = File.ReadAllLines(newPath).Skip(1).ToList();
    if (o.SequenceEqual(n)) { Console.WriteLine($"SAME ({o.Count} lines)"); return 0; }

    // 第二层：忽略梁番号（旧版梁番号后缀随并行顺序变化，见 JwFileHandle.Rectangles.cs），其余字段逐行比较
    static List<string> Norm(List<string> lines) => lines
        .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"(^|\|)(BeamCode|[A-Za-z]*BeamCode[A-Za-z]*|BeamName|Code)=[^|]*", "$1$2=*"))
        .OrderBy(l => l, StringComparer.Ordinal).ToList();
    var on = Norm(o);
    var nn = Norm(n);
    if (on.SequenceEqual(nn)) { Console.WriteLine($"SAME-EXCEPT-BEAMCODE ({o.Count} lines)"); return 0; }
    var onlyOld = o.Except(n).ToList();
    var onlyNew = n.Except(o).ToList();
    Console.WriteLine($"DIFF old={o.Count} new={n.Count} onlyOld={onlyOld.Count} onlyNew={onlyNew.Count}");
    foreach (var l in o.Where(l => l.StartsWith("## "))) Console.WriteLine("  old " + l);
    foreach (var l in n.Where(l => l.StartsWith("## "))) Console.WriteLine("  new " + l);
    foreach (var l in onlyOld.Take(3)) Console.WriteLine("  - " + (l.Length > 300 ? l[..300] : l));
    foreach (var l in onlyNew.Take(3)) Console.WriteLine("  + " + (l.Length > 300 ? l[..300] : l));
    return 1;
}
