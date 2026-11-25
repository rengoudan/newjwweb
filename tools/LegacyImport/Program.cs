using System.Security.Claims;
using Jw.Application;
using Jw.Application.Abstractions;
using Jw.Domain.Authorization;
using Jw.Infrastructure;
using LegacyImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 旧版データ移行：
//   dotnet run --project tools/LegacyImport -- --sqlite <jwdata.db> --target "<新DB接続文字列>" [--dry-run] [--timezone "Tokyo Standard Time"]
//   dotnet run --project tools/LegacyImport -- --sqlserver "<旧DB接続文字列>" --target "<新DB接続文字列>"
// 新 DB は先に一度サイトを起動してマイグレーション済みにしておくこと。顧客・工事が 1 件でもあると中止する。
Console.OutputEncoding = System.Text.Encoding.UTF8;
var a = Args.Parse(args);
if (a is null)
{
    Console.WriteLine("""
        使い方:
          LegacyImport --sqlite <jwdata.db のパス> --target <新 DB の接続文字列> [--dry-run] [--no-verify] [--timezone <旧データの時刻のタイムゾーン>]
          LegacyImport --sqlserver <旧 DB の接続文字列> --target <新 DB の接続文字列> [--dry-run]
        """);
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(l => l.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
services.AddApplication();
services.AddInfrastructure(o => o.UseSqlServer(a.Target));
services.AddScoped<LegacyImporter>();
await using var sp = services.BuildServiceProvider();

await using (var probe = sp.CreateAsyncScope())
{
    var db = probe.ServiceProvider.GetRequiredService<IAppDbContext>().Db;
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Console.Error.WriteLine($"新 DB のマイグレーションが未適用です（{pending.Count} 件）。先にサイトを一度起動してください。");
        return 1;
    }
}

await using var scope = sp.CreateAsyncScope();
// 監査ログ上の操作者：「旧データ移行」（実行した Windows ユーザー名を併記）
scope.ServiceProvider.GetRequiredService<CurrentUserState>().Set(new ClaimsPrincipal(new ClaimsIdentity(
[
    new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
    new Claim(ClaimTypes.Name, $"旧データ移行({Environment.UserName})"),
    .. Permissions.All.Select(p => new Claim(Permissions.ClaimType, p)),
], "legacy-import")), Environment.MachineName);

await using var src = a.Sqlite is not null
    ? await LegacySource.OpenSqliteAsync(a.Sqlite)
    : await LegacySource.OpenSqlServerAsync(a.SqlServer!);
Console.WriteLine($"移行元：{src.Description}{(a.DryRun ? "（確認のみ・書き込みなし）" : "")}");

try
{
    var report = await scope.ServiceProvider.GetRequiredService<LegacyImporter>()
        .ImportAsync(src, new ImportOptions { DryRun = a.DryRun, SourceTimeZone = a.TimeZone });
    foreach (var (k, v) in report.Counts) Console.WriteLine($"  {k,-8} {v,6}");
    foreach (var w in report.Warnings) Console.WriteLine($"  ⚠ {w}");
    if (a.Verify && !a.DryRun)
    {
        var problems = await scope.ServiceProvider.GetRequiredService<LegacyImporter>().VerifyAsync(src);
        Console.WriteLine(problems.Count == 0 ? "照合：解析明細は旧 DB と全項目一致しました。" : $"照合：不一致 {problems.Count} 件");
        foreach (var p in problems.Take(50)) Console.WriteLine("  ✗ " + p);
        if (problems.Count > 0) return 1;
    }
    Console.WriteLine(a.DryRun ? "確認のみ完了（書き込みなし）。" : "移行が完了しました。");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("移行に失敗しました（新 DB は変更されていません）：" + ex.Message);
    return 1;
}

internal sealed record Args(string? Sqlite, string? SqlServer, string Target, bool DryRun, bool Verify, TimeZoneInfo TimeZone)
{
    public static Args? Parse(string[] args)
    {
        string? Get(string name) => args.SkipWhile(x => x != name).Skip(1).FirstOrDefault();
        var sqlite = Get("--sqlite");
        var sqlServer = Get("--sqlserver");
        var target = Get("--target");
        if (target is null || (sqlite is null) == (sqlServer is null)) return null;
        var tz = Get("--timezone") is { } id ? TimeZoneInfo.FindSystemTimeZoneById(id) : TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        return new Args(sqlite, sqlServer, target, args.Contains("--dry-run"), !args.Contains("--no-verify"), tz);
    }
}
