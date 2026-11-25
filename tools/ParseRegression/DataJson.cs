using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Point = NetTopologySuite.Geometries.Point;

/// <summary>解析数据 ⇄ JSON（新旧两端共用）。几何点存为 "X,Y"，导航属性不序列化。</summary>
public static class DataJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        o.Converters.Add(new PointConverter());
        o.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                ti =>
                {
                    foreach (var p in ti.Properties.ToList())
                    {
                        var t = p.PropertyType;
                        bool nav = t.Namespace == "JwCore" && t.IsClass && !t.IsEnum;
                        bool coll = t != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(t);
                        if (nav || coll) ti.Properties.Remove(p);
                    }
                },
            },
        };
        return o;
    }

    private sealed class PointConverter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var s = r.GetString();
            if (string.IsNullOrEmpty(s)) return null;
            var i = s.IndexOf(',');
            return new Point(double.Parse(s[..i], CultureInfo.InvariantCulture), double.Parse(s[(i + 1)..], CultureInfo.InvariantCulture));
        }

        public override void Write(Utf8JsonWriter w, Point v, JsonSerializerOptions o) =>
            w.WriteStringValue(v.X.ToString("R", CultureInfo.InvariantCulture) + "," + v.Y.ToString("R", CultureInfo.InvariantCulture));
    }
}
