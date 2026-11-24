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
        services.AddScoped<UserAdminService>();
        services.AddScoped<RoleAdminService>();
        return services;
    }
}
