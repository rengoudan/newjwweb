using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;

namespace LegacyImport;

/// <summary>一行旧数据：列名（不区分大小写）→ 值（几何列已解码为 Point）。</summary>
public sealed class LegacyRow(Dictionary<string, object?> values)
{
    public bool Has(string column) => values.ContainsKey(column);
    public object? this[string column] => values.TryGetValue(column, out var v) ? v : null;

    public string Str(string c) => this[c]?.ToString() ?? "";
    public string? StrOrNull(string c) => this[c]?.ToString() is { Length: > 0 } s ? s : null;
    public long Long(string c) => this[c] is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;
    public int Int(string c) => (int)Long(c);
    public double Double(string c) => this[c] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;
    public decimal Decimal(string c) => this[c] switch
    {
        null => 0,
        string s => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0,
        var v => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
    };
    public DateTime? Time(string c) => this[c] switch
    {
        DateTime d => d,
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) => d,
        _ => null,
    };
}

/// <summary>旧版数据库（SQLite jwdata.db 或 SQL Server）。只读。</summary>
public sealed class LegacySource : IAsyncDisposable
{
    private readonly DbConnection _conn;
    private readonly bool _sqlite;

    private LegacySource(DbConnection conn, bool sqlite) { _conn = conn; _sqlite = sqlite; }

    public static async Task<LegacySource> OpenSqliteAsync(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("旧版データベースが見つかりません", path);
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await conn.OpenAsync();
        return new LegacySource(conn, true);
    }

    public static async Task<LegacySource> OpenSqlServerAsync(string connectionString)
    {
        var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        return new LegacySource(conn, false);
    }

    public string Description => _sqlite ? $"SQLite {_conn.DataSource}" : $"SQL Server {_conn.DataSource}/{_conn.Database}";

    public async Task<bool> TableExistsAsync(string table)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = _sqlite
            ? "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@t"
            : "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@t";
        var p = cmd.CreateParameter();
        p.ParameterName = "@t";
        p.Value = table;
        cmd.Parameters.Add(p);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>
    /// 读出整张表。顺序与旧版读取时相同：SQLite 按插入顺序（rowid），SQL Server 按主键（聚集索引）。
    /// 解析明细的顺序会影响导出结果，所以要保持。表不存在（旧版本的库）时返回空。
    /// </summary>
    public async Task<List<LegacyRow>> ReadAsync(string table)
    {
        var rows = new List<LegacyRow>();
        if (!await TableExistsAsync(table)) return rows;
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = _sqlite ? $"SELECT * FROM \"{table}\" ORDER BY rowid" : $"SELECT * FROM [{table}] ORDER BY [Id]";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++)
            {
                var name = r.GetName(i);
                if (r.IsDBNull(i)) { values[name] = null; continue; }
                var typeName = r.GetDataTypeName(i);
                if (r is SqlDataReader sql && (typeName.EndsWith("geometry", StringComparison.OrdinalIgnoreCase)
                                               || typeName.EndsWith("geography", StringComparison.OrdinalIgnoreCase)))
                {
                    values[name] = LegacyGeometry.Decode(sql.GetSqlBytes(i).Value);
                    continue;
                }
                var v = r.GetValue(i);
                // SQLite の POINT 列は SpatiaLite の BLOB
                values[name] = v is byte[] blob && LegacyGeometry.IsSpatiaLite(blob) ? LegacyGeometry.Decode(blob) : v;
            }
            rows.Add(new LegacyRow(values));
        }
        return rows;
    }

    /// <summary>
    /// 把一行映射到同名属性的对象（解析明细沿用旧版的数据类，列名与属性名一致）。
    /// 只设置标量属性；导航属性和集合保持默认。
    /// </summary>
    public static T Materialize<T>(LegacyRow row) where T : new()
    {
        var obj = new T();
        foreach (var p in Props<T>.Scalars)
        {
            if (row[p.Name] is not { } v) continue;
            p.SetValue(obj, ConvertTo(v, p.PropertyType));
        }
        return obj;
    }

    private static object? ConvertTo(object v, Type target)
    {
        var t = Nullable.GetUnderlyingType(target) ?? target;
        if (t.IsInstanceOfType(v)) return v;
        if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt64(v, CultureInfo.InvariantCulture));
        if (t == typeof(bool)) return v is string s ? s is "1" or "True" or "true" : Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0;
        if (t == typeof(Guid)) return Guid.Parse(v.ToString()!);
        if (t == typeof(DateTime)) return v is string ds ? DateTime.Parse(ds, CultureInfo.InvariantCulture) : Convert.ToDateTime(v, CultureInfo.InvariantCulture);
        if (t == typeof(decimal) && v is string dec) return decimal.Parse(dec, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (t == typeof(string)) return v.ToString();
        return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
    }

    private static class Props<T>
    {
        public static readonly PropertyInfo[] Scalars = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && IsScalar(p.PropertyType))
            .ToArray();

        private static bool IsScalar(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
                   || t == typeof(Guid) || t == typeof(Point);
        }
    }

    public async ValueTask DisposeAsync() => await _conn.DisposeAsync();
}
