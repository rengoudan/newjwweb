using System.IO.Compression;
using Jw.Application.Budgets;
using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Application.Floors;
using Jw.Application.Materials;
using Jw.Application.Projects;
using Jw.Cad.Export;
using Jw.Cad.Parsing;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using JwShapeCommon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Tests;

public class Phase4Tests
{
    private static readonly string[] Operator = Permissions.DefaultsFor("Operator").ToArray();

    private static ParseOptions Options2 => new()
    {
        BeamColor = 5, SplitColor = 10, PillarColor = 3, PillarPenStyle = 6, SplitPenStyle = 1, NearSplitMax = 5,
        BeamTextColor = 5, DownPillarColor = 3, BraceColor = 2, BraceOrigin = JwCore.LianjieParsingMethod.OnBeam,
        PillarRecognition = JwCore.PillarDrawingMethod.Shape, SplitRecognition = JwCore.PillarDrawingMethod.Shape,
    };

    private static async Task<(Guid project, Guid floor)> ImportAsync(IServiceProvider sp, string file = "fuzu1.jww", Guid? beamMaterial = null)
    {
        var customer = await sp.GetRequiredService<CustomerService>().SaveAsync(new CustomerInput { CompanyName = "テスト建設" });
        await sp.GetRequiredService<CustomerParseSettingService>().SaveAsync(new ParseSettingInput
        {
            CustomerId = customer, BeamColor = 5, SplitColor = 10, PillarColor = 3, PillarPenStyle = 6, SplitPenStyle = 1,
            NearSplitMax = 5, BeamTextColor = 5, DownPillarColor = 3, BraceColor = 2,
            PillarRecognition = ShapeRecognition.Shape, SplitRecognition = ShapeRecognition.Shape,
        });
        var project = await sp.GetRequiredService<ProjectService>().SaveAsync(new ProjectInput
        {
            Name = "A邸", SiteAddress = "東京", CustomerId = customer, FloorQuantity = 2,
        });
        var svc = sp.GetRequiredService<FloorImportService>();
        await using var stream = File.OpenRead(TestHost.TestData(file));
        var up = await svc.UploadAsync(stream, file);
        var jobId = await svc.StartAsync(new ImportInput
        {
            ProjectId = project, FileId = up.Id, FloorName = "1階", ExcludeDegenerateRectangles = false, BeamMaterialId = beamMaterial,
        });
        for (var i = 0; i < 600 && svc.GetJob(jobId).Status is ParseJobStatus.Queued or ParseJobStatus.Running; i++) await Task.Delay(100);
        return (project, await svc.SaveAsync(jobId));
    }

