using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

/// <summary>
/// 把解析结果（CreateData 产出的实体）扁平化成可比较的文本：
/// 只取公共标量属性；Id/外键（随机 Guid）与时间不比较；浮点数保留 6 位；几何对象用 ToString（WKT）。
/// 每个集合内的行按文本排序，消除随机顺序的影响。新旧两端共用这一份代码。
/// </summary>
public static class ParseDump
{
    public static string Dump(IDictionary<string, object> sets)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in sets.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var rows = new List<string>();
            if (value is IEnumerable e && value is not string)
                foreach (var item in e) rows.Add(Row(item));
            else if (value is not null)
                rows.Add(Row(value));
            rows.Sort(StringComparer.Ordinal);
            sb.Append("## ").Append(name).Append(" (").Append(rows.Count).AppendLine(")");
            foreach (var r in rows) sb.AppendLine(r);
        }
        return sb.ToString();
    }

    private static string Row(object o)
    {
        var parts = new List<string>();
        foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetIndexParameters().Length == 0)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (p.Name == "Id" || p.Name.EndsWith("Id", StringComparison.Ordinal) || p.Name.EndsWith("Ids", StringComparison.Ordinal)
                || p.Name is "CreationTime" or "BeamSignature") continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!(t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t.Namespace?.StartsWith("NetTopologySuite") == true)) continue;
            object v;
            try { v = p.GetValue(o); } catch { v = "<err>"; }
            parts.Add(p.Name + "=" + Fmt(v));
        }
        return string.Join("|", parts);
    }

    private static string Fmt(object v) => v switch
    {
        null => "null",
        double d => Math.Round(d, 6).ToString("R", CultureInfo.InvariantCulture),
        float f => Math.Round(f, 6).ToString("R", CultureInfo.InvariantCulture),
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString()!,
    };
}
