using System.Buffers.Binary;
using Jw.Application.Floors;
using Jw.Application.Materials;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using LegacyImport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Tests;

public class Phase5Tests
{
    private static readonly string[] Operator = Permissions.DefaultsFor("Operator").ToArray();

    // ---------------- 3D ----------------

    [Fact]
    public async Task Beam_3d_model_is_built_from_the_beams_processing_csv()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var floorId = await Phase4Import(sp);
        var wb = await sp.GetRequiredService<FloorService>().GetWorkbenchAsync(floorId);
        var beam = wb.Beams.Where(b => b.HasCsv).MaxBy(b => b.HoleCount)!;

        var m = await sp.GetRequiredService<ExportService>().GetBeam3DAsync(floorId, beam.Id);

        Assert.Null(m.Message);
        Assert.Equal(beam.BeamCode, m.BeamCode);
        Assert.Equal("H-200x100x5.5x8", m.SizeRaw);
        Assert.Equal((200, 100, 5.5, 8), (m.H, m.B, m.Tw, m.Tf));
        Assert.StartsWith("START", m.Csv);
        Assert.NotEmpty(m.Holes);
        // 2D 図と同じく、縦穴数 1 の CSV 行は対の 2 穴（ピッチ 56）で表示する
        Assert.Contains(m.Holes, h => h.IsPairPartner);
        Assert.All(m.Holes.Where(h => h.IsPairPartner), h =>
            Assert.Contains(m.Holes, o => !o.IsPairPartner && o.LineNumber == h.LineNumber && Math.Abs(h.Y - o.Y - 56) < 1e-9));
        Assert.All(m.Holes, h => Assert.InRange(h.X, 0, m.Length));
    }

    [Fact]
    public void Beam_3d_model_reports_non_h_sections_and_empty_csv()
    {
        Assert.NotNull(Beam3DModel.FromCsv("B1", null, 56).Message);
        var csv = "START\r\n0001,H-,B1,,,, [-100x50x5x7.5, 0.0, 1000, 1, 0, 0.0, 0.0\r\n0, 0, 0, , 0, 0\r\nEND\r\n";
        Assert.Contains("H形鋼のみ", Beam3DModel.FromCsv("B1", csv, 56).Message);
    }

    private static async Task<Guid> Phase4Import(IServiceProvider sp)
    {
        var customer = await sp.GetRequiredService<Jw.Application.Customers.CustomerService>().SaveAsync(new() { CompanyName = "テスト建設" });
        await sp.GetRequiredService<Jw.Application.Customers.CustomerParseSettingService>().SaveAsync(new()
        {
            CustomerId = customer, BeamColor = 5, SplitColor = 10, PillarColor = 3, PillarPenStyle = 6, SplitPenStyle = 1,
            NearSplitMax = 5, BeamTextColor = 5, DownPillarColor = 3, BraceColor = 2,
            PillarRecognition = ShapeRecognition.Shape, SplitRecognition = ShapeRecognition.Shape,
        });
        var project = await sp.GetRequiredService<Jw.Application.Projects.ProjectService>().SaveAsync(new()
        {
            Name = "A邸", SiteAddress = "東京", CustomerId = customer, FloorQuantity = 1,
        });
        var svc = sp.GetRequiredService<FloorImportService>();
        await using var stream = File.OpenRead(TestHost.TestData("fuzu1.jww"));
        var up = await svc.UploadAsync(stream, "fuzu1.jww");
        var jobId = await svc.StartAsync(new ImportInput { ProjectId = project, FileId = up.Id, FloorName = "1階" });
        for (var i = 0; i < 600 && svc.GetJob(jobId).Status is ParseJobStatus.Queued or ParseJobStatus.Running; i++) await Task.Delay(100);
        return await svc.SaveAsync(jobId);
    }

    // ---------------- 旧データ移行 ----------------

    [Fact]
    public void Spatialite_and_sql_server_points_are_decoded()
    {
        var p = LegacyGeometry.Decode(SpatiaLitePoint(-251.98, 100.5))!;
        Assert.Equal((-251.98, 100.5), (p.X, p.Y));

        var sql = new byte[22];
        BinaryPrimitives.WriteInt32LittleEndian(sql, 0); // SRID
        sql[4] = 1; sql[5] = 0x0C; // 版本 1、属性：有效 + 单点
        BinaryPrimitives.WriteDoubleLittleEndian(sql.AsSpan(6), 12.25);
        BinaryPrimitives.WriteDoubleLittleEndian(sql.AsSpan(14), -3.5);
        var q = LegacyGeometry.Decode(sql)!;
        Assert.Equal((12.25, -3.5), (q.X, q.Y));
    }

    [Fact]
    public async Task Legacy_database_is_imported_with_ids_times_and_parse_rows_intact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jw-legacy-{Guid.NewGuid():N}.db");
        CreateLegacyDb(path);
        try
        {
            await using var host = await TestHost.CreateAsync();
            await using var admin = await host.ScopeAsAdminAsync();
            var importer = ActivatorUtilities.CreateInstance<LegacyImporter>(admin.ServiceProvider);
            var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

            await using (var src = await LegacySource.OpenSqliteAsync(path))
            {
                var dry = await importer.ImportAsync(src, new ImportOptions { DryRun = true, SourceTimeZone = tokyo });
                Assert.Equal(2, dry.Counts["梁"]);
                await using (var check = await host.RawDbAsync()) Assert.False(await check.Projects.AnyAsync()); // 確認のみ

                var report = await importer.ImportAsync(src, new ImportOptions { SourceTimeZone = tokyo });
                Assert.Equal(1, report.Counts["工事"]);
                Assert.Equal(1, report.Counts["階"]);
                Assert.Equal(2, report.Counts["梁"]);
                Assert.Equal(1, report.Counts["穴"]);
                Assert.Contains(report.Warnings, w => w.Contains("所属する階・梁のないデータ 1 件")); // 孤立した穴
                Assert.Empty(await importer.VerifyAsync(src));

                // 2 回目は拒否（重複防止）
                await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(src, new ImportOptions()));
            }

            await using var db = await host.RawDbAsync();
            var floor = await db.Floors.SingleAsync();
            Assert.Equal(Guid.Parse("7533d266-80c0-443e-adde-140b224fee86"), floor.Id); // 旧版の GUID を維持
            Assert.Equal(new DateTime(2026, 11, 22, 5, 50, 18), floor.CreatedAt);        // JST 14:50:18 → UTC
            Assert.Equal("admin", floor.CreatedByName); // 実行したアカウント（CLI では「旧データ移行(Windows ユーザー)」）
            Assert.Equal((10.5, -20.25), (floor.LocationX, floor.LocationY));
            Assert.Contains("\"BeamColor\":5", floor.ParseOptionsJson);                   // 顧客の解析設定から

            var beams = await db.FloorBeams.InOrder().ToListAsync();
            Assert.Equal(["B2", "B1"], beams.Select(b => b.BeamCode));                    // 旧版の並び順（rowid 順）
            Assert.All(beams, b => Assert.Equal(floor.Id.ToString(), b.JwProjectSubDataId));
            Assert.Equal((1.5, 2.5), (beams[0].Location!.X, beams[0].Location!.Y));
            Assert.Equal("11111111-1111-1111-1111-111111111111", beams[0].BeamXHId);       // 材料 ID もそのまま

            var project = await db.Projects.Include(p => p.Customer).SingleAsync();
            Assert.Equal("テスト工事", project.Name);
            Assert.Equal("旧顧客", project.Customer!.CompanyName);
            Assert.Equal(1, project.ParsedQuantity);
            Assert.Equal(2, project.BeamCount);
            var material = await db.Materials.SingleAsync();
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), material.Id);
            Assert.Equal(1200m, material.UnitPrice);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "LegacyImport"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static byte[] SpatiaLitePoint(double x, double y)
    {
        var b = new byte[60];
        b[0] = 0x00; b[1] = 0x01;                                    // start, little endian
        for (var i = 0; i < 4; i++) BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(6 + i * 8), i % 2 == 0 ? x : y); // MBR
        b[38] = 0x7C;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(39), 1);   // POINT
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(43), x);
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(51), y);
        b[59] = 0xFE;
        return b;
    }

    /// <summary>旧版スキーマの一部（移行で読む列）だけを持つ小さな jwdata.db を作る。</summary>
    private static void CreateLegacyDb(string path)
    {
        using var c = new SqliteConnection($"Data Source={path}");
        c.Open();
        void Exec(string sql, params (string, object?)[] ps)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        Exec("""
            CREATE TABLE JwCustomerDatas (Id INTEGER PRIMARY KEY AUTOINCREMENT, CompanyName TEXT, CompanyAddress TEXT, Contact TEXT, Telephone TEXT, CreationTime TEXT);
            CREATE TABLE JwCustDesignConstDatas (Id INTEGER PRIMARY KEY AUTOINCREMENT, PickPrecision INTEGER, JwJianxi REAL, JwScale REAL,
                BeamParseColorNumber INTEGER, BeamSplitParseColor INTEGER, BeamPillarParseColor INTEGER, PillarPenStyle INTEGER, NearSpliteMax REAL,
                SplitPenStyle INTEGER, BeamSymbolTextColorNumber INTEGER, JwCustomerDataId INTEGER, CreationTime TEXT, DownPillarColorNumber INTEGER,
                LianjieColorNumber INTEGER, LianjieParsingMethod INTEGER, PillarDrawingMethod INTEGER, SplitDrawingMethod INTEGER, LianjieCompentType INTEGER);
            CREATE TABLE JwMaterialTypeDatas (Id TEXT PRIMARY KEY, MaterialTypeName TEXT, DefaultDataId TEXT, MaterialCount INTEGER, MaterialType INTEGER, CreationTime TEXT);
            CREATE TABLE JwMaterialDatas (Id TEXT PRIMARY KEY, MaterialName TEXT, GeneralTitle TEXT, MaterialParameter TEXT, UnitPrice TEXT, UnitName TEXT,
                MaterialType INTEGER, Remark TEXT, JwMaterialTypeDataId TEXT, CreationTime TEXT, NumParameter REAL);
            CREATE TABLE JwProjectMainDatas (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProjectName TEXT, Biaochi TEXT, FloorQuantity INTEGER, ParsedQuantity INTEGER,
                ExportCount INTEGER, JwCustomerDataId INTEGER, CreationTime TEXT, ProjectStatus INTEGER, SiteAddress TEXT);
            CREATE TABLE JwProjectSubDatas (Id TEXT PRIMARY KEY, FloorName TEXT, BeamCount INTEGER, HorizontalBeamsCount INTEGER, VerticalBeamsCount INTEGER,
                Biaochi TEXT, PillarCount INTEGER, KPillarCount INTEGER, SinglePillarCount INTEGER, BCount INTEGER, BGCount INTEGER, ExportCount INTEGER,
                JwProjectMainDataId INTEGER, CreationTime TEXT, Width REAL, Height REAL, Scale REAL, DirectionType INTEGER, Location POINT, MarkBeam INTEGER,
                KPillarType INTEGER, LianjieCompentType INTEGER, MinimalStrategy INTEGER);
            CREATE TABLE JwBeamDatas (Id TEXT PRIMARY KEY, BeamCode TEXT, FloorName TEXT, Length REAL, Location POINT, Width REAL, Height REAL, Scale REAL,
                DirectionType INTEGER, HasCsv INTEGER, BeamXHId TEXT, BeamXHName TEXT, JwProjectSubDataId TEXT);
            CREATE TABLE JwHoleDatas (Id TEXT PRIMARY KEY, HasTop INTEGER, HasCenter INTEGER, HasBottom INTEGER, KongNum INTEGER, Location POINT, JwBeamDataId TEXT);
            """);
        Exec("INSERT INTO JwCustomerDatas VALUES (1, '旧顧客', '大阪', '山田', '06-0000', '2026-05-01 10:00:00')");
        Exec("INSERT INTO JwCustDesignConstDatas VALUES (1, 6, 120, 100, 5, 10, 3, 6, 5, 1, 5, 1, '2026-05-01 10:00:00', 3, 2, 0, 1, 1, 0)");
        Exec("INSERT INTO JwMaterialTypeDatas VALUES ('22222222-2222-2222-2222-222222222222', 'H形鋼', '11111111-1111-1111-1111-111111111111', 1, 0, '2026-05-01 10:00:00')");
        Exec("INSERT INTO JwMaterialDatas VALUES ('11111111-1111-1111-1111-111111111111', 'H形鋼', 'H', '200x100', '1200.0', 'm', 0, '', '22222222-2222-2222-2222-222222222222', '2026-05-01 10:00:00', 0)");
        Exec("INSERT INTO JwProjectMainDatas VALUES (1, 'テスト工事', '1/100', 2, 1, 0, 1, '2026-11-01 09:00:00', 1, '東京')");
        Exec("INSERT INTO JwProjectSubDatas VALUES ('7533D266-80C0-443E-ADDE-140B224FEE86', '2FC1', 2, 2, 0, '1/100', 0, 0, 0, 0, 0, 1, 1, '2026-11-22 14:50:18', 300, 200, 100, 0, @loc, NULL, 0, 0, 0)",
            ("@loc", SpatiaLitePoint(10.5, -20.25)));
        // rowid 順：B2 → B1（ID の順とは逆）
        Exec("INSERT INTO JwBeamDatas VALUES ('b-2', 'B2', '2FC1', 1500, @loc, 10, 1, 100, 0, 1, '11111111-1111-1111-1111-111111111111', 'H', '7533D266-80C0-443E-ADDE-140B224FEE86')",
            ("@loc", SpatiaLitePoint(1.5, 2.5)));
        Exec("INSERT INTO JwBeamDatas VALUES ('b-1', 'B1', '2FC1', 2500, @loc, 20, 1, 100, 0, 1, '11111111-1111-1111-1111-111111111111', 'H', '7533D266-80C0-443E-ADDE-140B224FEE86')",
            ("@loc", SpatiaLitePoint(3, 4)));
        Exec("INSERT INTO JwHoleDatas VALUES ('h-1', 1, 0, 0, 2, @loc, 'b-1')", ("@loc", SpatiaLitePoint(3.2, 4)));
        Exec("INSERT INTO JwHoleDatas VALUES ('h-x', 1, 0, 0, 2, NULL, 'no-such-beam')");
    }
}
