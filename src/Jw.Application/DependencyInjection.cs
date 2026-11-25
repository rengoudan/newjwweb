using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CurrentUserState>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<OperationScope>();

        services.AddScoped<AuditQueryService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<CustomerParseSettingService>();
        services.AddScoped<Projects.ProjectService>();
        services.AddScoped<Materials.MaterialService>();
        services.AddScoped<Budgets.BudgetService>();
        services.AddScoped<Floors.FloorImportService>();
        services.AddScoped<Floors.FloorService>();
        services.AddScoped<Floors.ExportService>();
        services.AddScoped<Floors.FloorSceneService>();
        services.AddScoped<Floors.WorkbenchService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<RoleAdminService>();
        return services;
    }
}
