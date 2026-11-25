using System.Buffers.Binary;
using NetTopologySuite.Geometries;

namespace LegacyImport;

/// <summary>
/// 旧库里的几何列（全部是点）。不依赖 SpatiaLite / Microsoft.SqlServer.Types，直接解码二进制：
/// - SQLite（SpatiaLite BLOB）：00 | 字节序 | SRID(4) | MBR(32) | 7C | 类型(4)=1 | X(8) | Y(8) | FE
/// - SQL Server（geometry 序列化格式）：SRID(4) | 版本(1) | 属性(1，0x08=单点) | X(8) | Y(8)
/// </summary>
public static class LegacyGeometry
{
    public static Point? Decode(byte[]? blob)
    {
        if (blob is null || blob.Length == 0) return null;
        return IsSpatiaLite(blob) ? SpatiaLite(blob) : SqlServer(blob);
    }

    public static bool IsSpatiaLite(byte[] b) => b.Length >= 60 && b[0] == 0x00 && b[38] == 0x7C && b[^1] == 0xFE;

    private static Point SpatiaLite(byte[] b)
    {
        var little = b[1] == 0x01;
        var type = ReadInt(b, 39, little);
        if (type % 1000 != 1) throw new FormatException($"点以外の SpatiaLite ジオメトリ（type={type}）には対応していません");
        return new Point(ReadDouble(b, 43, little), ReadDouble(b, 51, little));
    }

    private static Point SqlServer(byte[] b)
    {
        if (b.Length < 22) throw new FormatException("SQL Server geometry として短すぎます");
        if ((b[5] & 0x08) == 0) throw new FormatException("単一点以外の SQL Server geometry には対応していません");
        return new Point(BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(6)), BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(14)));
    }

    private static int ReadInt(byte[] b, int at, bool little) =>
        little ? BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)) : BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(at));

    private static double ReadDouble(byte[] b, int at, bool little) =>
        little ? BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(at)) : BinaryPrimitives.ReadDoubleBigEndian(b.AsSpan(at));
}
