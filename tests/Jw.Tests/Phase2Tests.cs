using System.Text.Json;
using Jw.Application.Budgets;
using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Application.Materials;
using Jw.Application.Projects;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NPOI.XSSF.UserModel;

namespace Jw.Tests;

public class Phase2Tests
{
    private static readonly string[] Operator = Permissions.DefaultsFor("Operator").ToArray();

    private static async Task<(Guid customer, Guid project)> SeedProjectAsync(IServiceProvider sp, int floors = 3)
    {
        var customer = await sp.GetRequiredService<CustomerService>().SaveAsync(new CustomerInput { CompanyName = "山田建設" });
        var project = await sp.GetRequiredService<ProjectService>().SaveAsync(new ProjectInput
        {
            Name = "A邸新築", SiteAddress = "東京都", CustomerId = customer, FloorQuantity = floors,
        });
        return (customer, project);
    }

    private static async Task<Guid> SeedMaterialAsync(IServiceProvider sp, string name = "H形鋼", string spec = "200x100", decimal price = 1200m,
        MaterialKind kind = MaterialKind.Beam)
    {
        var svc = sp.GetRequiredService<MaterialService>();
        var cats = await svc.CategoriesAsync();
        var catId = cats.FirstOrDefault(c => c.Kind == kind)?.Id
                    ?? await svc.SaveCategoryAsync(new CategoryInput { Name = EnumNames.Of(kind) + "材", Kind = kind });
        return await svc.SaveMaterialAsync(new MaterialInput
        {
            CategoryId = catId, Name = name, Spec = spec, UnitName = "m", UnitPrice = price, GeneralTitle = name,
        });
    }

    // ---------- 工事 ----------

    [Fact]
    public async Task Project_create_and_list_with_customer_name()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var (_, projectId) = await SeedProjectAsync(scope.ServiceProvider);

