using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace Jw.Infrastructure.Persistence;

/// <summary>NetTopologySuite 的点 ⇄ "X,Y" 文本（round-trip 格式，精度不损失）。</summary>
public sealed class PointTextConverter() : ValueConverter<Point, string>(
    p => Format(p),
    s => Parse(s))
{
    private static string Format(Point p) =>
        p.X.ToString("R", CultureInfo.InvariantCulture) + "," + p.Y.ToString("R", CultureInfo.InvariantCulture);

    private static Point Parse(string s)
    {
        var i = s.IndexOf(',');
        return new Point(double.Parse(s.AsSpan(0, i), CultureInfo.InvariantCulture),
                         double.Parse(s.AsSpan(i + 1), CultureInfo.InvariantCulture));
    }
}
