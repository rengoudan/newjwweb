using System.Text;
using JwwHelper;

// 用法: JwwSmokeTest <样例.jww>...
// 对每个文件：读取 -> 统计实体 -> 用原文件做模板头写出全部实体（输出放到含日文的目录）-> 再读回比对数量
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var outRoot = Path.Combine(Path.GetTempPath(), "jww冒烟テスト");
Directory.CreateDirectory(outRoot);
int failed = 0;

foreach (var src in args)
{
    Console.WriteLine($"== {src}");
    try
    {
        var first = Read(src);
        Console.WriteLine($"   scale 1/{first.Scale}  blocks(def)={first.BlockDefs}  {Fmt(first.Counts)}");

        var outPath = Path.Combine(outRoot, Path.GetFileNameWithoutExtension(src) + "_再出力.jww");
        using (var w = new JwwWriter())
        {
            w.InitHeader(src);
            foreach (var d in first.Data) w.AddData(d);
            w.Write(outPath);
        }
        var second = Read(outPath);
        Console.WriteLine($"   写出 -> {outPath}");
        Console.WriteLine($"   读回 {Fmt(second.Counts)}");

        // 写出时只回写主数据列表（块定义不回写），故只比对 Sen/Moji/Solid 等非块实体
        var keys = first.Counts.Keys.Where(k => k != "JwwBlock");
        var diff = keys.Where(k => first.Counts[k] != second.Counts.GetValueOrDefault(k)).ToList();
        // 逐实体比对内容（坐标、文字、颜色等，取自各实体的 ToString）
        var a = first.Data.Where(d => d is not JwwBlock).Select(d => d.ToString()).ToList();
        var b = second.Data.Where(d => d is not JwwBlock).Select(d => d.ToString()).ToList();
        int mismatch = a.Zip(b).Count(p => p.First != p.Second);
        Console.WriteLine($"   内容不一致实体数: {mismatch}");
        if (mismatch > 0) diff.Add("content");
        if (diff.Count == 0) Console.WriteLine("   OK");
        else { failed++; Console.WriteLine("   NG 差异: " + string.Join(",", diff)); }
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine("   NG 异常: " + ex);
    }
}
Console.WriteLine(failed == 0 ? "ALL OK" : $"FAILED {failed}");
return failed == 0 ? 0 : 1;

static Result Read(string path)
{
    Result? r = null;
    using var reader = new JwwReader();
    reader.Read(path, rd =>
    {
        var data = new List<JwwData>();
        foreach (var d in rd.DataList) data.Add(d);
        r = new Result(
            rd.Header.m_adScale[0],
            rd.DataListList.Count,
            data,
            data.GroupBy(d => d.GetType().Name).ToDictionary(g => g.Key, g => g.Count()));
    });
    return r ?? throw new InvalidOperationException("Read callback not invoked");
}

static string Fmt(Dictionary<string, int> c) => string.Join(" ", c.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));

record Result(double Scale, int BlockDefs, List<JwwData> Data, Dictionary<string, int> Counts);