        var item = Assert.Single(await scope.ServiceProvider.GetRequiredService<ProjectService>().ListAsync());
        Assert.Equal(projectId, item.Id);
        Assert.Equal("山田建設", item.CustomerName);
        Assert.Equal(ProjectStatus.Created, item.Status);
        Assert.Equal(3, item.FloorQuantity);
        Assert.Equal("tanaka", item.CreatedByName);
    }

    [Fact]
    public async Task Floor_quantity_cannot_go_below_parsed_floors()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var (_, projectId) = await SeedProjectAsync(scope.ServiceProvider);

        // 模拟已经解析了 2 层（第 3 阶段由导入写入）
        await using (var db = await host.RawDbAsync())
        {
            await db.Projects.Where(p => p.Id == projectId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ParsedQuantity, 2));
        }

        var svc = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var input = await svc.GetForEditAsync(projectId);
        input.FloorQuantity = 1;
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.SaveAsync(input));
        Assert.Contains("2", ex.Message);

        input = await svc.GetForEditAsync(projectId);
        input.FloorQuantity = 2;
        await svc.SaveAsync(input);
    }

    [Fact]
    public async Task Deleting_project_soft_deletes_its_budget_and_lines()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var (_, projectId) = await SeedProjectAsync(sp);
        var materialId = await SeedMaterialAsync(sp);
        var budgets = sp.GetRequiredService<BudgetService>();
        var budgetId = await budgets.CreateAsync(projectId);
        await budgets.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, MaterialId = materialId, Quantity = 2 });

        await sp.GetRequiredService<ProjectService>().DeleteAsync(projectId);

        Assert.Empty(await sp.GetRequiredService<ProjectService>().ListAsync());
        Assert.Empty(await budgets.ListAsync());
        await using var db = await host.RawDbAsync();
        Assert.True((await db.Budgets.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.True((await db.BudgetLines.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityType == "Project" && a.Action == AuditActions.Delete));
    }

    [Fact]
    public async Task Viewer_cannot_edit_projects()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("viewer", Permissions.DefaultsFor("Viewer").ToArray());
        var svc = scope.ServiceProvider.GetRequiredService<ProjectService>();
        await svc.ListAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.SaveAsync(new ProjectInput { Name = "x", SiteAddress = "y" }));
    }

    // ---------- 顧客 ----------

    [Fact]
    public async Task Customer_with_projects_cannot_be_deleted()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var (customerId, _) = await SeedProjectAsync(scope.ServiceProvider);
        var ex = await Assert.ThrowsAsync<AppException>(() => scope.ServiceProvider.GetRequiredService<CustomerService>().DeleteAsync(customerId));
        Assert.Contains("工事", ex.Message);
    }

    [Fact]
    public async Task Parse_setting_is_created_then_updated_with_field_audit()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var customerId = await sp.GetRequiredService<CustomerService>().SaveAsync(new CustomerInput { CompanyName = "B社" });
        var svc = sp.GetRequiredService<CustomerParseSettingService>();

        var s = await svc.GetAsync(customerId);
        Assert.False(s.Exists);
        s.BeamColor = 2;
        s.PillarRecognition = ShapeRecognition.Shape;
        await svc.SaveAsync(s);

        s = await svc.GetAsync(customerId);
        Assert.True(s.Exists);
        Assert.Equal(2, s.BeamColor);
        s.BeamColor = 5;
        await svc.SaveAsync(s);

        Assert.True(Assert.Single(await sp.GetRequiredService<CustomerService>().ListAsync()).HasParseSetting);
        await using var db = await host.RawDbAsync();
        var update = await db.AuditLogs.SingleAsync(a => a.EntityType == "CustomerParseSetting" && a.Action == AuditActions.Update);
        var change = Assert.Single(JsonSerializer.Deserialize<List<AuditChange>>(update.Detail!)!);
        Assert.Equal(new AuditChange("BeamColor", "2", "5"), change);
    }

    // ---------- 基礎データ ----------

    [Fact]
    public async Task Material_name_and_spec_must_be_unique_and_category_with_materials_cannot_be_deleted()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var svc = sp.GetRequiredService<MaterialService>();
        await SeedMaterialAsync(sp, "H形鋼", "200x100");
        await SeedMaterialAsync(sp, "H形鋼", "250x125"); // 规格不同可以

        await Assert.ThrowsAsync<AppException>(() => SeedMaterialAsync(sp, "H形鋼", "200x100"));

        var cat = Assert.Single(await svc.CategoriesAsync());
        Assert.Equal(2, cat.MaterialCount);
        await Assert.ThrowsAsync<AppException>(() => svc.DeleteCategoryAsync(cat.Id));
    }

    [Fact]
    public async Task Default_material_is_cleared_when_material_is_deleted()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var svc = sp.GetRequiredService<MaterialService>();
        var id = await SeedMaterialAsync(sp);
        var cat = Assert.Single(await svc.CategoriesAsync());

        await svc.SetDefaultAsync(cat.Id, id);
        Assert.Equal(id, Assert.Single(await svc.CategoriesAsync()).DefaultMaterialId);

        await svc.DeleteMaterialAsync(id);
        Assert.Null(Assert.Single(await svc.CategoriesAsync()).DefaultMaterialId);
    }

    // ---------- 予算 ----------

    [Fact]
    public async Task Budget_lines_snapshot_material_and_totals_follow_changes()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var (_, projectId) = await SeedProjectAsync(sp);
        var beam = await SeedMaterialAsync(sp, "H形鋼", "200x100", 1200m);
        var bolt = await SeedMaterialAsync(sp, "B金物", "", 350m, MaterialKind.Hardware);
        var svc = sp.GetRequiredService<BudgetService>();

        var budgetId = await svc.CreateAsync(projectId);
        Assert.Equal(ProjectStatus.Budgeted, (await sp.GetRequiredService<ProjectService>().GetAsync(projectId)).Status);
        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(projectId)); // 每个工事一份

        await svc.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, MaterialId = beam, Quantity = 12.5m });
        await svc.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, MaterialId = bolt, Quantity = 10, UnitPrice = 300m });

        var detail = await svc.GetAsync(budgetId);
        Assert.Equal(2, detail.Lines.Count);
        var beamLine = detail.Lines.Single(l => l.MaterialId == beam);
        Assert.Equal("H形鋼", beamLine.ItemName);
        Assert.Equal(15000m, beamLine.Amount);
        Assert.Equal(3000m, detail.Lines.Single(l => l.MaterialId == bolt).Amount);
        Assert.Equal(18000m, detail.Subtotal);
        Assert.Equal(1800m, detail.Tax);
        Assert.Equal(19800m, detail.Total);
        Assert.Equal(18000m, Assert.Single(await svc.ListAsync()).Amount);

        // 修改材料单价不影响已有明细（快照）
        var materials = sp.GetRequiredService<MaterialService>();
        var m = await materials.GetMaterialForEditAsync(beam);
        m.UnitPrice = 9999m;
        await materials.SaveMaterialAsync(m);
        Assert.Equal(15000m, (await svc.GetAsync(budgetId)).Lines.Single(l => l.MaterialId == beam).Amount);

        // 修改数量、删除明细，合计跟着变
        await svc.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, LineId = beamLine.Id, Quantity = 10, ConcurrencyStamp = beamLine.ConcurrencyStamp });
        Assert.Equal(15000m, (await svc.GetAsync(budgetId)).Subtotal);
        await svc.DeleteLineAsync(budgetId, beamLine.Id);
        Assert.Equal(3000m, (await svc.GetAsync(budgetId)).Subtotal);
        Assert.Equal(3000m, Assert.Single(await svc.ListAsync()).Amount);

        // 删除予算后工事状态回到“作成済み”
        await svc.DeleteAsync(budgetId);
        Assert.Equal(ProjectStatus.Created, (await sp.GetRequiredService<ProjectService>().GetAsync(projectId)).Status);
    }

    [Fact]
    public async Task Quotation_export_fills_template_and_is_audited()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Operator);
        var sp = scope.ServiceProvider;
        var (_, projectId) = await SeedProjectAsync(sp);
        var beam = await SeedMaterialAsync(sp, "H形鋼", "200x100", 1200m);
        var svc = sp.GetRequiredService<BudgetService>();
        var budgetId = await svc.CreateAsync(projectId);
        await svc.SaveLineAsync(new BudgetLineInput { BudgetId = budgetId, MaterialId = beam, Quantity = 10 });

        var file = await svc.ExportQuotationAsync(budgetId);

        Assert.Equal("A邸新築見積書.xlsx", file.FileName);
        using var book = new XSSFWorkbook(new MemoryStream(file.Content));
        var sheet = book.GetSheetAt(0);
        Assert.EndsWith("A邸新築", sheet.GetRow(0).GetCell(0).StringCellValue);
        var line = sheet.GetRow(11);
        Assert.Equal("H形鋼", line.GetCell(0).StringCellValue);
        Assert.Equal("200x100", line.GetCell(4).StringCellValue);
        Assert.Equal(12000d, line.GetCell(10).NumericCellValue);
        Assert.Equal("小  計", sheet.GetRow(12).GetCell(0).StringCellValue);
        Assert.Equal(12000d, sheet.GetRow(12).GetCell(10).NumericCellValue);
        Assert.Equal("総  合  計", sheet.GetRow(15).GetCell(0).StringCellValue);
        Assert.Equal(13200d, sheet.GetRow(15).GetCell(10).NumericCellValue);

        await using var db = await host.RawDbAsync();
        var log = await db.AuditLogs.SingleAsync(a => a.Action == "ExportQuotation");
        Assert.Equal(AuditCategory.Action, log.Category);
        Assert.Equal("tanaka", log.UserName);
    }
}
