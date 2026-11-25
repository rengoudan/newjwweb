using Jw.Cad.Export;
using Jw.Cad.Parsing;
using JwShapeCommon;

/// <summary>输出回归：新版输出与旧版基准（同名文件）逐一比较。JWW 读回后逐实体比较，CSV 按字节比较。</summary>
static class ExportCompare
{
    public static void Export(string jww, string outDir, ParseOptions options)
    {
        Directory.CreateDirectory(outDir);
        var r = FloorParser.Parse(jww, "F", options);
        var floor = FloorDataBuilder.FromResult(r);
        foreach (FloorExportKind kind in Enum.GetValues<FloorExportKind>())
        {
            if (kind == FloorExportKind.BracePlan && floor.JwLianjieDatas.Count == 0) continue;
            try
            {
                foreach (var f in FloorExporter.Export(floor, kind, ExportSettings.Default, options.ToSettings()))
                {
                    var name = kind switch
                    {
                        FloorExportKind.BeamJw => "beam_" + f.FileName,
                        FloorExportKind.TopPlan => "top.jww",
                        FloorExportKind.BottomPlan => "bottom.jww",
                        FloorExportKind.BracePlan => "brace.jww",
                        _ => "beams.csv",
                    };
                    File.WriteAllBytes(Path.Combine(outDir, name), f.Content);
                }
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(outDir, kind + ".error"), ex.ToString()); }
        }
    }

    public static int Diff(string oldDir, string newDir)
    {
        var names = Directory.GetFiles(oldDir).Select(Path.GetFileName).Union(Directory.GetFiles(newDir).Select(Path.GetFileName)).Order().ToList();
        int bad = 0;
        foreach (var n in names)
        {
            var o = Path.Combine(oldDir, n!);
            var nw = Path.Combine(newDir, n!);
            string result;
            if (!File.Exists(o)) result = "ONLY-NEW";
            else if (!File.Exists(nw)) result = "ONLY-OLD";
            else if (n!.EndsWith(".csv"))
            {
                var a = File.ReadAllBytes(o);
                var b = File.ReadAllBytes(nw);
                result = a.SequenceEqual(b) ? "SAME"
                    : CsvBlocks(a).SequenceEqual(CsvBlocks(b)) ? "SAME-UNORDERED（梁の順序・連番のみ異なる）" : "DIFF";
            }
            else if (n.EndsWith(".jww"))
            {
                var a = Entities(o);
                var b = Entities(nw);
                result = a.SequenceEqual(b) ? $"SAME ({a.Count})"
                    : a.Order(StringComparer.Ordinal).SequenceEqual(b.Order(StringComparer.Ordinal)) ? $"SAME-UNORDERED ({a.Count}、図形の順序のみ異なる)"
                    : $"DIFF old={a.Count} new={b.Count} first={FirstDiff(a, b)}";
            }
            else result = "?";
            if (!result.StartsWith("SAME")) bad++;
            Console.WriteLine($"  {n}: {result}");
        }
        Console.WriteLine(bad == 0 ? "ALL SAME" : $"DIFFERENT {bad}");
        return bad;
    }

    private static List<string> Entities(string path)
    {
        var list = new List<string>();
        lock (JwwNative.Sync)
        {
            using var reader = new JwwHelper.JwwReader();
            reader.Read(path, rd =>
            {
                foreach (var d in rd.DataList) list.Add(d.ToString());
                foreach (var b in rd.DataListList) list.Add("BLOCKDEF " + b.ToString());
            });
        }
        return list;
    }

    /// <summary>CSV を梁ごとのブロック（梁の行＋その穴の行）に分け、先頭の連番を除いて並べ替える。</summary>
    private static List<string> CsvBlocks(byte[] bytes)
    {
        var text = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(932)!.GetString(bytes);
        var blocks = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d{4},"))
            {
                if (current.Length > 0) blocks.Add(current.ToString());
                current.Clear().Append(l[4..]);
            }
            else current.Append('\n').Append(l);
        }
        if (current.Length > 0) blocks.Add(current.ToString());
        return blocks.Order(StringComparer.Ordinal).ToList();
    }

    private static string FirstDiff(List<string> a, List<string> b)
    {
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            if (a[i] != b[i]) return $"#{i}: {a[i]} <> {b[i]}";
        return "length";
    }
}

/// <summary>用旧版解析出的同一份数据（JSON）执行新版输出，排除解析阶段随机性的影响，只验证输出逻辑。</summary>
static class ExportFromJson
{
    public static void Run(string jsonPath, string outDir, Jw.Cad.Parsing.ParseOptions options)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
        T One<T>(string k) => System.Text.Json.JsonSerializer.Deserialize<T>(doc.RootElement.GetProperty(k), DataJson.Options)!;
        var floor = Jw.Cad.Export.FloorDataBuilder.Build(
            One<JwCore.JwProjectSubData>("sub"), One<List<JwCore.JwBeamData>>("beams"), One<List<JwCore.JwHoleData>>("holes"),
            One<List<JwCore.JwBeamVerticalData>>("verts"), One<List<JwCore.JwPillarData>>("pillars"), One<List<JwCore.JwLinkPartData>>("links"),
            One<List<JwCore.JwLianjieData>>("lianjies"), One<List<JwCore.JwDownPillarData>>("downs"), One<List<JwCore.JwCutting>>("cuts"),
            One<List<JwCore.JwAddedHoleMarkData>>("adds"));
        Directory.CreateDirectory(outDir);
        foreach (Jw.Cad.Export.FloorExportKind kind in Enum.GetValues<Jw.Cad.Export.FloorExportKind>())
        {
            if (kind == Jw.Cad.Export.FloorExportKind.BracePlan && floor.JwLianjieDatas.Count == 0) continue;
            try
            {
                foreach (var f in Jw.Cad.Export.FloorExporter.Export(floor, kind, JwShapeCommon.ExportSettings.Default, options.ToSettings()))
                {
                    var name = kind switch
                    {
                        Jw.Cad.Export.FloorExportKind.BeamJw => "beam_" + f.FileName,
                        Jw.Cad.Export.FloorExportKind.TopPlan => "top.jww",
                        Jw.Cad.Export.FloorExportKind.BottomPlan => "bottom.jww",
                        Jw.Cad.Export.FloorExportKind.BracePlan => "brace.jww",
                        _ => "beams.csv",
                    };
                    File.WriteAllBytes(Path.Combine(outDir, name), f.Content);
                }
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(outDir, kind + ".error"), ex.ToString()); }
        }
    }
}