    private static List<string> JwwEntities(byte[] content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, content);
        var list = new List<string>();
        lock (JwwNative.Sync)
        {
            using var r = new JwwHelper.JwwReader();
            r.Read(path, rd => { foreach (var d in rd.DataList) list.Add(d.ToString()); });
        }
        File.Delete(path);
        return list;
    }

    [Theory]
    [InlineData(FloorExportKind.BeamJw)]
    [InlineData(FloorExportKind.TopPlan)]
    [InlineData(FloorExportKind.BottomPlan)]
    [InlineData(FloorExportKind.BeamCsv)]
    public async Task Export_from_database_equals_export_from_parse_result(FloorExportKind kind)
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var (_, floorId) = await ImportAsync(sp);

        // 期待値：解析結果をそのまま出力（保存・読み込みを経由しない）
        var parsed = FloorParser.Parse(TestHost.TestData("fuzu1.jww"), "1階", Options2);
        var expected = FloorExporter.Export(FloorDataBuilder.FromResult(parsed), kind,
            new ExportSettings { HoleDiameter = 17.5 }, Options2.ToSettings());

        var actual = await sp.GetRequiredService<ExportService>().ExportFloorAsync(floorId, kind);
        var actualFiles = actual.FileName.EndsWith(".zip")
            ? new ZipArchive(new MemoryStream(actual.Content)).Entries.Select(e =>
            {
                using var ms = new MemoryStream();
                e.Open().CopyTo(ms);
                return new ExportFile(e.Name, ms.ToArray());
            }).ToList()
            : [new ExportFile(actual.FileName, actual.Content)];

        Assert.Equal(expected.Select(f => f.FileName).Order(), actualFiles.Select(f => f.FileName).Order());
        foreach (var e in expected)
        {
            var a = actualFiles.Single(f => f.FileName == e.FileName);
            if (kind == FloorExportKind.BeamCsv) Assert.Equal(e.Content, a.Content);
            else Assert.Equal(JwwEntities(e.Content), JwwEntities(a.Content));
        }
    }

    [Fact]
    public async Task Csv_export_counts_and_is_audited_and_batch_zip_contains_all_floors_files()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var (projectId, floorId) = await ImportAsync(sp);
        var exports = sp.GetRequiredService<ExportService>();

        var csv = await exports.ExportFloorAsync(floorId, FloorExportKind.BeamCsv);
        Assert.Equal("1階-3015-2.csv", csv.FileName);
        Assert.Equal(1, Assert.Single(await sp.GetRequiredService<FloorService>().ListAsync(projectId)).ExportCount);

        var zip = await exports.ExportProjectAsync(projectId);
        var names = new ZipArchive(new MemoryStream(zip.Content)).Entries.Select(e => e.FullName).ToList();
        Assert.Contains("1階/1階 上F孔伏図.jww", names);
        Assert.Contains("1階/1階 下F孔伏図.jww", names);
        Assert.Contains("1階/1階-3015-2.csv", names);
        Assert.Contains(names, n => n.StartsWith("1階/梁JW/") && n.EndsWith(".jww"));

        await using var db = await host.RawDbAsync();
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "Export"));
    }

    [Fact]
    public async Task Export_settings_are_saved_and_change_csv_output()
    {
        await using var host = await TestHost.CreateAsync();
        await using var admin = await host.ScopeAsAdminAsync();
        var (_, floorId) = await ImportAsync(admin.ServiceProvider);
        var exports = admin.ServiceProvider.GetRequiredService<ExportService>();

        var before = await exports.ExportFloorAsync(floorId, FloorExportKind.BeamCsv);
        await exports.SaveSettingsAsync(new ExportSettingsInput { HoleDiameter = 22, CsvYAdjust = 3 });
        Assert.Equal(22, (await exports.GetSettingsAsync()).HoleDiameter);
        var after = await exports.ExportFloorAsync(floorId, FloorExportKind.BeamCsv);
        Assert.NotEqual(before.Content, after.Content);

        await using var op = host.ScopeAs("tanaka", Operator);
        await Assert.ThrowsAsync<ForbiddenException>(() => op.ServiceProvider.GetRequiredService<ExportService>()
            .SaveSettingsAsync(new ExportSettingsInput { HoleDiameter = 1 }));
    }

    [Fact]
    public async Task Budget_is_generated_from_parse_results_and_keeps_custom_lines()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var materials = sp.GetRequiredService<MaterialService>();
        var beamCat = await materials.SaveCategoryAsync(new CategoryInput { Name = "H形鋼", Kind = MaterialKind.Beam });
        var beamMat = await materials.SaveMaterialAsync(new MaterialInput { CategoryId = beamCat, Name = "H形鋼", Spec = "200x100", UnitName = "m", UnitPrice = 1000 });
        var hwCat = await materials.SaveCategoryAsync(new CategoryInput { Name = "金物", Kind = MaterialKind.Hardware });
        await materials.SaveMaterialAsync(new MaterialInput { CategoryId = hwCat, Name = "B金物", GeneralTitle = "B", UnitName = "個", UnitPrice = 300 });
        var (projectId, floorId) = await ImportAsync(sp, beamMaterial: beamMat);

        var budgets = sp.GetRequiredService<BudgetService>();
        var budgetId = await budgets.GenerateAsync(projectId);
        var detail = await budgets.GetAsync(budgetId);

        var wb = await sp.GetRequiredService<FloorService>().GetWorkbenchAsync(floorId);
        var beamLine = detail.Lines.Single(l => l.Kind == MaterialKind.Beam);
        Assert.Equal(BudgetLineSource.Auto, beamLine.Source);
        Assert.Equal(Math.Round((decimal)wb.Beams.Sum(b => b.Length) / 1000, 3), Math.Round(beamLine.Quantity, 3));
        var b = detail.Lines.Single(l => l.ItemName == "B金物");
        Assert.Equal(wb.LinkParts.Count(l => l.Type == JwCore.GouJianType.B), (int)b.Quantity);
        // BG の材料は未登録 → 識別失敗として残る（旧版はここで止まっていた）
        Assert.Contains(detail.Lines, l => l.Source == BudgetLineSource.AutoFailed && l.ItemName == "BG");

        // 手入力の明細は再作成しても残る
        await budgets.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, MaterialId = beamMat, Quantity = 1 });
        await budgets.GenerateAsync(projectId);
        var again = await budgets.GetAsync(budgetId);
        Assert.Single(again.Lines, l => l.Source == BudgetLineSource.Custom);
        Assert.Equal(detail.Lines.Count + 1, again.Lines.Count);
    }

    [Fact]
    public async Task Workbench_lists_needs_check_and_csv_pending_floors()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        await ImportAsync(sp);

        var s = await sp.GetRequiredService<WorkbenchService>().GetAsync();
        Assert.Equal(1, s.Floors);
        Assert.Equal(1, s.FloorsCsvPending);
        Assert.Equal(1, s.ProjectsInProgress); // 2 階のうち 1 階のみ取り込み
        Assert.Contains(s.Todos, t => t.Kind == TodoKind.CsvPending);
        Assert.Contains(s.Todos, t => t.Kind == TodoKind.ImportPending);
    }
}
