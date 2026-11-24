using System.Text.Json;
using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Tests;

public class CustomerAuditTests
{
    private static readonly string[] Edit = [Permissions.Customers.View, Permissions.Customers.Edit, Permissions.Customers.Delete];

    [Fact]
    public async Task Create_fills_audit_fields_and_writes_create_log()
    {
        await using var host = await TestHost.CreateAsync();
        Guid id;
        await using (var scope = host.ScopeAs("tanaka", Edit))
            id = await scope.ServiceProvider.GetRequiredService<CustomerService>()
                .SaveAsync(new CustomerInput { CompanyName = "  山田建設 ", Contact = "山田" });

        await using var db = await host.RawDbAsync();
        var c = await db.Customers.SingleAsync(x => x.Id == id);
        Assert.Equal("山田建設", c.CompanyName);
        Assert.Equal("tanaka", c.CreatedByName);
        Assert.NotEqual(default, c.CreatedAt);
        Assert.Null(c.ModifiedAt);

        var log = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString());
        Assert.Equal(AuditCategory.Data, log.Category);
        Assert.Equal(AuditActions.Create, log.Action);
        Assert.Equal("Customer", log.EntityType);
        Assert.Equal("tanaka", log.UserName);
        Assert.Equal("10.0.0.1", log.IpAddress);
        Assert.Contains("山田建設", log.Summary);
        var changes = JsonSerializer.Deserialize<List<AuditChange>>(log.Detail!)!;
        Assert.Contains(changes, ch => ch.Property == "CompanyName" && ch.New == "山田建設" && ch.Old == null);
        Assert.DoesNotContain(changes, ch => ch.Property is "CreatedByName" or "ConcurrencyStamp" or "IsDeleted");
    }

    [Fact]
    public async Task Update_logs_only_changed_fields_and_keeps_creator()
    {
        await using var host = await TestHost.CreateAsync();
        Guid id;
        await using (var s1 = host.ScopeAs("tanaka", Edit))
            id = await s1.ServiceProvider.GetRequiredService<CustomerService>()
                .SaveAsync(new CustomerInput { CompanyName = "A社", Telephone = "03-1111" });

        await using (var s2 = host.ScopeAs("suzuki", Edit))
        {
            var svc = s2.ServiceProvider.GetRequiredService<CustomerService>();
            var input = await svc.GetForEditAsync(id);
            input.Telephone = "03-2222";
            await svc.SaveAsync(input);
        }

        await using var db = await host.RawDbAsync();
        var c = await db.Customers.SingleAsync(x => x.Id == id);
        Assert.Equal("tanaka", c.CreatedByName);
        Assert.Equal("suzuki", c.ModifiedByName);
        Assert.NotNull(c.ModifiedAt);

        var log = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString() && a.Action == AuditActions.Update);
        Assert.Equal("suzuki", log.UserName);
        var change = Assert.Single(JsonSerializer.Deserialize<List<AuditChange>>(log.Detail!)!);
        Assert.Equal(new AuditChange("Telephone", "03-1111", "03-2222"), change);
    }

    [Fact]
    public async Task Saving_without_changes_writes_no_update_log()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Edit);
        var svc = scope.ServiceProvider.GetRequiredService<CustomerService>();
        var id = await svc.SaveAsync(new CustomerInput { CompanyName = "B社" });
        await svc.SaveAsync(await svc.GetForEditAsync(id));

        await using var db = await host.RawDbAsync();
        Assert.Equal(0, await db.AuditLogs.CountAsync(a => a.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Delete_is_soft_and_logged()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Edit);
        var svc = scope.ServiceProvider.GetRequiredService<CustomerService>();
        var id = await svc.SaveAsync(new CustomerInput { CompanyName = "C社" });

        await svc.DeleteAsync(id);

        Assert.Empty(await svc.ListAsync());
        await using var db = await host.RawDbAsync();
        var c = await db.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        Assert.True(c.IsDeleted);
        Assert.Equal("tanaka", c.DeletedByName);
        Assert.Single(await db.AuditLogs.Where(a => a.EntityId == id.ToString() && a.Action == AuditActions.Delete).ToListAsync());

        // 同名可以重新登记（唯一索引只针对未删除的数据）
        await svc.SaveAsync(new CustomerInput { CompanyName = "C社" });
    }

    [Fact]
    public async Task Concurrent_edit_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        Guid id;
        await using (var s = host.ScopeAs("tanaka", Edit))
            id = await s.ServiceProvider.GetRequiredService<CustomerService>().SaveAsync(new CustomerInput { CompanyName = "D社" });

        await using var a = host.ScopeAs("tanaka", Edit);
        await using var b = host.ScopeAs("suzuki", Edit);
        var svcA = a.ServiceProvider.GetRequiredService<CustomerService>();
        var svcB = b.ServiceProvider.GetRequiredService<CustomerService>();
        var formA = await svcA.GetForEditAsync(id);
        var formB = await svcB.GetForEditAsync(id);

        formA.Contact = "田中";
        await svcA.SaveAsync(formA);

        formB.Contact = "鈴木";
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => svcB.SaveAsync(formB));
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("tanaka", Edit);
        var svc = scope.ServiceProvider.GetRequiredService<CustomerService>();
        await svc.SaveAsync(new CustomerInput { CompanyName = "E社" });
        await Assert.ThrowsAsync<AppException>(() => svc.SaveAsync(new CustomerInput { CompanyName = "E社" }));
    }

    [Fact]
    public async Task Service_rejects_users_without_permission()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.ScopeAs("viewer", Permissions.Customers.View);
        var svc = scope.ServiceProvider.GetRequiredService<CustomerService>();
        await svc.ListAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.SaveAsync(new CustomerInput { CompanyName = "F社" }));
    }
}
