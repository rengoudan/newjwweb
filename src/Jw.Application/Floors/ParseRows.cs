using Jw.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

/// <summary>
/// 解析明細の並び順。出力（JW 図面・CSV）は解析時の並び順に依存するため、
/// 保存時に連番（シャドウプロパティ Seq）を振り、読み込み時はその順で返す。
/// 主キー（文字列 ID）順に任せると、SQL Server ではクラスター化インデックス順になり出力が変わる。
/// </summary>
public static class ParseRows
{
    public const string Seq = "Seq";

    public static void AddInOrder<T>(IAppDbContext db, DbSet<T> set, IEnumerable<T> rows) where T : class
    {
        var i = 0;
        foreach (var r in rows)
        {
            set.Add(r);
            db.Db.Entry(r).Property<int>(Seq).CurrentValue = i++;
        }
    }

    public static IQueryable<T> InOrder<T>(this IQueryable<T> q) where T : class =>
        q.OrderBy(x => EF.Property<int>(x, Seq));
}
