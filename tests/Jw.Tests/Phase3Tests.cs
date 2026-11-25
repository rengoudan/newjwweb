using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Application.Floors;
using Jw.Application.Projects;
using Jw.Cad.Parsing;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using JwCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectStatus = Jw.Domain.Common.ProjectStatus;

namespace Jw.Tests;

public class Phase3Tests
{
    private static readonly string[] Operator = Permissions.DefaultsFor("Operator").ToArray();

    /// <summary>旧版数据库里顧客 2 的解析設定（1伏図・2伏図 用）。</summary>
    private static ParseSettingInput Setting2(Guid customerId) => new()
    {
        CustomerId = customerId, BeamColor = 5, SplitColor = 10, PillarColor = 3, PillarPenStyle = 6, SplitPenStyle = 1,
        NearSplitMax = 5, BeamTextColor = 5, DownPillarColor = 3, BraceColor = 2,
        BraceOrigin = BraceOrigin.BeamEnd, PillarRecognition = ShapeRecognition.Shape, SplitRecognition = ShapeRecognition.Shape,
        BraceComponent = BraceComponent.BR8,
    };

    private static ParseOptions Options2 => new()
    {
        BeamColor = 5, SplitColor = 10, PillarColor = 3, PillarPenStyle = 6, SplitPenStyle = 1, NearSplitMax = 5,
        BeamTextColor = 5, DownPillarColor = 3, BraceColor = 2, BraceOrigin = LianjieParsingMethod.OnBeam,
        PillarRecognition = PillarDrawingMethod.Shape, SplitRecognition = PillarDrawingMethod.Shape,
    };

    private static async Task<Guid> SeedProjectAsync(IServiceProvider sp, int floors = 2)
    {
        var customer = await sp.GetRequiredService<CustomerService>().SaveAsync(new CustomerInput { CompanyName = "テスト建設" });
        await sp.GetRequiredService<CustomerParseSettingService>().SaveAsync(Setting2(customer));
        return await sp.GetRequiredService<ProjectService>().SaveAsync(new ProjectInput
        {
            Name = "A邸", SiteAddress = "東京", CustomerId = customer, FloorQuantity = floors,
        });
    }

    private static async Task<ParseJob> WaitAsync(FloorImportService svc, Guid jobId)
    {
        for (var i = 0; i < 600; i++)
        {
            var job = svc.GetJob(jobId);
            if (job.Status is ParseJobStatus.Succeeded or ParseJobStatus.Failed) return job;
            await Task.Delay(100);
        }
        throw new TimeoutException("解析が終わりません");
    }

    private static async Task<Guid> ImportAsync(IServiceProvider sp, Guid projectId, string file, string floorName)
    {
        var svc = sp.GetRequiredService<FloorImportService>();
        await using var stream = File.OpenRead(TestHost.TestData(file));
        var uploaded = await svc.UploadAsync(stream, file);
        var jobId = await svc.StartAsync(new ImportInput { ProjectId = projectId, FileId = uploaded.Id, FloorName = floorName, ExcludeDegenerateRectangles = false });
        var job = await WaitAsync(svc, jobId);
        Assert.True(job.Status == ParseJobStatus.Succeeded, job.Error);
        return await svc.SaveAsync(jobId);
    }

    [Fact]
    public async Task Import_parses_in_background_and_saves_same_counts_as_parser()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var projectId = await SeedProjectAsync(sp);

        var floorId = await ImportAsync(sp, projectId, "fuzu1.jww", "1階");

        // 直接解析した結果と、保存された階の内容が一致する
        var direct = FloorParser.Parse(TestHost.TestData("fuzu1.jww"), "1階", Options2);
        var wb = await sp.GetRequiredService<FloorService>().GetWorkbenchAsync(floorId);
        Assert.Equal(direct.Beams.Count, wb.Beams.Count);
        Assert.Equal(direct.Pillars.Count, wb.Pillars.Count);
        Assert.Equal(direct.LinkParts.Count, wb.LinkParts.Count);
        Assert.Equal(direct.Holes.Count, wb.Beams.Sum(b => b.HoleCount));
        Assert.Equal(20, wb.Beams.Count); // 旧版の基準結果（1伏図 × 設定 2）
        Assert.Equal(direct.Beams.Select(b => b.BeamCode).Order(), wb.Beams.Select(b => b.BeamCode).Order());
        Assert.All(wb.Beams, b => Assert.True(b.X != 0 || b.Y != 0)); // 座標が往復で失われない
        Assert.Equal("fuzu1.jww", wb.FileName);

        var project = await sp.GetRequiredService<ProjectService>().GetAsync(projectId);
        Assert.Equal(1, project.ParsedQuantity);
        Assert.Equal(ProjectStatus.Parsed, project.Status);
        Assert.Equal(wb.Beams.Count, project.BeamCount);

        await using var db = await host.RawDbAsync();
        Assert.Equal(direct.Holes.Count, await db.FloorHoles.CountAsync());
        Assert.Equal(direct.DownPillars.Count, await db.FloorDownPillars.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "FloorImport"));
    }

    [Fact]
    public async Task Duplicate_floor_name_and_floor_limit_are_enforced()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var projectId = await SeedProjectAsync(sp, floors: 1);
        await ImportAsync(sp, projectId, "fuzu1.jww", "1階");

        var svc = sp.GetRequiredService<FloorImportService>();
        var ctx = await svc.PrepareAsync(projectId);
        Assert.NotNull(ctx.BlockedReason); // 階数 1 はすべて取り込み済み

        await using var stream = File.OpenRead(TestHost.TestData("fuzu2.jww"));
        var uploaded = await svc.UploadAsync(stream, "fuzu2.jww");
        await Assert.ThrowsAsync<AppException>(() => svc.StartAsync(new ImportInput { ProjectId = projectId, FileId = uploaded.Id, FloorName = "2階" }));

        // 階数を増やせば取り込めるが、同じ階名は不可
        var projects = sp.GetRequiredService<ProjectService>();
        var p = await projects.GetForEditAsync(projectId);
        p.FloorQuantity = 2;
        await projects.SaveAsync(p);
        await Assert.ThrowsAsync<AppException>(() => svc.StartAsync(new ImportInput { ProjectId = projectId, FileId = uploaded.Id, FloorName = "1階" }));
    }

    [Fact]
    public async Task Deleting_floor_recalculates_project()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var projectId = await SeedProjectAsync(sp);
        var f1 = await ImportAsync(sp, projectId, "fuzu1.jww", "1階");
        await ImportAsync(sp, projectId, "fuzu2.jww", "2階");
        var floors = sp.GetRequiredService<FloorService>();
        var projects = sp.GetRequiredService<ProjectService>();
        var before = await projects.GetAsync(projectId);
        Assert.Equal(2, before.ParsedQuantity);

        await floors.DeleteAsync(f1);

        var after = await projects.GetAsync(projectId);
        var remaining = Assert.Single(await floors.ListAsync(projectId));
        Assert.Equal(1, after.ParsedQuantity);
        Assert.Equal(remaining.BeamCount, after.BeamCount);
        Assert.Equal(ProjectStatus.Parsed, after.Status);
    }

    [Fact]
    public async Task Beam_edit_is_audited()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var projectId = await SeedProjectAsync(sp);
        var floorId = await ImportAsync(sp, projectId, "fuzu1.jww", "1階");
        var floors = sp.GetRequiredService<FloorService>();
        var beam = (await floors.GetWorkbenchAsync(floorId)).Beams[0];

        await floors.UpdateBeamAsync(new BeamEditInput { BeamId = beam.Id, BeamCode = "X99", GongQu = "A", HasCsv = beam.HasCsv });

        var updated = (await floors.GetWorkbenchAsync(floorId)).Beams.Single(b => b.Id == beam.Id);
        Assert.Equal("X99", updated.BeamCode);
        Assert.Equal("A", updated.GongQu);
        await using var db = await host.RawDbAsync();
        var log = await db.AuditLogs.SingleAsync(a => a.EntityType == "FloorBeam");
        Assert.Contains("X99", log.Detail);
        Assert.Equal("tanaka", log.UserName);
    }

    /// <summary>
    /// 旧版は解析の設定・縮尺・事件が全部进程级静态变量，同时解析会互相干扰。
    /// 新版没有任何静态可变状态：不同图纸、不同设定同时解析，结果必须与逐个解析完全相同。
    /// </summary>
    [Fact]
    public async Task Concurrent_parses_do_not_interfere()
    {
        var cases = new (string File, ParseOptions Options)[]
        {
            ("fuzu1.jww", Options2),
            ("fuzu2.jww", Options2),
            ("fuzu1.jww", Options2 with { BeamColor = 2, SplitColor = -1, PillarRecognition = PillarDrawingMethod.Line, ExcludeDegenerateRectangles = true }),
            ("fuzu2.jww", Options2 with { KPillarType = KPillarType.A910, MinimalStrategy = true }),
        };
        static string Signature(FloorParseResult r) => string.Join(";",
            r.Beams.Select(b => $"{b.BeamCode}|{b.Length}|{b.CenterPoint}").Order()) + "#" + r.Holes.Count + "#" + r.Pillars.Count
            + "#" + string.Join(",", r.Pillars.Select(p => p.PillarCode).Order()) + "#" + r.LinkParts.Count + "#" + r.Floor.KPillarType + r.Floor.MinimalStrategy;

        var sequential = cases.Select(c => Signature(FloorParser.Parse(TestHost.TestData(c.File), "F", c.Options))).ToList();

        var parallel = await Task.WhenAll(Enumerable.Range(0, 3).SelectMany(_ => cases.Select((c, i) =>
            Task.Run(() => (i, sig: Signature(FloorParser.Parse(TestHost.TestData(c.File), "F", c.Options)))))));

        foreach (var (i, sig) in parallel) Assert.Equal(sequential[i], sig);
        Assert.NotEqual(sequential[0], sequential[2]); // 設定が違えば結果も違う（設定が確かに効いている）
    }
}
